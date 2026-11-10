using Xunit;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Tests;

public class ScoreTests
{
    // A tall viewport so a popup that rises 30 frames never leaves the screen (OnScreen kills it otherwise).
    private static readonly Viewport Tall = new(0, 2, 320, 6000) { OriginX = 0, OriginY = -3000 };

    private static void Step(ObjectManager w, Viewport vp, int frames)
    {
        for (int i = 0; i < frames; i++) { w.Gamecount++; w.UpdateObjects(vp); }
    }

    [Fact]
    public void AddScoreSpawnsDriftingPopup()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        int px = w.Obj(0).X;
        uint before = w.Pl.Score;
        w.AddScore(500, px + 50, 100);
        Assert.Equal(before + 500, w.Pl.Score);
        Assert.Equal(1, w.CountKind(ObjectKind.Score));
        var o = w.Objects[^1];
        Assert.Equal(ObjectKind.Score, o.Kind);
        Assert.Equal(500, o.State);
        Assert.Equal(30, o.Counter);
        Assert.Equal(px + 50, o.X); Assert.Equal(100, o.Y);
        Assert.InRange(o.Xd, 0, 3);   // popup is right of the player: drifts right
        Assert.InRange(o.Yd, 4, 7);
        Assert.Equal((2 + 3) * KindDefs.Get(ObjectKind.Score).Xl, o.Xl);
    }

    [Fact]
    public void ScorePopupIsDrawnInFrontPass()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        int n = w.AddObj(ObjectKind.Score, 200, 100, 0, 0);
        w.Obj(n).State = 500;
        var vp = new Viewport(0, 2, 320, 158) { OriginX = 150, OriginY = 50 };
        var trace = new List<int>();
        w.DrawTrace = trace;
        w.Screen.Clear(1);
        w.DrawFore(w.Screen, vp);
        Assert.Contains(n, trace);

        bool changed = false;
        for (int y = vp.Y + 50; y < vp.Y + 50 + 12 && !changed; y++)
            for (int x = 50; x < 50 + 24; x++)
                if (w.Screen.Pixels[y * VgaScreen.Width + x] != 1) { changed = true; break; }
        Assert.True(changed, "score digits left no pixels");
    }

    [Fact]
    public void KillingGruntScoresOnceAndPopupExpires()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        int px = w.Obj(0).X, py = w.Obj(0).Y;
        int g = w.AddObj(ObjectKind.Grunt, px + 20, py, 0, 0);
        w.Obj(g).State = 4; // dying
        uint before = w.Pl.Score;
        Step(w, Tall, 1);
        Assert.Equal(before + (uint)KindDefs.Get(ObjectKind.Grunt).Score, w.Pl.Score);
        Assert.Equal(1, w.CountKind(ObjectKind.Score));
        Step(w, Tall, 30); // counter 30 -> survives 30 updates (29..0), culled by the 31st (<0)
        Assert.Equal(1, w.CountKind(ObjectKind.Score));
        Step(w, Tall, 1);
        Assert.Equal(0, w.CountKind(ObjectKind.Score));
        Assert.Equal(before + (uint)KindDefs.Get(ObjectKind.Grunt).Score, w.Pl.Score);
    }

    [Fact]
    public void PlayerKillRecordsCellsAsModified()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        int n = w.AddObj(ObjectKind.Bonus, 100, 100, 0, 0);
        Assert.Empty(w.ModifiedCells);
        w.PlayerKill(n);
        Assert.Contains((6, 6), w.ModifiedCells);
    }
}
