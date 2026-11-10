using XargonNET.Assets;
using XargonNET.Objects;
using XargonNET.Ui;
using XargonNET.Video;

namespace XargonNET.Engine;

/// <summary>
/// Owns the cross-board state for death, respawn and level transitions — the port of XARGON.C's
/// play() newlevel handling plus p_reenter/stats/saveboard/loadboard. It is deliberately free of
/// SDL so it can be unit-tested headlessly: it loads boards through an injected loader delegate and
/// builds the <see cref="ObjectManager"/>/<see cref="Behaviors"/> world around them.
///
/// C mapping:
/// <list type="bullet">
/// <item><c>saveboard(tempname)</c> — <see cref="EnterLevel"/> deep-copies the live map into
/// <c>_savedMap</c>.</item>
/// <item><c>stats(1)</c> — snapshot the live player state into <c>_oPl</c> on entering a level.</item>
/// <item><c>stats(0)</c> — <see cref="Restore"/> copies the snapshot back and strips the per-level
/// items (epic, extra lasers, rocks, jump, invincibility, keys).</item>
/// <item><c>loadboard</c> — the injected <c>_load</c> delegate returns a fresh board.</item>
/// <item><c>p_reenter</c> — <see cref="ObjectManager.ReenterPlayer"/> plus
/// <see cref="Camera.SetOrigin"/>; the died-and-checkpoint-state==1 reload lives in <see cref="Die"/>.</item>
/// </list>
/// </summary>
public sealed class LevelSession
{
    /// <summary>The map hero's pl.level sentinel (no level checkpoint matches it, so the hero keeps
    /// its stored map position on reenter).</summary>
    public const int MapLevel = 127;

    /// <summary>A built world: the loaded board and the object manager bound to it.</summary>
    public readonly record struct World(Board Board, ObjectManager Objects);

    private readonly Func<string, Board> _load;
    private readonly ShapeManager _shapes;
    private readonly InfoTable _info;
    private readonly VgaScreen _screen;
    private readonly Viewport _vp;
    private readonly PlayerState _pl;
    private readonly TextRenderer? _text;
    private readonly int _episode;

    private Board? _savedMap;      // saveboard(tempname): the stashed overworld map
    private PlayerState? _oPl;     // stats(1): the level-entry player snapshot
    private int _completedLevel;   // pl.level captured at level entry (which map checkpoint to retire)
    private string _curBoardName = ""; // name of the current board (for the death reload)

    private Board _curBoard = null!;
    private ObjectManager _curObjects = null!;

    public LevelSession(Func<string, Board> loadBoard, ShapeManager shapes, InfoTable info,
        VgaScreen screen, Viewport gameVp, PlayerState pl, TextRenderer? text = null, int episode = 1)
    {
        _episode = episode;
        _load = loadBoard;
        _shapes = shapes;
        _info = info;
        _screen = screen;
        _vp = gameVp;
        _pl = pl;
        _text = text;
    }

    /// <summary>Whether a map snapshot is currently stashed (i.e. the player is inside a level).</summary>
    public bool InLevel => _savedMap is not null;

    /// <summary>The blocking popup host handed to every world this session builds (default: no-op).</summary>
    public IPopupHost Popups { get; set; } = NullPopupHost.Instance;

    /// <summary>The per-episode first-time hint flags shared by every world (so they persist across
    /// boards, deaths and map&lt;-&gt;level transitions). Default: a fresh all-true set.</summary>
    public HintFlags Hints { get; set; } = new();

    private Action? _pendingPalette;

    /// <summary>
    /// Apply the palette work a deferred <see cref="EnterLevel"/>/<see cref="ExitToMap"/> postponed (the
    /// sky colours of the new board, and on map return init_colors()). In the C this happens inside the
    /// game loop after donelevelmsg (the effects object's first update; o_col=1 handled after refresh),
    /// not in loadboard, so the old frame behind the level banner must not recolour. No-op if nothing pending.
    /// </summary>
    public void ApplyPendingPalette()
    {
        var p = _pendingPalette;
        _pendingPalette = null;
        p?.Invoke();
    }

    /// <summary>Build the object world for a board and apply (or, when deferring, queue) its effects palette.</summary>
    private ObjectManager BuildWorld(Board b, bool deferPalette = false)
    {
        if (deferPalette) _pendingPalette = () => Effects.ApplyForBoard(_screen, b);
        else { _pendingPalette = null; Effects.ApplyForBoard(_screen, b); }
        var om = new ObjectManager(b, _shapes, _info, _screen, _pl, _episode)
        { Text = _text, Popups = Popups, Hints = Hints };
        _ = new Behaviors(om);
        return om;
    }

    private World Adopt(Board b, string name, bool deferPalette = false)
    {
        var om = BuildWorld(b, deferPalette);
        _curBoard = b;
        _curObjects = om;
        _curBoardName = name;
        return new World(b, om);
    }

