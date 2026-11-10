using Xunit;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Tests;

public class WaterAndFrontTests
{
    /// <summary>Level 1 world with cells (4..14, 2..8) cleared to empty air.</summary>
    private static ObjectManager Arena()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        for (int y = 2; y < 9; y++)
            for (int x = 4; x < 15; x++) w.SetBoard(x, y, 0);
        return w;
    }

    [Fact]
    public void BubbleOnNonWaterIsKilledWithinOneUpdate()
    {
        var w = Arena();
        w.AddObj(ObjectKind.Bubble, 100, 80, 0, 0);
        TestWorld.Step(w, 1);
        Assert.Equal(0, w.CountKind(ObjectKind.Bubble));
    }

    [Fact]
    public void BubbleInWaterSurvivesAndRises()
    {
        var w = Arena();
        for (int y = 2; y < 9; y++)
            for (int x = 4; x < 15; x++) w.SetBoard(x, y, Tiles.WaterT1);
        int n = w.AddObj(ObjectKind.Bubble, 100, 100, 0, 0);
        int y0 = w.Obj(n).Y;
        TestWorld.Step(w, 3);
        Assert.Equal(1, w.CountKind(ObjectKind.Bubble));
        Assert.True(w.Obj(n).Y < y0);
    }

    [Fact]
    public void ReactorTouchedByLaserCountsHitAndConsumesWeapon()
    {
        var w = Arena();
        int r = w.AddObj(ObjectKind.Front, 100, 60, 0, 0);
        w.Obj(r).State = 13;
        int l = w.AddObj(ObjectKind.Laser, 110, 70, 0, 0);
        int fx = w.CountKind(ObjectKind.FireExpl);
        Assert.Equal(1, w.Dispatch(r, Msg.Touch, l));
        Assert.Equal(1, w.Obj(r).Info1);
        Assert.Equal(ObjectKind.Killme, w.Obj(l).Kind);
        Assert.Equal(fx + 2, w.CountKind(ObjectKind.FireExpl));
    }

    /// <summary>Number of non-sentinel pixels in the reactor sprite rectangle (up to 64x64) after drawing the fore layers.</summary>
    private static int ReactorInkAt(int state, int info1)
    {
        var w = Arena();
        int r = w.AddObj(ObjectKind.Front, 100, 60, 0, 0);
        w.Obj(r).State = state;
        w.Obj(r).Info1 = info1;
        w.Screen.Clear(255);
        // Fresh viewport: TestWorld.Vp is shared and other tests may scroll it, which would move the sprite.
        var vp = new Viewport(0, 2, VgaScreen.Width, 158);
        w.DrawFore(w.Screen, vp);
        int ink = 0;
        for (int y = 60; y < 130; y++)
            for (int x = 100; x < 170; x++)
                if (w.Screen.Pixels[(y + vp.Y) * VgaScreen.Width + x] != 255) ink++;
        return ink;
    }

    [Fact]
    public void ReactorTouchedByPlayerHurtsPlayerAndDoesNotCountHit()
    {
        var w = Arena();
        int r = w.AddObj(ObjectKind.Front, 100, 60, 0, 0);
        w.Obj(r).State = 13;
        Assert.Equal(0, w.Dispatch(r, Msg.Touch, 0));
        Assert.Equal(3, w.Obj(r).ZapHold); // Hitplayer ran
        Assert.Equal(0, w.Obj(r).Info1);
    }

    [Fact]
    public void ReactorTouchedByNonWeaponNonPlayerLeavesHitsUnchanged()
    {
        var w = Arena();
        int r = w.AddObj(ObjectKind.Front, 100, 60, 0, 0);
        w.Obj(r).State = 13;
        int b = w.AddObj(ObjectKind.Bonus, 110, 70, 0, 0);
        int fx = w.CountKind(ObjectKind.FireExpl);
        Assert.Equal(0, w.Dispatch(r, Msg.Touch, b));
        Assert.Equal(0, w.Obj(r).Info1);
        Assert.Equal(ObjectKind.Bonus, w.Obj(b).Kind);          // not consumed
        Assert.Equal(fx, w.CountKind(ObjectKind.FireExpl));     // no explosion
    }

    [Fact]
    public void ReactorAtFortyHitsIgnoresWeaponTouch()
    {
        var w = Arena();
        int r = w.AddObj(ObjectKind.Front, 100, 60, 0, 0);
        w.Obj(r).State = 13;
        w.Obj(r).Info1 = 40;
        int l = w.AddObj(ObjectKind.Laser, 110, 70, 0, 0);
        Assert.Equal(0, w.Dispatch(r, Msg.Touch, l));
        Assert.Equal(40, w.Obj(r).Info1);
        Assert.NotEqual(ObjectKind.Killme, w.Obj(l).Kind);
    }

    [Fact]  // state 14 (0x2604) draws nothing in the ep1 harness (not investigated), so only state 13 is asserted
    public void ReactorDrawsOnlyWhileFewerThanFortyHits()
    {
        Assert.True(ReactorInkAt(13, 0) > 0, "undamaged reactor draws");
        Assert.True(ReactorInkAt(13, 39) > 0, "39 hits still draws");
        Assert.Equal(0, ReactorInkAt(13, 40));
        Assert.Equal(0, ReactorInkAt(13, 41));
    }

    [Theory]
    [InlineData(ObjectKind.Flash)]
    [InlineData(ObjectKind.HitHero)]
    [InlineData(ObjectKind.FragExpl)]
    [InlineData(ObjectKind.CentExpl)]
    public void EffectObjectsOffScreenAreKilledOnUpdate(int kind)
    {
        var w = Arena();
        // Inside the 96px update margin but outside the 320-wide viewport.
        w.AddObj(kind, 360, 80, 0, 0);
        TestWorld.Step(w, 1);
        Assert.Equal(0, w.CountKind(kind));
    }

    [Fact]
    public void SearockEventuallySpawnsBubbleAtOffset()
    {
        var w = Arena();
        int n = w.AddObj(ObjectKind.SeaRock, 100, 80, 0, 0);
        for (int i = 0; i < 400 && w.CountKind(ObjectKind.Bubble) == 0; i++)
            w.Dispatch(n, Msg.Update, 0);
        int b = -1;
        for (int i = 0; i < w.Objects.Count; i++) if (w.Obj(i).Kind == ObjectKind.Bubble) b = i;
        Assert.True(b >= 0);
        Assert.Equal(102, w.Obj(b).X);
        Assert.Equal(84, w.Obj(b).Y);
    }
}
