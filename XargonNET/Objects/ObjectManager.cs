using XargonNET.Text;
using XargonNET.Assets;
using XargonNET.Video;

namespace XargonNET.Objects;

/// <summary>
/// Holds the level's live objects, draws them, and provides the block-collision primitives that
/// the player and object behaviours build on (a port of the relevant parts of X_OBJMAN.C).
///
/// Rendering: each object is drawn at its kind's resting shape with back/front layering.
/// Behaviour: a <see cref="ObjMsg"/> dispatch table (kindmsg) is set up here; individual handlers
/// are filled in a later phase. Collision: <see cref="Cando"/>/<see cref="TryMove"/> etc. test an
/// object's footprint against info[] block flags (a block is solid unless f_playerthru).
/// </summary>
public sealed class ObjectManager
{
    private const int MaxScrnObjs = 192;
    private const int ObjKillme = ObjectKind.Killme;

    private readonly List<Obj> _objects;
    private readonly ShapeManager _shapes;
    private readonly Board _board;
    private readonly InfoTable _info;
    private readonly VgaScreen _screen;
    private readonly ObjMsg[] _kindMsg = new ObjMsg[KindDefs.Count];
    private readonly bool[] _hasBehavior = new bool[KindDefs.Count];
    private readonly Random _rng = new(12345);

    // Screen-object cull list (scrnobjs), current viewport, and current draw context.
    private readonly int[] _scrnObjs = new int[MaxScrnObjs];
    private int _numScrn;
    private Viewport _curVp = null!;
    private VgaScreen _drawScreen = null!;
    private Viewport _drawVp = null!;

    // stateinfo (X_INFO.C): per player-state flags. Indexed by state 0..7.
    private const int StiCanfire = 1, StiInvincible = 2;
    private static readonly int[] StateInfo =
    {
        StiCanfire, StiInvincible, StiCanfire, 0, StiInvincible, StiInvincible, StiInvincible, StiCanfire,
    };
    public static bool IsInvincible(int state) => (uint)state < 8 && (StateInfo[state] & StiInvincible) != 0;
    public static bool CanFire(int state) => (uint)state < 8 && (StateInfo[state] & StiCanfire) != 0;

    /// <summary>Player/inventory state (pl). Persists across boards when supplied by the caller.</summary>
    public PlayerState Pl { get; }

    /// <summary>Frame counter (gamecount) — drives animations and colour cycling.</summary>
    public int Gamecount;

    /// <summary>Set by a checkpoint to request a board/level transition (newlevel).</summary>
    public string? NewLevel;

    /// <summary>Set when the end-of-game checkpoint (state 4) is touched (the C's gameover=2).</summary>
    public bool GameWon;

    /// <summary>Text renderer for object text (kinds 6/7). Null = text objects draw nothing.</summary>
    public Ui.TextRenderer? Text { get; set; }

    /// <summary>The bottom status line (botmsg/bottime/text_flg).</summary>
    public Ui.StatusLine Status { get; } = new();

    /// <summary>The blocking popup host (dotextmsg/dialogmsg). Default is a no-op so headless code and
    /// tests never open a window; <c>GameApp</c> installs the real modal implementation.</summary>
    public IPopupHost Popups { get; set; } = NullPopupHost.Instance;

    /// <summary>First-time tutorial hint flags (first_*). Defaults to a fresh all-true set; the live
    /// game hands in the per-episode instance so the hints persist across boards and deaths.</summary>
    public HintFlags Hints { get; set; } = new();

    /// <summary>dotextmsg(n, flg): raise a text popup (blocks until dismissed in the live game).</summary>
    public void TextMsg(int n, PopupStyle s = PopupStyle.Plain) => Popups.TextMessage(n, s);

    /// <summary>dialogmsg(n, flg): raise a portrait dialog popup (Task 7).</summary>
    public void DialogMsg(int n, bool eagle) => Popups.DialogMessage(n, eagle);

    /// <summary>The current gameplay viewport origin (gamevp.vpox/vpoy) during an update pass — used by
    /// the HUD-pinned map banner (msg_mapdemo). 0 when no update is in flight.</summary>
    public int ViewOriginX => _curVp?.OriginX ?? 0;
    public int ViewOriginY => _curVp?.OriginY ?? 0;

    /// <summary>The episode's default status message (v_msg), shown once a transient message expires.</summary>
    public string DefaultStatusMessage { get; }

    /// <summary>The episode number (1-3) this world belongs to; selects episode-specific strings.</summary>
    public int Episode { get; }

    /// <summary>txt(): post a message on the bottom status line.</summary>
    public void Txt(string msg, int col, int flg) => Status.Txt(msg, col, flg);

    /// <summary>upd_botmsg(): once per game tick, after <see cref="UpdateObjects"/>.</summary>
    public void UpdBotMsg() => Status.Update(DefaultStatusMessage);

    public ObjectManager(Board board, ShapeManager shapes, InfoTable info, VgaScreen screen, PlayerState? pl = null,
        int episode = 1)
    {
        Episode = episode;
        DefaultStatusMessage = XargonNET.Text.GameStrings.VMsg(episode);
        Status.Txt(DefaultStatusMessage, 4, 1); // play(): txt(v_msg,4,1) when a level/map starts
        _objects = board.Objects;
        _shapes = shapes;
        _board = board;
        _info = info;
        _screen = screen;
        Pl = pl ?? board.Player;

        for (int k = 0; k < _kindMsg.Length; k++) _kindMsg[k] = (_, _, _) => 0;
        foreach (var o in _objects) SetObjSize(o);
    }

