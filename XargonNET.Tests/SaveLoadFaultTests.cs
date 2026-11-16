using Xunit;
using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>Task 10 fix round: a loaded save is not trusted per field (fault isolation) and the load swap is atomic.</summary>
public sealed class SaveLoadFaultTests
{
    private readonly AssetSet _set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);

    private static Board MapWith(params Obj[] extra)
    {
        var b = new Board();
        b.Objects.Add(new Obj { Kind = ObjectKind.Tiny, X = 100, Y = 100 });
        b.Objects.Add(new Obj { Kind = ObjectKind.Checkpt, Counter = 0, State = 5, X = 16, Y = 16 });
        b.Objects.AddRange(extra);
        b.Player = PlayerState.NewGame();
        return b;
    }

    private static Obj BadBall(int x) => new() { Kind = ObjectKind.Ball, X = x, Y = 60, Counter = 99, Info1 = 0 };
    private static Obj GoodKey(int x) => new() { Kind = ObjectKind.Key, X = x, Y = 60, Counter = 3 };

    private (ObjectManager om, VgaScreen screen, ShapeManager shapes) World(Board b, bool isolate)
    {
        var screen = new VgaScreen();
        _set.LoadPalette(screen);
        var shapes = new ShapeManager(_set.Graphics);
        var om = new ObjectManager(b, shapes, _set.Info, screen, new PlayerState { Health = 5 }, 1) { IsolateHandlerFaults = isolate };
        _ = new Behaviors(om);
        return (om, screen, shapes);
    }

    [Fact]
    public void WithoutIsolationAHandlerFaultStillThrows()
    {
        var b = MapWith(BadBall(110));
        var (om, _, _) = World(b, isolate: false);
        Assert.False(om.IsolateHandlerFaults);
        Assert.ThrowsAny<IndexOutOfRangeException>(() => om.UpdateObjects(TestWorld.Vp));
    }

    [Fact]
    public void IsolationKillsTheFaultingObjectAndOthersStillUpdate()
    {
        var b = MapWith(BadBall(110), GoodKey(50));
        var (om, _, _) = World(b, isolate: true);
        om.Gamecount++;
        var ex = Record.Exception(() => om.UpdateObjects(TestWorld.Vp));
        Assert.Null(ex);
        Assert.DoesNotContain(b.Objects, o => o.Kind == ObjectKind.Ball); // killed, then purged at the end of the update
        var key = b.Objects.Single(o => o.Kind == ObjectKind.Key);
        Assert.Equal(4, key.Counter);                          // the key Update ran (3 -> 4)
        om.UpdateObjects(TestWorld.Vp);                        // and the world keeps running (killed object purged)
        Assert.Equal(5, key.Counter);
    }

    [Fact]
    public void IsolationAlsoCoversTheDrawPass()
    {
        // Key.Draw indexes the sprite table by State; with an absurd State it either draws nothing or faults, and under
        // isolation neither may escape.
        var b = MapWith(new Obj { Kind = ObjectKind.Key, X = 60, Y = 60, State = 30000 }, GoodKey(80));
        var (om, screen, _) = World(b, isolate: true);
        var ex = Record.Exception(() => { om.DrawBack(screen, TestWorld.Vp); om.DrawFore(screen, TestWorld.Vp); });
        Assert.Null(ex);
    }

    [Fact]
    public void IsolationCoversTouchDispatchAndTriggers()
    {
        // A key with State 200 touched by the hero: Inv.Key(200)/InvKeyMsg[200] go out of range inside the Touch handler.
        var plain = MapWith(new Obj { Kind = ObjectKind.Key, X = 100, Y = 100, State = 200 });
        plain.Objects[0].Xl = 16; plain.Objects[0].Yl = 16;
        var (om0, _, _) = World(plain, isolate: false);
        om0.Gamecount++;
        Assert.ThrowsAny<ArgumentException>(() => om0.UpdateObjects(TestWorld.Vp)); // proves the fault is real
        var b = MapWith(new Obj { Kind = ObjectKind.Key, X = 100, Y = 100, State = 200 });
        b.Objects[0].Xl = 16; b.Objects[0].Yl = 16;
        var (om, _, _) = World(b, isolate: true);
        om.Gamecount++;
        var ex = Record.Exception(() => om.UpdateObjects(TestWorld.Vp));
        Assert.Null(ex);
        Assert.DoesNotContain(b.Objects, o => o.Kind == ObjectKind.Key);
        Assert.Equal(ObjectKind.Tiny, b.Objects[0].Kind);     // the hero object is never killed
    }

    [Fact]
    public void HeroHandlerFaultResetsTheHeroInsteadOfKillingIt()
    {
        var b = MapWith();
        var (om, _, _) = World(b, isolate: true);
        om.Register(ObjectKind.Tiny, (n, msg, z) => throw new IndexOutOfRangeException());
        b.Objects[0].State = 7; b.Objects[0].Xd = 5; b.Objects[0].Yd = -3; b.Objects[0].StateCount = 9;
        int r = om.Dispatch(0, Msg.Touch, 1);
        Assert.Equal(0, r);
        var h = b.Objects[0];
        Assert.Equal(ObjectKind.Tiny, h.Kind);
        Assert.Equal((0, 0, 0, 0), (h.State, h.StateCount, h.Xd, h.Yd));
    }

    [Fact]
    public void NonFaultExceptionsAreNotSwallowed()
    {
        var b = MapWith();
        var (om, _, _) = World(b, isolate: true);
        om.Register(ObjectKind.Ball, (n, msg, z) => throw new OutOfMemoryException());
        b.Objects.Add(new Obj { Kind = ObjectKind.Ball, X = 110, Y = 60 });
        Assert.Throws<OutOfMemoryException>(() => om.Dispatch(2, Msg.Update, 0));
    }

    // ---- LoadSaved: isolation inheritance and atomic swap -------------------------------------

    private LevelSession Session(PlayerState pl, VgaScreen screen) =>
        new(n => _set.LoadBoard(n.ToUpperInvariant()), new ShapeManager(_set.Graphics), _set.Info, screen,
            TestWorld.Vp, pl, episode: 1);

    [Fact]
    public void LoadedWorldsAndLaterWorldsOfTheSessionAreIsolatedFreshSessionsAreNot()
    {
        var screen = new VgaScreen(); _set.LoadPalette(screen);
        var pl = PlayerState.NewGame(); pl.Level = LevelSession.MapLevel;
        var s = Session(pl, screen);
        var fresh = s.Load("MAP");
        Assert.False(fresh.Objects.IsolateHandlerFaults);
        Assert.False(s.IsolateHandlerFaults);

        var saved = _set.LoadBoard("MAP");
        var loaded = s.LoadSaved(saved, saved.Player);
        Assert.True(loaded.Objects.IsolateHandlerFaults);
        Assert.True(s.IsolateHandlerFaults);

        pl.Level = 1;
        var level = s.EnterLevel("BOARD_01");        // a world created later by a transition inherits it
        Assert.True(level.Objects.IsolateHandlerFaults);
        Assert.True(s.ExitToMap().Objects.IsolateHandlerFaults);

        var other = Session(PlayerState.NewGame(), screen); // a new game session starts clean
        Assert.False(other.Load("MAP").Objects.IsolateHandlerFaults);
    }

    [Fact]
    public void LoadSavedThatFailsToBuildLeavesPlayerStashAndWorldUntouched()
    {
        var screen = new VgaScreen(); _set.LoadPalette(screen);
        var pl = PlayerState.NewGame(); pl.Level = LevelSession.MapLevel; pl.Score = 77;
        var s = Session(pl, screen);
        var map = s.Load("MAP");
        pl.Level = 1;
        s.EnterLevel("BOARD_01");
        Assert.True(s.InLevel);
        byte[] plBefore = BoardWriter.Write(MapWith(), pl);

        var hostile = MapWith();
        hostile.Objects.Add(null!);                  // makes ObjectManager construction throw
        var incoming = PlayerState.NewGame(); incoming.Score = 12345;
        Assert.ThrowsAny<Exception>(() => s.LoadSaved(hostile, incoming));

        Assert.True(s.InLevel);                      // stash kept
        Assert.False(s.IsolateHandlerFaults);        // no half-applied flag
        Assert.Equal(plBefore, BoardWriter.Write(MapWith(), pl)); // player state unchanged
        Assert.Equal(77u, pl.Score);
        // the session still works: leaving the level returns the ORIGINAL stashed map
        var back = s.ExitToMap();
        Assert.Equal(map.Board.Objects.Count, back.Board.Objects.Count);
    }
}
