using Xunit;
using XargonNET.Assets;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>msg_leech (kind 54, x_obj2.c:199). No shipped board contains a leech, so everything runs
/// on synthetic objects in an empty arena with a solid floor row.</summary>
public class LeechTests
{
    private const int Solid = 148;
    private const int FloorY = 7 * 16;          // top of the floor row (cell row 7)
    private const int LeechY = FloorY - 23;     // leech Yl = 23: feet exactly on the floor
    private const int LeechX = 100;

    /// <summary>Arena: air in cells (0..14, 2..9), solid floor on row 7 across cells floorFrom..floorTo.</summary>
    private static ObjectManager Arena(int floorFrom = 4, int floorTo = 14, int episode = 1)
    {
        var w = TestWorld.Load(episode, "BOARD_01");
        for (int y = 2; y < 10; y++)
            for (int x = 0; x < 15; x++) w.SetBoard(x, y, 0);
        for (int x = floorFrom; x <= floorTo; x++) w.SetBoard(x, 7, Solid);
        w.Obj(0).X = 20; w.Obj(0).Y = 20; // player far up-left, out of the way
        return w;
    }

    private static int NewLeech(ObjectManager w, int x = LeechX) => w.AddObj(ObjectKind.Leech, x, LeechY, 0, 0);

    [Fact]
    public void KindIsRegisteredWithCDefinition()
    {
        Assert.Equal(54, ObjectKind.Leech);
        var w = Arena();
        Assert.True(w.HasBehavior(ObjectKind.Leech));
        var d = KindDefs.Get(ObjectKind.Leech);
        Assert.Equal(44, d.Xl);
        Assert.Equal(23, d.Yl);
        Assert.Equal(300, d.Score);
        Assert.Equal(GameFlags.Fireball, d.Flags);
    }

    [Fact]
    public void OddCounterTickDoesNothingAndReturnsZero()
    {
        var w = Arena();
        int n = NewLeech(w);
        var p = w.Obj(n);
        p.Counter = 0; p.Xd = 4; p.SubState = 4; p.StateCount = 5;
        Assert.Equal(0, w.Dispatch(n, Msg.Update, 0));
        Assert.Equal(1, p.Counter);
        Assert.Equal(5, p.StateCount);
        Assert.Equal(LeechX, p.X);
    }

    [Fact]
    public void CounterWrapsModEight()
    {
        var w = Arena();
        int n = NewLeech(w);
        var p = w.Obj(n);
        p.Counter = 7;
        w.Dispatch(n, Msg.Update, 0);
        Assert.Equal(0, p.Counter);
        Assert.True(p.StateCount == -1 || p.StateCount == 2); // even tick ran: decrement, or pause
    }

    /// <summary>Runs one even tick per trial on a re-initialised leech and yields (ret, leech, startX).</summary>
    private static IEnumerable<(int ret, Obj p, int x0)> Trials(ObjectManager w, int n, int count, Action<Obj> setup)
    {
        var p = w.Obj(n);
        for (int i = 0; i < count; i++)
        {
            p.Counter = 1; p.X = LeechX; p.Y = LeechY; p.Yd = 0;
            setup(p);
            int ret = w.Dispatch(n, Msg.Update, 0);
            yield return (ret, p, LeechX);
        }
    }

    [Fact]
    public void PauseBranchStopsTheLeechAndReturnsZero()
    {
        var w = Arena();
        int n = NewLeech(w);
        int pauses = 0, moves = 0;
        foreach (var (ret, p, x0) in Trials(w, n, 600, p => { p.Xd = 4; p.SubState = 4; p.StateCount = 5; }))
        {
            if (ret == 0) { pauses++; Assert.Equal(0, p.Xd); Assert.Equal(2, p.StateCount); Assert.Equal(x0, p.X); }
            else { moves++; Assert.Equal(4, p.StateCount); }
        }
        Assert.True(pauses > 0);
        Assert.True(moves > pauses);
    }

    [Fact]
    public void StatecountZeroResumesSavedDirectionAndCrawls()
    {
        var w = Arena();
        int n = NewLeech(w);
        int moved = 0;
        foreach (var (ret, p, x0) in Trials(w, n, 300, p => { p.Xd = 0; p.SubState = 4; p.StateCount = 0; }))
        {
            if (ret == 0) continue; // pause branch
            moved++;
            Assert.Equal(-1, p.StateCount);
            Assert.Equal(p.Xd, p.X - x0);       // crawled by Xd (player is up-left: seek gives -4, resume gives +4)
            Assert.NotEqual(0, p.Xd);
        }
        Assert.True(moved > 200);
    }