    public IReadOnlyList<Obj> Objects => _objects;
    public VgaScreen Screen => _screen;
    public Obj Obj(int n) => _objects[n];
    public int XrRandom(int n) => n <= 0 ? 0 : _rng.Next(n);

    public void Register(int kind, ObjMsg handler)
    {
        if ((uint)kind < KindDefs.Count) { _kindMsg[kind] = handler; _hasBehavior[kind] = true; }
    }

    /// <summary>Whether a behaviour handler has been registered for this kind (for the census).</summary>
    public bool HasBehavior(int kind) => (uint)kind < KindDefs.Count && _hasBehavior[kind];

    /// <summary>Dispatch a message to object n's kind handler (kindmsg[objkind]).</summary>
    public int Dispatch(int n, int msg, int z)
    {
        if ((uint)n >= (uint)_objects.Count) return 0;
        return _kindMsg[_objects[n].Kind](n, msg, z);
    }

    // ---- Object lifecycle (X_OBJMAN.C) ----------------------------------------------------

    /// <summary>addobj(): spawn a new object; returns its index.</summary>
    public int AddObj(int kind, int x, int y, int xd, int yd)
    {
        var o = new Obj { Kind = (byte)kind, X = x, Y = y, Xd = xd, Yd = yd };
        SetObjSize(o);
        _objects.Add(o);
        return _objects.Count - 1;
    }

    /// <summary>
    /// killobj(): mark object n for removal (becomes obj_killme, purged after the frame). The C also
    /// sets objflags|=mod_screen, which is only a redraw-dirty bit on the dying object (not observable here).
    /// </summary>
    public void KillObj(int n) => _objects[n].Kind = ObjKillme;

    // notemod()/modboard(): cells touched by a collected item. In the C, modboard marks
    // bd[x][y] with mod_screen; here it is recorded so a later task can snapshot it with the board.
    private readonly HashSet<(int X, int Y)> _modifiedCells = new();

    /// <summary>Board cells marked by notemod (modboard).</summary>
    public IReadOnlyCollection<(int X, int Y)> ModifiedCells => _modifiedCells;


    /// <summary>notemod(): mark every cell under object n's footprint as modified.</summary>
    public void NoteMod(int n)
    {
        var o = _objects[n];
        int startx = o.X / 16, starty = o.Y / 16;
        int endx = (o.X + o.Xl + 15) / 16, endy = (o.Y + o.Yl + 15) / 16;
        for (int y = starty; y < endy; y++)
            for (int x = startx; x < endx; x++)
                if ((uint)x < Board.W && (uint)y < Board.H) _modifiedCells.Add((x, y));
    }

    /// <summary>playerkill(): award the kind's score at the object, note it modified, and remove it.</summary>
    public void PlayerKill(int n) => PlayerKill(n, KindDefs.Get(_objects[n].Kind).Score);

    /// <summary>playerkill() with an explicit score (the C mutates kindscore[] first, e.g. msg_bonus).</summary>
    public void PlayerKill(int n, int score)
    {
        AddScore(score, _objects[n].X, _objects[n].Y);
        NoteMod(n);
        KillObj(n);
    }

    /// <summary>addscore(): add to the score and spawn a floating score popup (obj_score) at (x,y).</summary>
    public void AddScore(int sc, int x, int y)
    {
        int n = AddObj(ObjectKind.Score, x, y, 0, 0);
        var o = _objects[n];
        o.State = sc;
        o.Counter = 30;
        int px = _objects[0].X;
        o.Xd = XrRandom(4) * ((x > px ? 1 : 0) - (x < px ? 1 : 0));
        o.Yd = XrRandom(4) + 4;
        SetObjSize(o);
        Pl.Score += (uint)sc;
    }

    public int InvCount(int inv)
    {
        int c = 0;
        for (int i = 0; i < Pl.NumInv; i++) if (Pl.Inv[i] == inv) c++;
        return c;
    }

    public void AddInv(int inv)
    {
        if (Pl.NumInv < 29) Pl.Inv[Pl.NumInv++] = inv;
    }

    public bool TakeInv(int inv)
    {
        for (int i = 0; i < Pl.NumInv; i++)
            if (Pl.Inv[i] == inv)
            {
                for (int d = i + 1; d < Pl.NumInv; d++) Pl.Inv[d - 1] = Pl.Inv[d];
                Pl.NumInv--;
                return true;
            }
        return false;
    }

    /// <summary>countobj(): number of objects of a kind; with flg, only those whose state==1.</summary>
    public int CountObj(int kind, bool flg)
    {
        int n = 0;
        foreach (var o in _objects)
            if (o.Kind == kind && (!flg || o.State == 1)) n++;
        return n;
    }

    private void PurgeObjs()
    {
        // Keep the player (index 0); drop killed objects.
        for (int i = _objects.Count - 1; i >= 1; i--)
            if (_objects[i].Kind == ObjKillme) _objects.RemoveAt(i);
    }

    // ---- Per-frame update (upd_objs) ------------------------------------------------------

    /// <summary>
    /// Update on-screen objects for one tick: cull to the viewport (+margin) or f_always kinds,
    /// dispatch msg_update to each (the player, object 0, is updated separately), then handle the
    /// player touching pickups/hazards. Killed objects are purged at the end.
    /// </summary>
    public void UpdateObjects(Viewport vp, bool touch = true)
    {
        _curVp = vp;
        BuildScrnObjs(vp);
        for (int i = 0; i < _numScrn; i++)
        {
            int n = _scrnObjs[i];
            // upd_objs: each frame, tick down the per-object touch cooldown so an enemy can damage
            // the player again once its zaphold reaches 0 (otherwise it only ever hits once).
            if (touch && _objects[n].ZapHold > 0) _objects[n].ZapHold--;
            if (n == 0) continue; // player handled by Player.Update
            if (_hasBehavior[_objects[n].Kind]) _kindMsg[_objects[n].Kind](n, Msg.Update, 0);
        }
        if (touch) HandleTouch();
        UpdateBlocks(vp);
        PurgeObjs();
    }

