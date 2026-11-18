using Xunit;
using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>The endings flow end to end: object update -> Esc continuation -> GameFlow.PostUpdate (the GameApp order).</summary>
public sealed class EndingsFlowTests
{
    private sealed class Dialogs : IPopupHost
    {
        public readonly List<(int, bool)> Shown = new();
        public void TextMessage(int n, PopupStyle style) { }
        public void DialogMessage(int n, bool eagle) => Shown.Add((n, eagle));
    }

    private static int Reactor(ObjectManager w, int state, int counter)
    {
        int r = w.AddObj(ObjectKind.Front, 100, 60, 0, 0);
        var o = w.Obj(r);
        o.State = state; o.Info1 = 40; o.Counter = counter;
        return r;
    }

    /// <summary>Run real update frames until an Esc request appears; returns its continuation and the frame count.</summary>
    private static (Action cont, int frames) UpdateUntilEsc(ObjectManager w, int max = 200)
    {
        for (int f = 1; f <= max; f++)
        {
            w.Gamecount++;
            w.UpdateObjects(TestWorld.Vp);
            if (w.TakeEscWait() is { } k) return (k, f);
        }
        throw new Xunit.Sdk.XunitException("no Esc request within " + max + " frames");
    }

    [Fact]
    public void BossWinFlowsToGameWonThroughPostUpdate()
    {
        var w = TestWorld.Load(3, "BOARD_01");
        int b = w.AddObj(ObjectKind.Xargon, 100, 40, 0, 0);
        w.Obj(b).SubState = 50;

        var (cont, frames) = UpdateUntilEsc(w);
        Assert.Equal(60, frames);
        Assert.False(w.GameWon); // nothing wins before the player presses Esc

        int died = 0, transitions = 0;
        // The loop polls PostUpdate each tick; before the continuation runs it must report None.
        Assert.Equal(GameOver.None, GameFlow.PostUpdate(w, false, () => died++, () => transitions++));

        cont();
        Assert.Equal(GameOver.Won, GameFlow.PostUpdate(w, false, () => died++, () => transitions++));
        Assert.Equal(GameOver.None, GameFlow.PostUpdate(w, false, () => died++, () => transitions++)); // exactly once
        Assert.Equal((0, 0), (died, transitions));
    }

    [Fact]
    public void BossWinWinsEvenIfHeroDiedSameTick()
    {
        var w = TestWorld.Load(3, "BOARD_01");
        int b = w.AddObj(ObjectKind.Xargon, 100, 40, 0, 0);
        w.Obj(b).SubState = 50;
        var (cont, _) = UpdateUntilEsc(w);
        cont();
        int died = 0;
        Assert.Equal(GameOver.Won, GameFlow.PostUpdate(w, true, () => died++, () => { }));
        Assert.Equal(0, died);
    }

    [Fact]
    public void ReactorVolumeOneTransitionEntersBoard33()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        Reactor(w, 14, 0);
        int cp = w.AddObj(ObjectKind.Checkpt, 200, 120, 0, 0);
        w.Obj(cp).Counter = 33;
        var popups = new Dialogs();
        w.Popups = popups;

        var (cont, frames) = UpdateUntilEsc(w);
        Assert.Equal(60, frames);
        Assert.Null(w.NewLevel);
        Assert.Equal(GameOver.None, GameFlow.PostUpdate(w, false, () => Assert.Fail("die"), () => Assert.Fail("transition early")));

        cont();
        var seen = new List<string?>();
        int died = 0;
        var over = GameFlow.PostUpdate(w, false, () => died++, () => seen.Add(w.NewLevel));
        Assert.Equal(GameOver.None, over);
        Assert.Equal(new string?[] { "board_33" }, seen);
        Assert.Equal(0, died);
        Assert.False(w.GameWon);
        Assert.Equal(33, w.Pl.Level);
        Assert.Equal(new[] { (22, false) }, popups.Shown);
    }

    // ---- palette ----

    private static LevelSession Session(VgaScreen screen, int episode, out AssetSet set)
    {
        var s = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        set = s;
        s.LoadPalette(screen);
        return new LevelSession(n => s.LoadBoard(n.ToUpperInvariant()), new ShapeManager(s.Graphics), s.Info,
            screen, new Viewport(0, 2, VgaScreen.Width, 158), PlayerState.NewGame(), episode: episode);
    }

    public static IEnumerable<object[]> DimModes() => new[] { new object[] { "dim" }, new object[] { "undim" } };

    [Theory]
    [MemberData(nameof(DimModes))]
    public void NewBoardRestoresFullPalette(string mode)
    {
        var screen = new VgaScreen();
        var session = Session(screen, 1, out _);
        session.Load("BOARD_01");
        if (mode == "dim") screen.Dim(); else screen.Undim();

        session.Load("BOARD_02");

        var control = new VgaScreen();
        var cs = Session(control, 1, out _);
        cs.Load("BOARD_02");
        Assert.Equal(control.Palette, screen.Palette);
    }

    [Fact]
    public void DeferredLevelEntryKeepsOldPaletteUntilAppliedThenMatchesFreshBoard()
    {
        var screen = new VgaScreen();
        var session = Session(screen, 1, out _);
        session.Load("MAP");
        screen.Dim();
        var dimmed = (uint[])screen.Palette.Clone();

        session.EnterLevel("BOARD_02", deferPalette: true);
        Assert.Equal(dimmed, screen.Palette); // the banner frame must not recolour yet
        session.ApplyPendingPalette();

        var control = new VgaScreen();
        Session(control, 1, out _).Load("BOARD_02");
        Assert.Equal(control.Palette, screen.Palette);
    }

    [Fact]
    public void DimActuallyChangesPaletteSoTheRestoreTestIsMeaningful()
    {
        var screen = new VgaScreen();
        Session(screen, 1, out _).Load("BOARD_01");
        var before = (uint[])screen.Palette.Clone();
        screen.Dim();
        Assert.NotEqual(before, screen.Palette);
    }

    // ---- session-level pending request ----

    [Fact]
    public void DeathReloadWithPendingEscRequestLeavesNewWorldClean()
    {
        var screen = new VgaScreen();
        var session = Session(screen, 1, out _);
        var map = session.Load("MAP");
        var lvl = session.EnterLevel("BOARD_01");
        Assert.True(lvl.Objects.RequestEscWait(() => Assert.Fail("stale continuation ran")));
        // Force a reloading checkpoint so Die() builds a new world.
        int n = lvl.Objects.FindCheckpt(lvl.Objects.Pl.Level);
        Assert.True(n > 0);
        lvl.Objects.Obj(n).State = 1;

        var after = session.Die();
        Assert.NotSame(lvl.Objects, after.Objects);
        Assert.Null(after.Objects.TakeEscWait());
        Assert.False(after.Objects.GameWon);
    }

    [Fact]
    public void LoadSavedWithPendingEscRequestLeavesNewWorldClean()
    {
        var screen = new VgaScreen();
        var session = Session(screen, 1, out var set);
        var map = session.Load("MAP");
        Assert.True(map.Objects.RequestEscWait(() => Assert.Fail("stale continuation ran")));
        map.Objects.GameWon = true;

        var loaded = session.LoadSaved(set.LoadBoard("MAP"), PlayerState.NewGame());
        Assert.NotSame(map.Objects, loaded.Objects);
        Assert.Null(loaded.Objects.TakeEscWait());
        Assert.False(loaded.Objects.GameWon);
    }
}