    [Fact]
    public void StatecountIsSixteenBitAndWrapsPastMinValue()
    {
        var w = Arena();
        int n = NewLeech(w);
        foreach (var (ret, p, _) in Trials(w, n, 100, p => { p.Xd = 4; p.SubState = 4; p.StateCount = -32768; }))
            Assert.Equal(ret == 0 ? 2 : 32767, p.StateCount);
    }

    [Fact]
    public void SeekSetsHorizontalTimesFourAndVerticalSign()
    {
        var w = Arena();
        w.Obj(0).X = 300; w.Obj(0).Y = LeechY + 30; // right of and below the leech
        int n = NewLeech(w);
        int seeks = 0;
        foreach (var (ret, p, x0) in Trials(w, n, 1500, p => { p.Xd = -4; p.SubState = -4; p.StateCount = 5; }))
        {
            if (ret == 0 || p.Xd != 4) continue; // seek is the only way a -4 leech heads right
            seeks++;
            Assert.Equal(1, p.Yd);
            Assert.Equal(x0 + 4, p.X);
        }
        Assert.True(seeks > 0);
    }

    [Fact]
    public void LedgeReversesDirectionWithoutMoving()
    {
        // floor only under cells 4..6 (x 64..111); the leech spans x 100..143 and faces right off the edge
        var w = Arena(4, 6);
        w.Obj(0).X = 300; // seek also heads right
        int n = NewLeech(w);
        int ticks = 0;
        foreach (var (ret, p, x0) in Trials(w, n, 300, p => { p.Xd = 4; p.SubState = 4; p.StateCount = 5; }))
        {
            if (ret == 0) continue;
            ticks++;
            Assert.Equal(x0, p.X);
            Assert.Equal(-4, p.Xd);
        }
        Assert.True(ticks > 200);
    }

    [Fact]
    public void LongRunOnBoardEdgesAndWithoutFloorNeverThrows()
    {
        foreach (int x in new[] { 0, 2, 280, 300 })
        {
            var w = Arena();
            int n = NewLeech(w, x);
            var p = w.Obj(n);
            p.Xd = 4; p.SubState = 4;
            for (int i = 0; i < 2000; i++) w.Dispatch(n, Msg.Update, 0);
            Assert.Equal(ObjectKind.Leech, p.Kind);
        }
        var w2 = Arena(0, -1); // no floor at all: it just turns around forever
        int m = NewLeech(w2);
        for (int i = 0; i < 500; i++) w2.Dispatch(m, Msg.Update, 0);
        Assert.Equal(ObjectKind.Leech, w2.Obj(m).Kind);
    }

    [Fact]
    public void ResumesAfterPauseSoItIsNeverStuckForever()
    {
        var w = Arena();
        int n = NewLeech(w);
        var p = w.Obj(n);
        p.Xd = 0; p.SubState = 4; p.StateCount = 2;
        int x0 = p.X;
        for (int i = 0; i < 200; i++) w.Dispatch(n, Msg.Update, 0);
        Assert.NotEqual(x0, p.X);
    }

    [Fact]
    public void HeroTouchHurtsPlayerAndReturnsOneWithoutHurtingLeech()
    {
        var w = Arena();
        int n = NewLeech(w);
        w.Obj(0).State = 0;
        int hp = w.Pl.Health;
        Assert.Equal(1, w.Dispatch(n, Msg.Touch, 0));
        Assert.Equal(3, w.Obj(n).ZapHold);
        Assert.Equal(hp - 1, w.Pl.Health);
        Assert.Equal(0, w.Obj(n).State);
        Assert.Equal(ObjectKind.Leech, w.Obj(n).Kind);
        Assert.Equal(0, w.CountKind(ObjectKind.FragExpl));
    }

    [Fact]
    public void LaserHitBumpsStateExplodesAndConsumesLaser()
    {
        var w = Arena();
        int n = NewLeech(w);
        int l = w.AddObj(ObjectKind.Laser, 110, LeechY, 4, 0);
        uint score = w.Pl.Score;
        Assert.Equal(1, w.Dispatch(n, Msg.Touch, l));
        Assert.Equal(1, w.Obj(n).State);
        Assert.Equal(ObjectKind.Killme, w.Obj(l).Kind);
        Assert.Equal(3 + 4, w.CountKind(ObjectKind.FragExpl));
        Assert.Equal(score, w.Pl.Score);
        Assert.Equal(ObjectKind.Leech, w.Obj(n).Kind);
    }

    [Fact]
    public void RockHitAlsoConsumesRock()
    {
        var w = Arena();
        int n = NewLeech(w);
        int r = w.AddObj(ObjectKind.Rock, 110, LeechY, 4, 0);
        Assert.Equal(1, w.Dispatch(n, Msg.Touch, r));
        Assert.Equal(1, w.Obj(n).State);
        Assert.Equal(ObjectKind.Killme, w.Obj(r).Kind);
    }