    /// <summary>
    /// Animate f_msgupdate blocks in view (upd_bkgnd + msg_block): lava, water surfaces, acid, and
    /// other tile ranges cycle their board value in place each frame; the renderer then draws the
    /// advanced frame via info[cell].sh.
    /// </summary>
    private void UpdateBlocks(Viewport vp)
    {
        int x0 = Math.Max(0, vp.OriginX / 16), y0 = Math.Max(0, vp.OriginY / 16);
        int x1 = Math.Min(Board.W, x0 + vp.W / 16 + 2), y1 = Math.Min(Board.H, y0 + vp.H / 16 + 2);
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                if ((_info.Entries[_board.CellIndex(x, y) % InfoTable.Count].Flags & GameFlags.MsgUpdate) != 0)
                    BlockUpdate(x, y);
    }

    /// <summary>msg_block(update): cycle an animated tile's board value within its range.</summary>
    private void BlockUpdate(int x, int y)
    {
        int bk = _board.CellIndex(x, y);
        int gc = Gamecount & 3;

        void Cycle(int lo, int hi) => _board.SetCell(x, y, bk + 1 > hi ? lo : bk + 1);

        if (bk >= Tiles.Lava1 && bk <= Tiles.Lava8 && gc == 0) Cycle(Tiles.Lava1, Tiles.Lava8);
        else if (bk >= Tiles.Spike1 && bk <= Tiles.Spike6 && gc == 1) Cycle(Tiles.Spike1, Tiles.Spike6);
        else if (bk >= Tiles.Comp1 && bk <= Tiles.Comp3 && gc == 0) Cycle(Tiles.Comp1, Tiles.Comp3);
        else if ((bk == Tiles.EyeCl || bk == Tiles.EyeOp) && XrRandom(200) == 0)
            _board.SetCell(x, y, bk == Tiles.EyeCl ? Tiles.EyeOp : Tiles.EyeCl);
        else if (bk >= Tiles.WaterT1 && bk <= Tiles.WaterT8 && (gc & 1) != 0) Cycle(Tiles.WaterT1, Tiles.WaterT8);
        else if (bk >= Tiles.WaterEnd1 && bk <= Tiles.WaterWav8 && (gc & 1) != 0)
        {
            int nb = bk + 1;
            if (nb == Tiles.WaterEnd8 + 1) nb = Tiles.WaterEnd1;
            else if (nb == Tiles.WaterMst8 + 1) nb = Tiles.WaterMst1;
            else if (nb == Tiles.WaterWav8 + 1) nb = Tiles.WaterWav1;
            _board.SetCell(x, y, nb);
        }
        else if (bk >= Tiles.Acid1 && bk <= Tiles.Acid8 && gc == 0) Cycle(Tiles.Acid1, Tiles.Acid8);
        else if (bk >= Tiles.Flow1 && bk <= Tiles.Flow3 && gc == 1) Cycle(Tiles.Flow1, Tiles.Flow3);
        else if (bk >= Tiles.Lever1 && bk <= Tiles.Lever7 && gc == 2) Cycle(Tiles.Lever1, Tiles.Lever7);
        else if (bk >= Tiles.Elec1 && bk <= Tiles.Elec9 && (gc & 1) != 0) Cycle(Tiles.Elec1, Tiles.Elec9);
        else if (bk >= Tiles.Qzap1 && bk <= Tiles.Qzap3 && (gc & 2) != 0) Cycle(Tiles.Qzap1, Tiles.Qzap3);
    }

    private void BuildScrnObjs(Viewport vp)
    {
        _numScrn = 0;
        _scrnObjs[_numScrn++] = 0; // player always first
        int sx = vp.OriginX - 96, ex = vp.OriginX + vp.W + 96;
        int sy = vp.OriginY - 48, ey = vp.OriginY + vp.H + 48;
        for (int n = 1; n < _objects.Count && _numScrn < MaxScrnObjs; n++)
        {
            var o = _objects[n];
            bool onScreen = o.X + o.Xl >= sx && o.X <= ex && o.Y + o.Yl >= sy && o.Y <= ey;
            if (onScreen || (KindDefs.Get(o.Kind).Flags & GameFlags.Always) != 0)
                _scrnObjs[_numScrn++] = n;
        }
    }

    /// <summary>
    /// Mutual touch handling (upd_objs): each on-screen object with f_msgtouch (the player and
    /// weapons) is tested against every other on-screen object; on overlap, msg_touch is dispatched
    /// both ways. This drives pickup collection (player touches pickup), weapons killing enemies,
    /// and enemies damaging the player.
    /// </summary>
    private void HandleTouch()
    {
        for (int i = 0; i < _numScrn; i++)
        {
            int n = _scrnObjs[i];
            var a = _objects[n];
            if ((KindDefs.Get(a.Kind).Flags & GameFlags.MsgTouch) == 0) continue;
            // Crouching hero (st_stand, xd==0, yd==3) tests only the lower part of its box: y+18.
            bool crouch = n == 0 && a.Kind == ObjectKind.Player && a.State == 0 && a.Xd == 0 && a.Yd == 3;
            for (int j = 0; j < _numScrn; j++)
            {
                int n2 = _scrnObjs[j];
                if (n2 == n) continue;
                var b = _objects[n2];
                if (crouch)
                {
                    if (b.X < a.X + a.Xl && a.X < b.X + b.Xl && b.Y < a.Y + a.Yl && a.Y + 18 < b.Y + b.Yl)
                    {
                        _kindMsg[a.Kind](n, Msg.Touch, n2);
                        _kindMsg[b.Kind](n2, Msg.Touch, n);
                    }
                    continue;
                }
                if (a.X < b.X + b.Xl && b.X < a.X + a.Xl && a.Y < b.Y + b.Yl && b.Y < a.Y + a.Yl)
                {
                    _kindMsg[a.Kind](n, Msg.Touch, n2);
                    _kindMsg[b.Kind](n2, Msg.Touch, n);
                }
            }
        }
    }