    private void ReenterAndFrame(ObjectManager om, int level)
    {
        om.ReenterPlayer(level);
        Camera.SetOrigin(_vp, om.Obj(0));
    }

    /// <summary>
    /// Load a board fresh and frame the hero at its checkpoint (used for the initial overworld map and
    /// any direct board load). Does not stash: this is a plain loadboard + p_reenter.
    /// </summary>
    public World Load(string board)
    {
        var w = Adopt(_load(board), board);
        ReenterAndFrame(w.Objects, _pl.Level);
        return w;
    }

    /// <summary>
    /// Enter a level from the overworld (the else branch of play()'s newlevel handling): stash the live
    /// map (saveboard) and snapshot the player (stats(1)), load the level board, restore+strip the
    /// snapshot (stats(0)), then p_reenter the hero at the level's checkpoint.
    /// </summary>
    public World EnterLevel(string levelBoard, bool deferPalette = false)
    {
        // The putlevelmsg/donelevelmsg banner around this call is run by GameApp.HandleTransition.
        _savedMap = _curBoard.Clone();   // saveboard(tempname)
        _oPl = _pl.Clone();              // stats(1)
        _completedLevel = _pl.Level;     // the level number the map checkpoint set

        var w = Adopt(_load(levelBoard), levelBoard, deferPalette); // loadboard(newlevel)
        Restore();                       // stats(0): restore the map pl (no-op) and strip per-level items
        ReenterAndFrame(w.Objects, _pl.Level);
        return w;
    }

    /// <summary>
    /// Return to the overworld (the '!' branch): restore the stashed map (loadboard tempname),
    /// re-snapshot/restore/strip the player (stats(1)/stats(0)), p_reenter the map hero, then retire the
    /// completed level's checkpoint on the map (findcheckpt(pl.level); killobj) so it reads as done.
    /// </summary>
    public World ExitToMap(bool deferPalette = false)
    {
        // The putlevelmsg(0)/donelevelmsg banner around this call is run by GameApp.HandleTransition.
        var map = _savedMap ?? _curBoard.Clone();
        _savedMap = null;

        _oPl = _pl.Clone();              // stats(1)
        var w = Adopt(map, "MAP", deferPalette); // loadboard(tempname)
        Restore();                       // stats(0)
        if (deferPalette)                // o_col=1 -> init_colors() runs after donelevelmsg
        {
            var sky = _pendingPalette;
            _pendingPalette = () => { sky?.Invoke(); Effects.InitColors(_screen); };
        }
        else Effects.InitColors(_screen); // o_col=1 -> init_colors(): drop the level's sky / hero flash
        ReenterAndFrame(w.Objects, MapLevel); // p_reenter(0): map hero keeps its stored position

        int c = w.Objects.FindCheckpt(_completedLevel);
        if (c > 0) w.Objects.KillObj(c);
        return w;
    }

    /// <summary>
    /// Die (p_reenter(1)): if the hero's checkpoint for pl.level is a reloading checkpoint (state==1),
    /// reload the level board and restore+strip the entry snapshot (stats(0)); otherwise just reposition
    /// on the current board. Either way the hero reenters at the checkpoint with full health.
    /// </summary>
    public World Die()
    {
        int n = _curObjects.FindCheckpt(_pl.Level);
        bool reload = n > 0 && _curObjects.Obj(n).State == 1 && _oPl is not null;

        if (reload)
        {
            var w = Adopt(_load(_curBoardName), _curBoardName); // loadboard(curlevel)
            Restore();                                          // stats(0)
            ReenterAndFrame(w.Objects, _pl.Level);
            return w;
        }

        // Non-reloading death: respawn in place (no board reload, no stats restore).
        ReenterAndFrame(_curObjects, _pl.Level);
        return new World(_curBoard, _curObjects);
    }

    /// <summary>stats(0): copy the snapshot back over the live player and strip the per-level items.</summary>
    private void Restore()
    {
        if (_oPl is not null) _pl.CopyFrom(_oPl);
        Strip(_curObjects);
    }

    /// <summary>The item-stripping tail of stats(0): remove the epic, any lasers past the first, all
    /// rocks/jump/invincibility, and all gate keys.</summary>
    private static void Strip(ObjectManager om)
    {
        while (om.InvCount(Inv.Epic) > 0) om.TakeInv(Inv.Epic);
        while (om.InvCount(Inv.Laser) > 1) om.TakeInv(Inv.Laser);
        while (om.InvCount(Inv.Rock) > 0) om.TakeInv(Inv.Rock);
        while (om.InvCount(Inv.Jump) > 0) om.TakeInv(Inv.Jump);
        while (om.InvCount(Inv.Invin) > 0) om.TakeInv(Inv.Invin);
        for (int k = 0; k < 4; k++)
            while (om.InvCount(Inv.Key(k)) > 0) om.TakeInv(Inv.Key(k));
    }
}