    [Fact]
    public void FireballHitCountsButFireballPassesThrough()
    {
        var w = Arena();
        int n = NewLeech(w);
        int f = w.AddObj(ObjectKind.Fireball, 110, LeechY, 4, 0);
        Assert.Equal(1, w.Dispatch(n, Msg.Touch, f));
        Assert.Equal(1, w.Obj(n).State);
        Assert.Equal(ObjectKind.Fireball, w.Obj(f).Kind);
    }

    [Fact]
    public void FourthWeaponHitKillsForScoreWithBiggerBurst()
    {
        var w = Arena();
        int n = NewLeech(w);
        for (int hit = 1; hit <= 3; hit++)
        {
            int l = w.AddObj(ObjectKind.Laser, 110, LeechY, 4, 0);
            Assert.Equal(1, w.Dispatch(n, Msg.Touch, l));
            Assert.Equal(hit, w.Obj(n).State);
            Assert.Equal(ObjectKind.Leech, w.Obj(n).Kind);
        }
        Assert.Equal(3 * 7, w.CountKind(ObjectKind.FragExpl));
        uint score = w.Pl.Score;
        int l4 = w.AddObj(ObjectKind.Laser, 110, LeechY, 4, 0);
        Assert.Equal(1, w.Dispatch(n, Msg.Touch, l4));
        Assert.Equal(ObjectKind.Killme, w.Obj(n).Kind);
        Assert.Equal(ObjectKind.Killme, w.Obj(l4).Kind);
        Assert.Equal(score + 300, w.Pl.Score);
        Assert.Equal(3 * 7 + 5 + 6, w.CountKind(ObjectKind.FragExpl));
        Assert.Equal(1, w.CountKind(ObjectKind.Score));
    }

    [Fact]
    public void FireballKillingBlowSparesFireball()
    {
        var w = Arena();
        int n = NewLeech(w);
        w.Obj(n).State = 5; // the C only tests state >= 3
        int f = w.AddObj(ObjectKind.Fireball, 110, LeechY, 4, 0);
        uint score = w.Pl.Score;
        Assert.Equal(1, w.Dispatch(n, Msg.Touch, f));
        Assert.Equal(ObjectKind.Killme, w.Obj(n).Kind);
        Assert.Equal(ObjectKind.Fireball, w.Obj(f).Kind);
        Assert.Equal(score + 300, w.Pl.Score);
    }

    [Fact]
    public void NonWeaponNonHeroTouchIsInertButReturnsOne()
    {
        var w = Arena();
        int n = NewLeech(w);
        int b = w.AddObj(ObjectKind.Bonus, 110, LeechY, 0, 0);
        Assert.Equal(1, w.Dispatch(n, Msg.Touch, b));
        Assert.Equal(0, w.Obj(n).State);
        Assert.Equal(0, w.Obj(n).ZapHold);
        Assert.NotEqual(ObjectKind.Killme, w.Obj(b).Kind);
        Assert.Equal(0, w.CountKind(ObjectKind.FragExpl));
    }

    [Theory]
    [InlineData(0, -4, 0, 4)]     // idle after heading left
    [InlineData(0, 4, 0, 9)]      // idle after heading right
    [InlineData(0, 0, 0, 9)]      // never moved: substate 0 is not < 0
    [InlineData(-4, -4, 0, 0)]
    [InlineData(-4, -4, 6, 3)]
    [InlineData(4, 4, 0, 5)]
    [InlineData(4, 4, 6, 8)]
    public void DrawFrameSelection(int xd, int sub, int counter, int expected) =>
        Assert.Equal(expected, Behaviors.LeechFrame(xd, sub, counter));

    private static int Ink(ObjectManager w)
    {
        w.Screen.Clear(255);
        var vp = new Viewport(0, 2, VgaScreen.Width, 158);
        w.DrawFore(w.Screen, vp);
        int ink = 0;
        for (int y = 40; y < 130; y++)
            for (int x = 80; x < 170; x++)
                if (w.Screen.Pixels[(y + vp.Y) * VgaScreen.Width + x] != 255) ink++;
        return ink;
    }

    /// <summary>Only episode 3 ships leech art (shape table 42, shapes 0..9); episodes 1 and 2 have none.</summary>
    [Theory]
    [InlineData(-4, 4, 0)]
    [InlineData(4, 4, 6)]
    [InlineData(0, -4, 0)]
    [InlineData(0, 4, 0)]
    public void DrawPutsPixelsOnScreen(int xd, int sub, int counter)
    {
        var w = Arena(episode: 3);
        int n = w.AddObj(ObjectKind.Leech, 100, 60, xd, 0);
        w.Obj(n).SubState = sub; w.Obj(n).Counter = counter;
        Assert.True(Ink(w) > 0);
    }
}