    // ---- Combat / helpers used by behaviours ----------------------------------------------

    public int KindFlags(int n) => KindDefs.Get(_objects[n].Kind).Flags;
    public bool HasFlag(int n, int flag) => (KindFlags(n) & flag) != 0;

    public int CountKind(int kind)
    {
        int c = 0;
        foreach (var o in _objects) if (o.Kind == kind) c++;
        return c;
    }

    /// <summary>onscreen(): is object n within the current gameplay viewport?</summary>
    public bool OnScreen(int n)
    {
        var o = _objects[n];
        var vp = _curVp;
        return o.X + o.Xl >= vp.OriginX && o.Y + o.Yl >= vp.OriginY
            && o.X <= vp.OriginX + vp.W && o.Y <= vp.OriginY + vp.H;
    }

    /// <summary>crawl(): step (dx,dy) only if it stays on floor and is unobstructed.</summary>
    public int Crawl(int n, int dx, int dy)
    {
        var o = _objects[n];
        if (StandFloor(n, dx, dy) != 0 && Cando(n, o.X + dx, o.Y + dy, GameFlags.PlayerThru) == GameFlags.PlayerThru)
        {
            MoveObj(n, o.X + dx, o.Y + dy);
            return 1;
        }
        return 0;
    }

    /// <summary>seekplayer(): unit direction from object n toward the player.</summary>
    public void SeekPlayer(int n, out int dx, out int dy)
    {
        var o = _objects[n];
        var p = _objects[0];
        dx = Math.Sign(p.X - o.X);
        dy = Math.Sign(p.Y - o.Y);
    }

    public int VectDist(int n1, int n2) =>
        Math.Abs(_objects[n1].X - _objects[n2].X) + Math.Abs(_objects[n1].Y - _objects[n2].Y);

    /// <summary>Nearest f_killable on-screen object to n within maxDist (for homing rocks); -1 if none.</summary>
    public int ClosestKillable(int n, int maxDist)
    {
        int best = -1, bestDist = maxDist;
        for (int i = 0; i < _numScrn; i++)
        {
            int o = _scrnObjs[i];
            if (o == 0 || (KindFlags(o) & GameFlags.Killable) == 0) continue;
            int d = VectDist(n, o);
            if (d < bestDist) { bestDist = d; best = o; }
        }
        return best;
    }

    /// <summary>Nearest on-screen ball (kind 46) to n whose counter exceeds c, within 96px; -1 if
    /// none. Drives the heavier balls' pull on the lighter ones (msg_ball flocking).</summary>
    public int ClosestBall(int n, int c)
    {
        int best = -1, bestDist = 96;
        for (int i = 0; i < _numScrn; i++)
        {
            int o = _scrnObjs[i];
            if (_objects[o].Kind == ObjectKind.Ball && _objects[o].Counter > c)
            {
                int d = VectDist(n, o);
                if (d < bestDist) { bestDist = d; best = o; }
            }
        }
        return best;
    }

    /// <summary>pointvect(): a vector of length len pointing from n2 toward n1, as (ax,ay).</summary>
    public void PointVect(int n1, int n2, out int ax, out int ay, int len)
    {
        int x = _objects[n1].X - _objects[n2].X;
        int y = _objects[n1].Y - _objects[n2].Y;
        if (x == 0) { ax = 0; ay = y != 0 ? len * Math.Sign(y) : 0; }
        else if (y == 0) { ax = len * Math.Sign(x); ay = 0; }
        else if (Math.Abs(x) > Math.Abs(y)) { ay = y * len / Math.Abs(x); ax = len * Math.Sign(x); }
        else { ax = x * len / Math.Abs(y); ay = len * Math.Sign(y); }
    }

    /// <summary>hitplayer(): the player takes a hit from object n (unless briefly safe/invincible).</summary>
    public void Hitplayer(int n, int flg)
    {
        var o = _objects[n];
        if (o.ZapHold == 0 && !IsInvincible(_objects[0].State))
        {
            POuch(1, flg != 0 ? 2 /*die_fish*/ : 0 /*die_ash*/);
            AddObj(ObjectKind.HitHero /*hithero*/, _objects[0].X, _objects[0].Y, 0, 0);
        }
        o.ZapHold = 3;
    }

    private const int DieBird = 1, DieFish = 2;

    /// <summary>p_ouch(): apply damage to the player; death -> st_die.</summary>
    public void POuch(int healthTake, int dieMode)
    {
        var p = _objects[0];
        if (p.Kind == ObjectKind.Tiny) return; // the map hero is never hurt
        if (p.Kind == ObjectKind.Player && IsInvincible(p.State)) return;
        if (p.Kind == ObjectKind.HeroBee) dieMode = DieBird;
        healthTake -= InvCount(Inv.Invin);
        if (healthTake <= 0) return;
        Pl.Health -= healthTake;
        Pl.Ouched = 4;
        if (Pl.Health <= 0)
        {
            Pl.Health = 0;
            p.Kind = ObjectKind.Player; // revert a transformed hero (herobee) before dying
            // Dying off a platform shortens the death throe (statecount 18 -> reenter next frame) so the
            // ride ends at once; otherwise the full death animation plays from 0.
            p.StateCount = p.State == 7 /*st_platform*/ ? 18 : 0;
            p.State = 5; // st_die
            p.SubState = dieMode;
            if (dieMode is DieBird or DieFish) Pl.Ouched = 0;
            if (dieMode == DieBird) p.Yd = 2;
            Explode1(p.X, p.Y, 10, 0);
        }
    }

    /// <summary>inv_xfm (X_OBJ.C): which inventory kinds are "transform" items — inv_hero(0, revert to
    /// human), inv_bird(5) and inv_fish(6). playerxfm clears all of them on a successful change.</summary>
    private static readonly int[] InvXfm = { 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    /// <summary>
    /// playerxfm(): transform the hero (object 0) in response to a transform token whose state/inv id
    /// is <paramref name="xfmTo"/> — inv_bird(5)→herobee, inv_fish(6)→heroswim, anything else
    /// (inv_hero 0)→back to the plain player. The hero resizes to the new kind; the change is committed
    /// only if the new body fits (first dropped to the old feet, else at the old origin). On success it
    /// resets the hero's motion, clears every transform inventory item, and bursts an explosion; on
    /// failure the old kind/size are restored. No-op if already that kind.
    /// </summary>
    public void PlayerXfm(int xfmTo)
    {
        var obj0 = _objects[0];
        int newKind = xfmTo switch { Inv.Bird => ObjectKind.HeroBee, Inv.Fish => ObjectKind.HeroSwim, _ => ObjectKind.Player };
        if (obj0.Kind == newKind) return;

        int oldKind = obj0.Kind, oldXl = obj0.Xl, oldYl = obj0.Yl;
        obj0.Kind = (byte)newKind;
        obj0.Xl = KindDefs.Get(newKind).Xl;
        obj0.Yl = KindDefs.Get(newKind).Yl;

        int newx = obj0.X & 0xfff8;
        int newy = obj0.Y + oldYl - obj0.Yl;
        bool fits = Cando(0, newx, newy, GameFlags.PlayerThru) != 0;
        if (!fits) { newy = obj0.Y; fits = Cando(0, newx, newy, GameFlags.PlayerThru) != 0; }

        if (fits)
        {
            AddInv(xfmTo);
            obj0.X = newx; obj0.Y = newy;
            obj0.State = 0; obj0.SubState = 0; obj0.Counter = 0; obj0.Xd = 0; obj0.Yd = 0;
            for (int k = 0; k < InvXfm.Length; k++)
                if (InvXfm[k] != 0) while (TakeInv(k)) { }
            Explode1(obj0.X, obj0.Y, 10, 0);
            Txt(GameStrings.InvGetMsg[xfmTo], 7, 0);
        }
        else
        {
            obj0.Kind = (byte)oldKind; obj0.Xl = oldXl; obj0.Yl = oldYl;
        }
    }

    /// <summary>
    /// touchbkgnd(): the player takes damage from hazardous tiles it overlaps (water drowns you,
    /// lava/acid burn, spikes/thorns/electricity hurt). Invincible states (entry/death) are immune.
    /// </summary>
    public void TouchBkgnd(int n)
    {
        var o = _objects[n];
        if (IsInvincible(o.State)) return;
        int startx = o.X / 16, starty = o.Y / 16;
        int endx = (o.X + o.Xl + 15) / 16, endy = (o.Y + o.Yl + 15) / 16;
        for (int y = starty; y < endy; y++)
            for (int x = startx; x < endx; x++)
            {
                if ((_info.Entries[BoardCell(x, y) % InfoTable.Count].Flags & GameFlags.MsgTouch) != 0)
                    BlockTouch(x, y);
                else if (y + 1 < Board.H && BoardCell(x, y + 1) >= Tiles.Crum1 && BoardCell(x, y + 1) <= Tiles.CrumLast
                    && (Gamecount & 3) == 2)
                {
                    // Standing on a crumble block: it sheds a fragment and advances one frame, then vanishes.
                    int frag = AddObj(ObjectKind.FragExpl, o.X + XrRandom(10), o.Y + 44, 0, 2);
                    _objects[frag].State = -1;
                    SetBoard(x, y + 1, BoardCell(x, y + 1) + 1);
                    if (BoardCell(x, y + 1) > Tiles.CrumLast) SetBoard(x, y + 1, 0);
                }
            }
    }

    /// <summary>msg_block(touch): hazard tiles hurting/killing the player (heroswim not yet ported).</summary>
    private void BlockTouch(int x, int y)
    {
        int bk = BoardCell(x, y);
        if (bk >= Tiles.Lava1 && bk <= Tiles.Lavab) { POuch(5, 0); AddObj(ObjectKind.HitHero, _objects[0].X, _objects[0].Y, 0, 0); }
        else if (bk == Tiles.Thornt || bk == Tiles.Thornb || bk == Tiles.Thrnt || bk == Tiles.Thrnb) Hitplayer(0, 0);
        else if (bk >= Tiles.WaterEnd1 && bk <= Tiles.WaterMst8) POuch(5, 0); // deep water drowns
        else if (bk >= Tiles.WaterT1 && bk <= Tiles.WaterT8) POuch(5, 0);
        else if (bk >= Tiles.Acid1 && bk <= Tiles.Acidb) { POuch(5, 0); AddObj(ObjectKind.HitHero, _objects[0].X, _objects[0].Y, 0, 0); }
        else if (bk >= Tiles.Spike1 && bk <= Tiles.Spike6) POuch(5, 0);
        else if (bk >= Tiles.Qzap1 && bk <= Tiles.Qzap3) Hitplayer(0, 0);
    }

    /// <summary>explode1(): spawn a burst of fragment objects (flg=1 pebbles, else coloured chunks).</summary>
    public void Explode1(int x, int y, int num, int flg)
    {
        for (int c = 0; c < num; c++)
        {
            int i = AddObj(ObjectKind.FragExpl /*fragexpl*/, x, y, XrRandom(7) - 3, XrRandom(11) - 8);
            _objects[i].State = flg != 0 ? -1 : XrRandom(5);
        }
    }

    /// <summary>explode2(): a fire explosion centred on object n.</summary>
    public void Explode2(int n)
    {
        var o = _objects[n];
        var fe = KindDefs.Get(ObjectKind.FireExpl);
        AddObj(ObjectKind.FireExpl /*fireexpl*/, o.X + (o.Xl - fe.Xl) / 2, o.Y + o.Yl - fe.Yl, 0, 0);
    }

    /// <summary>Whether the board cell containing pixel (x,y) is a breakwall.</summary>
    public bool IsBreakWallAt(int x, int y) =>
        x >= 0 && y >= 0 && x / 16 < Board.W && y / 16 < Board.H && BoardCell(x / 16, y / 16) == Tiles.BreakWall;

    /// <summary>trybreakwall(): break breakwall cells touched by n's box at (x,y); eye cells open
    /// to noeye. Returns the number of breakwall cells broken.</summary>
    public int TryBreakWall(int n, int x, int y)
    {
        var o = _objects[n];
        int f = 0;
        for (int xc = x / 16; xc <= (x + o.Xl) / 16; xc++)
        {
            for (int yc = y / 16; yc <= (y + o.Yl) / 16; yc++)
            {
                if ((uint)xc >= Board.W || (uint)yc >= Board.H) continue;
                int cell = BoardCell(xc, yc);
                if (cell == Tiles.BreakWall)
                {
                    SetBoard(xc, yc, 0); Pl.Score += 10;
                    if (o.Kind == ObjectKind.Fireball) AddObj(ObjectKind.FireExpl, xc * 16 - 16, yc * 16 - 16, 0, 0);
                    else KillObj(n);
                    if (f++ == 0) Explode1(xc * 16, yc * 16, 5, 1);
                }
                else if (cell == Tiles.EyeOp)
                {
                    AddObj(ObjectKind.Flash, xc * 16 - 1, yc * 16 - 4, 0, 0);
                    SetBoard(xc, yc, Tiles.NoEye);
                    AddScore(100, xc * 16, yc * 16); KillObj(n);
                }
            }
        }
        return f;
    }

    // Current player input (dx1/dy1), set each frame — used by switches etc.
    public int InputDx;
    public int InputDy;
    // The live controls for this frame — behaviours that read the jump button (platform ride) need
    // the edge-triggered Fire2/Fire2Off, not just the Dx1/Dy1 copies above.
    public Controls? CurControls;

    public int BoardCell(int x, int y) => _board.CellIndex(x, y);
    public void SetBoard(int x, int y, int value) => _board.SetCell(x, y, value);
    public int InfoSh(int index) => _info.Entries[((uint)index < InfoTable.Count ? index : 0)].Sh;
    public int InfoFlags(int index) => _info.Entries[((uint)index < InfoTable.Count ? index : 0)].Flags;

    /// <summary>setorigin(): snap the gameplay camera to centre on the player (used by transports).</summary>
    public void CenterCamera() { if (_curVp is not null) Camera.Center(_curVp, _objects[0]); }

    /// <summary>calc_scroll(): follow the hero (object 0) with the gameplay camera. Used by the form
    /// handlers (msg_heroswim/msg_herobee), which do their own scrolling like msg_player does.</summary>
    public void CalcScroll(int peeky) { if (_curVp is not null) Camera.Follow(_curVp, _objects[0], peeky); }

    /// <summary>
    /// The 16-fruit rule inside drawstats() (XARGON.C:197): with 16 fruit and fewer than 5 hearts, reset the
    /// counter and add a heart (blue hero flash unless invincible). The C runs it only when the status bar
    /// is redrawn (statmodflg, set by every health/fruit change); the port redraws every frame, and the
    /// rule is self-resetting, so calling it once per frame before Hud.Draw is equivalent. At 5 hearts
    /// the counter stays 16 until damage lowers health. (The snd_getheart sound is not ported yet.)
    /// </summary>
    public void UpdateFruitHeart()
    {
        if (Pl.Fruit >= 16 && Pl.Health < 5)
        {
            Pl.Fruit = 0; Pl.Health++;
            if (InvCount(Inv.Invin) == 0) Pl.Ouched = -4;
        }
    }

    /// <summary>
    /// drawstats() at the head of refresh(): restore the hero palette when the hurt/heal flash counter
    /// reaches +-1 and clear it. Called once per drawn frame for any hero kind (player, herobee,
    /// heroswim, or the map's tiny hero) — the C runs it in refresh regardless of the hero's form.
    /// </summary>
    public void HeroColorFlashPre(VgaScreen screen)
    {
        Effects.OuchedColors(screen, Pl.Ouched);
        Pl.Ouched = Player.OuchedBeforeDraw(Pl.Ouched);
    }

    /// <summary>The tail of refresh(): flash the hero colours red (hurt) or blue (healed/invincible)
    /// while |ouched|&gt;=2 and step the counter back toward 0. Hero-kind independent, as in the C.</summary>
    public void HeroColorFlashPost(VgaScreen screen)
    {
        if (Pl.Ouched is >= 2 or <= -2) Effects.OuchedColors(screen, Pl.Ouched);
        Pl.Ouched = Player.OuchedAfterDraw(Pl.Ouched);
    }

    /// <summary>sendtrig(): send a trigger message to every f_trigger object with matching counter.</summary>
    public void SendTrig(int cnt, int msg, int fromObj)
    {
        for (int i = 0; i < _objects.Count; i++)
            if ((KindDefs.Get(_objects[i].Kind).Flags & GameFlags.Trigger) != 0 && _objects[i].Counter == cnt)
                _kindMsg[_objects[i].Kind](i, msg, fromObj);
    }

    private const int ObjCheckpt = ObjectKind.Checkpt;
    private const int StBegin = 4;

    /// <summary>
    /// Position the player (object 0) at a checkpoint, as p_reenter does: the checkpoint whose
    /// counter matches <paramref name="level"/> (else the first checkpoint), at (x&amp;~7, y-24),
    /// in the st_begin entry state. No-op if the board has no checkpoint.
    /// </summary>
    public void ReenterPlayer(int level)
    {
        int n = FindCheckpt(level);
        if (n > 0)
        {
            var cp = _objects[n];
            var p = _objects[0];
            p.X = cp.X & 0xfff8;
            // desty-=24 for the platformer hero, but not the top-down map hero (obj_tiny).
            p.Y = cp.Y - (p.Kind == ObjectKind.Tiny ? 0 : 24);
            p.State = StBegin;
            p.StateCount = 0;
            p.Xd = 0;
            p.Yd = 0;
            p.Info1 = 1; // facing right, so firing works immediately
        }
        // no matching checkpoint: leave the hero at its board position (e.g. the map start)
        Pl.Health = 5; // p_reenter restores full health on every reenter
    }

    /// <summary>findcheckpt(): index of the checkpoint whose counter matches <paramref name="level"/>
    /// (or any checkpoint when level&lt;0); 0 (the player) if none.</summary>
    public int FindCheckpt(int level)
    {
        for (int i = 0; i < _objects.Count; i++)
            if (_objects[i].Kind == ObjCheckpt && (level < 0 || _objects[i].Counter == level))
                return i;
        return 0;
    }

    public static void SetObjSize(Obj o)
    {
        var kd = KindDefs.Get(o.Kind);
        o.Xl = kd.Xl;
        o.Yl = kd.Yl;
        int slen = o.Inside?.Length ?? 0;
        if (o.Kind == ObjectKind.Text6) o.Xl = slen * 6;
        else if (o.Kind == ObjectKind.Text8) o.Xl = slen * 8;
        else if (o.Kind == ObjectKind.Score) o.Xl = (2 + o.State.ToString().Length) * kd.Xl;
    }

    // ---- Rendering ------------------------------------------------------------------------

    /// <summary>Test seam: when set, the index of every object drawn is appended in draw order.</summary>
    internal List<int>? DrawTrace;

    private enum DrawPass { Back, Normal, Front }

    public void DrawBack(VgaScreen screen, Viewport vp) => DrawLayer(screen, vp, DrawPass.Back);

    /// <summary>
    /// The rest of refresh(): the normal layer in REVERSE index order (so lower indexes, ending with the
    /// hero at object 0, are drawn on top), then the front layer ascending. <paramref name="drawHero"/> runs
    /// at object 0's slot, i.e. after every other normal object and before the front objects.
    /// </summary>
    public void DrawFore(VgaScreen screen, Viewport vp, Action? drawHero = null)
    {
        DrawLayer(screen, vp, DrawPass.Normal);
        if (drawHero is not null)
        {
            DrawTrace?.Add(0);
            drawHero();
        }
        DrawLayer(screen, vp, DrawPass.Front);
    }

    private void DrawLayer(VgaScreen screen, Viewport vp, DrawPass pass)
    {
        _drawScreen = screen;
        _drawVp = vp;
        bool reverse = pass == DrawPass.Normal;
        for (int i = 1; i < _objects.Count; i++) // object 0 (player) is drawn by the caller
        {
            int n = reverse ? _objects.Count - i : i;
            var o = _objects[n];
            var kd = KindDefs.Get(o.Kind);
            // Text objects (kinds 6/7) carry the Inside flag, and Score (27) has no shape table, so HasSprite is
            // false for them, but they draw through their handler (the C's refresh has no sprite gate).
            if (!kd.HasSprite && o.Kind != ObjectKind.Text6 && o.Kind != ObjectKind.Text8
                && o.Kind != ObjectKind.Score) continue;
            bool want = pass switch
            {
                DrawPass.Back => kd.IsBack,
                DrawPass.Front => kd.IsFront,
                _ => !kd.IsBack && !kd.IsFront,
            };
            if (!want) continue;

            DrawTrace?.Add(n);
            if (_hasBehavior[o.Kind]) _kindMsg[o.Kind](n, Msg.Draw, 0); // handler draws via DrawSprite
            else _shapes.DrawShape(screen, vp, kd.ShapeTable * 256 + kd.DrawOffset, o.X, o.Y);
        }
    }

    /// <summary>Draw a shape into the current draw context — used by object msg_draw handlers.</summary>
    public void DrawSprite(int sh, int x, int y) => _shapes.DrawShape(_drawScreen, _drawVp, sh, x, y);

    /// <summary>Draw a font-flagged shape under fontcolor(hi, back) into the current draw context; no-op without a renderer.</summary>
    public void DrawFontSprite(int sh, int x, int y, int hi, int back) => Text?.DrawFontShape(_drawScreen, _drawVp, sh, x, y, hi, back);

    /// <summary>Draw bitmap text into the current draw context (fontcolor + wprint on gamevp); no-op without a renderer.</summary>
    public void DrawText(int font, int x, int y, int hi, int back, string? text)
    {
        if (Text is null || string.IsNullOrEmpty(text)) return;
        Text.Draw(_drawScreen, _drawVp, x, y, font, hi, back, text);
    }

    // ---- Collision (X_OBJMAN.C) -----------------------------------------------------------

    /// <summary>Block flags at a cell; out-of-bounds is solid (grants no flags).</summary>
    private int CellFlags(int x, int y)
    {
        if ((uint)x >= Board.W || (uint)y >= Board.H) return 0;
        return _info.Entries[_board.CellIndex(x, y) % InfoTable.Count].Flags;
    }

    /// <summary>
    /// cando(): AND together (blockflags | flagor) &amp; ourflags over the object's footprint at
    /// (newx,newy). Above the object's feet (splity) f_notstair is forced on so stairs don't block
    /// from the side; below it the raw flags apply. Result == ourflags means the move is allowed.
    /// </summary>
    public int Cando(int n, int newx, int newy, int ourflags)
    {
        var o = _objects[n];
        int startx = newx / 16, starty = newy / 16;
        int endx = (newx + o.Xl + 15) / 16, endy = (newy + o.Yl + 15) / 16;
        int splity = (o.Y + KindDefs.Get(o.Kind).Yl + 15) / 16;

        int flagor = GameFlags.NotStair;
        int result = 0xffff;
        for (int y = starty; y < endy; y++)
        {
            if (y >= splity) flagor = 0;
            for (int x = startx; x < endx; x++)
                result &= (CellFlags(x, y) | flagor) & ourflags;
        }
        return result;
    }

    /// <summary>objdo(): like Cando but without the stair (splity) special case.</summary>
    public int ObjDo(int n, int newx, int newy, int ourflags)
    {
        var o = _objects[n];
        int startx = newx / 16, starty = newy / 16;
        int endx = (newx + o.Xl + 15) / 16, endy = (newy + o.Yl + 15) / 16;
        int result = 0xffff;
        for (int y = starty; y < endy; y++)
            for (int x = startx; x < endx; x++)
                result &= CellFlags(x, y) & ourflags;
        return result;
    }

    /// <summary>fishdo(): move the object to (newx,newy) only if the destination is entirely water;
    /// returns 1 if it moved, 0 if blocked. Used by the water enemies to stay submerged.</summary>
    public int Fishdo(int n, int newx, int newy)
    {
        int want = GameFlags.PlayerThru | GameFlags.Water;
        if (ObjDo(n, newx, newy, want) == want) { MoveObj(n, newx, newy); return 1; }
        return 0;
    }

    public void MoveObj(int n, int newx, int newy)
    {
        var o = _objects[n];
        newy = Math.Clamp(newy, 0, Board.H * 16 - 16 - o.Yl);
        newx = Math.Clamp(newx, 0, Board.W * 16 - 16 - o.Xl);
        o.X = newx;
        o.Y = newy;
    }

    /// <summary>trymove(): 0=blocked, 1=full, 2=y-only, 4=x-only.</summary>
    public int TryMove(int n, int newx, int newy)
    {
        var o = _objects[n];
        int ourflags = GameFlags.PlayerThru;
        if (newy > o.Y) ourflags |= GameFlags.NotStair;

        if (Cando(n, newx, newy, ourflags) == ourflags) { MoveObj(n, newx, newy); return 1; }
        if (Cando(n, o.X, newy, ourflags) == ourflags) { MoveObj(n, o.X, newy); return 2; }
        if (Cando(n, newx, o.Y, ourflags) == ourflags) { MoveObj(n, newx, o.Y); return 4; }
        return 0;
    }

    public int JustMove(int n, int newx, int newy)
    {
        if (Cando(n, newx, newy, GameFlags.PlayerThru) != 0) { MoveObj(n, newx, newy); return 1; }
        return 0;
    }

    /// <summary>trymovey(): move allowing stairs to block; drops xd to 0 if fully blocked.</summary>
    public int TryMovey(int n, int newx, int newy)
    {
        int flags = GameFlags.PlayerThru | GameFlags.NotStair;
        if (Cando(n, newx, newy, flags) == flags) { MoveObj(n, newx, newy); return 1; }
        var o = _objects[n];
        if (Cando(n, o.X, newy, flags) == flags) { MoveObj(n, o.X, newy); return 1; }
        o.Xd = 0;
        return 0;
    }

    /// <summary>standfloor(): 1 if the object would be resting on solid floor after (dx,dy).</summary>
    public int StandFloor(int n, int dx, int dy)
    {
        var o = _objects[n];
        int newx = o.X + dx, newy = o.Y + dy;
        if (((newy + o.Yl) & 15) != 0) return 0;
        newy = (newy + o.Yl - 1) / 16 + 1;
        int startx = newx / 16, endx = (newx + o.Xl + 15) / 16;
        int mask = GameFlags.PlayerThru | GameFlags.NotStair;
        for (int xc = startx; xc < endx; xc++)
            if ((CellFlags(xc, newy) & mask) == mask) return 0;
        return 1;
    }
}
