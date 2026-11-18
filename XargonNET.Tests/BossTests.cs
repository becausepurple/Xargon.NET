using Xunit;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>x_obj2.c:867-939 msg_xargon: the end boss (kind 87).</summary>
public class BossTests
{
    private const int StillState = 1; // st_still

    private static (ObjectManager w, int b) Boss(int substate = 0)
    {
        var w = TestWorld.Load(3, "BOARD_01");
        int b = w.AddObj(ObjectKind.Xargon, 100, 40, 0, 0);
        w.Obj(b).SubState = substate;
        return (w, b);
    }

    private static uint Pack(int r, int g, int b) =>
        0xFF000000u | (uint)(r * 255 / 63) << 16 | (uint)(g * 255 / 63) << 8 | (uint)(b * 255 / 63);

    private static void Update(ObjectManager w, int b, int frames)
    {
        for (int i = 0; i < frames; i++) w.Dispatch(b, Msg.Update, 0);
    }

    private static List<int> Of(ObjectManager w, int kind)
    {
        var l = new List<int>();
        for (int i = 0; i < w.Objects.Count; i++) if (w.Obj(i).Kind == kind) l.Add(i);
        return l;
    }

    [Fact]
    public void WeaponHitsAccumulateAndKillAt50()
    {
        var (w, b) = Boss();
        uint score = w.Pl.Score;
        for (int hit = 1; hit <= 50; hit++)
        {
            // A fireball must be consumed too (unlike robot/ghoul, msg_xargon kills z unconditionally).
            int z = w.AddObj(hit % 2 == 0 ? ObjectKind.Fireball : ObjectKind.Rock, 100, 40, 0, 0);
            int fires = Of(w, ObjectKind.FireExpl).Count, frags = Of(w, ObjectKind.FragExpl).Count;
            Assert.Equal(1, w.Dispatch(b, Msg.Touch, z));
            Assert.Equal(hit, w.Obj(b).SubState);
            Assert.Equal(ObjectKind.Killme, w.Obj(z).Kind);
            Assert.Equal(2, Of(w, ObjectKind.FireExpl).Count - fires);
            Assert.Equal(5, Of(w, ObjectKind.FragExpl).Count - frags); // explode1 counts 2 + 3
            if (hit < 50)
            {
                w.Dispatch(b, Msg.Update, 0);
                Assert.Equal(0, w.Obj(b).StateCount); // still alive: no death sequence
                Assert.Equal(score, w.Pl.Score);
            }
        }
        w.Dispatch(b, Msg.Update, 0);
        Assert.Equal(1, w.Obj(b).StateCount); // the 50th hit starts the death sequence
        Assert.Equal(score + 30000, w.Pl.Score);
    }

    [Fact]
    public void WeaponHitBurstsStayInsideCExtents()
    {
        var (w, b) = Boss();
        var o = w.Obj(b);
        int maxFx = 0, maxFy = 0, maxEx = 0, maxEy = 0;
        for (int i = 0; i < 300; i++)
        {
            int z = w.AddObj(ObjectKind.Rock, 100, 40, 0, 0);
            o.SubState = 0;
            int before = w.Objects.Count;
            w.Dispatch(b, Msg.Touch, z);
            for (int k = before; k < w.Objects.Count; k++)
            {
                var q = w.Obj(k);
                if (q.Kind == ObjectKind.FireExpl)
                {
                    Assert.InRange(q.X - 100, 0, 43); Assert.InRange(q.Y - 40, 0, 51);
                    maxFx = Math.Max(maxFx, q.X - 100); maxFy = Math.Max(maxFy, q.Y - 40);
                }
                else if (q.Kind == ObjectKind.FragExpl)
                {
                    Assert.InRange(q.X - 100, 0, 54); Assert.InRange(q.Y - 40, 0, 59);
                    maxEx = Math.Max(maxEx, q.X - 100); maxEy = Math.Max(maxEy, q.Y - 40);
                }
            }
        }
        Assert.True(maxFx >= 40 && maxFy >= 48 && maxEx >= 51 && maxEy >= 56);
    }

    [Fact]
    public void TouchingHeroHurtsPlayerWithoutDamagingBoss()
    {
        var (w, b) = Boss();
        w.Obj(0).State = 0; // vulnerable (the start state is invincible)
        int health = w.Pl.Health;
        Assert.Equal(1, w.Dispatch(b, Msg.Touch, 0));
        Assert.Equal(health - 1, w.Pl.Health);
        Assert.Equal(0, w.Obj(b).SubState);
        // A non-weapon, non-hero toucher does nothing, yet the C still returns 1.
        int bonus = w.AddObj(ObjectKind.Bonus, 100, 40, 0, 0);
        Assert.Equal(1, w.Dispatch(b, Msg.Touch, bonus));
        Assert.Equal(0, w.Obj(b).SubState);
        Assert.Equal(ObjectKind.Bonus, w.Obj(bonus).Kind);
    }

    [Fact]
    public void AliveBossBobsAlongYdtabAndCounterWraps()
    {
        var (w, b) = Boss();
        var o = w.Obj(b);
        int[] ydtab = { 1, 2, 1, 0, -1, -2, -1, 0 };
        int y = o.Y;
        for (int f = 1; f <= 40; f++)
        {
            w.Dispatch(b, Msg.Update, 0);
            Assert.Equal(f & 15, o.Counter);
            y += ydtab[(f & 15) / 2];
            Assert.Equal(y, o.Y);
        }
    }

    [Fact]
    public void BossKeepsDistanceByFlippingXd()
    {
        // Hero to the right: a random seek also gives +4, so the result is deterministic every frame.
        var (w, b) = Boss();
        var hero = w.Obj(0);
        var o = w.Obj(b);
        for (int i = 0; i < 30; i++)
        {
            o.X = 100; hero.X = 130; hero.Y = o.Y; o.Xd = -4; // dist<80 && xd<0 -> flips, closes in
            w.Dispatch(b, Msg.Update, 0);
            Assert.Equal(104, o.X);
        }
        for (int i = 0; i < 30; i++)
        {
            o.X = 100; hero.X = 400; hero.Y = o.Y; o.Xd = 4; // dist>256 && xd>=0 -> flips, backs away
            w.Dispatch(b, Msg.Update, 0);
            Assert.Equal(96, o.X);
        }
        // Exact thresholds (hero to the left makes a random seek agree with the expected result).
        foreach (var (dist, expect) in new[] { (79, 104), (80, 96) })
        {
            for (int i = 0; i < 30; i++)
            {
                o.X = 400; hero.X = 400 - dist; hero.Y = o.Y; o.Xd = -4;
                w.Dispatch(b, Msg.Update, 0);
                Assert.Equal(expect, o.X - 300);
            }
        }
        foreach (var (dist, expect) in new[] { (256, 104), (257, 96) })
        {
            for (int i = 0; i < 30; i++)
            {
                o.X = 100; hero.X = 100 + dist; hero.Y = o.Y; o.Xd = 4;
                w.Dispatch(b, Msg.Update, 0);
                Assert.Equal(expect, o.X);
            }
        }
        for (int i = 0; i < 30; i++)
        {
            o.X = 100; hero.X = 230; hero.Y = o.Y; o.Xd = 4; // mid range: no flip
            w.Dispatch(b, Msg.Update, 0);
            Assert.Equal(104, o.X);
        }
    }

    [Fact]
    public void BossSpawnsSkullsWithXdMinus2AndRearmsAfter15Frames()
    {
        var (w, b) = Boss();
        var o = w.Obj(b);
        var spawnFrames = new List<int>();
        for (int f = 1; f <= 2000; f++)
        {
            int skulls = Of(w, ObjectKind.Skull).Count;
            w.Dispatch(b, Msg.Update, 0);
            var now = Of(w, ObjectKind.Skull);
            if (now.Count == skulls) continue;
            Assert.Equal(skulls + 1, now.Count);
            var s = w.Obj(now[^1]);
            Assert.Equal(-2, s.Xd);
            Assert.Equal(0, s.Yd);
            Assert.Equal(o.X - 10, s.X);
            Assert.Equal(o.Y + 50, s.Y);
            Assert.Equal(15, o.Info1);
            Assert.Equal(5, o.State);
            spawnFrames.Add(f);
        }
        Assert.True(spawnFrames.Count >= 30, "about one skull per 20 frames, rate-limited");
        for (int i = 1; i < spawnFrames.Count; i++) Assert.True(spawnFrames[i] - spawnFrames[i - 1] >= 15);
    }

    [Fact]
    public void BossWithNonZeroYdNeverSpawnsSkulls()
    {
        var (w, b) = Boss();
        w.Obj(b).Yd = 1;
        Update(w, b, 1500);
        Assert.Empty(Of(w, ObjectKind.Skull));
    }

    [Fact]
    public void BossStateDecaysAfterSpawn()
    {
        var (w, b) = Boss();
        var o = w.Obj(b);
        for (int g = 0; g < 2000 && Of(w, ObjectKind.Skull).Count == 0; g++) w.Dispatch(b, Msg.Update, 0);
        Assert.Equal(5, o.State);
        int skulls = Of(w, ObjectKind.Skull).Count;
        for (int k = 4; k >= 0; k--)
        {
            w.Dispatch(b, Msg.Update, 0);
            if (Of(w, ObjectKind.Skull).Count != skulls) break; // cannot happen within 15 frames
            Assert.Equal(k, o.State);
        }
        Assert.Equal(0, o.State);
    }

    [Fact]
    public void DeathSequenceGivesScoreOnceAndAnnouncesDeath()
    {
        var (w, b) = Boss(50);
        w.Pl.Health = 2;
        uint score = w.Pl.Score;
        w.Dispatch(b, Msg.Update, 0);
        Assert.Equal(score + 30000, w.Pl.Score);
        Assert.Equal(5, w.Pl.Health);
        Assert.Equal(StillState, w.Obj(0).State);
        Assert.Equal(3, w.Obj(b).State);
        Assert.Equal(1, w.Obj(b).StateCount);
        // One-off effects are not re-applied.
        w.Obj(0).State = 0; w.Pl.Health = 3;
        for (int i = 0; i < 56; i++) w.Dispatch(b, Msg.Update, 0);
        Assert.Equal(score + 30000, w.Pl.Score);
        Assert.Equal(3, w.Pl.Health);
        Assert.Equal(0, w.Obj(0).State);
        Assert.Equal(57, w.Obj(b).StateCount);
        Assert.DoesNotContain("XARGON IS DEAD", w.Status.Message);
        w.Dispatch(b, Msg.Update, 0); // StateCount 57 -> 58: still no text
        Assert.DoesNotContain("XARGON IS DEAD", w.Status.Message);
        w.Dispatch(b, Msg.Update, 0); // StateCount==58 at entry: announce
        Assert.Equal("XARGON IS DEAD! Press ESC to continue", w.Status.Message);
        Assert.Equal(7, w.Status.Color);
        Assert.Equal(3, w.Pl.Health);
    }

    [Fact]
    public void DeathSequenceEmitsBurstsEveryFrameInsideCExtents()
    {
        var (w, b) = Boss(50);
        var o = w.Obj(b);
        int minFx = int.MaxValue, maxFx = int.MinValue, minHx = int.MaxValue, maxHx = int.MinValue;
        int minFy = int.MaxValue, maxFy = int.MinValue, minHy = int.MaxValue, maxHy = int.MinValue;
        for (int f = 0; f < 59; f++)
        {
            int fe = Of(w, ObjectKind.FireExpl).Count, hh = Of(w, ObjectKind.HitHero).Count, fr = Of(w, ObjectKind.FragExpl).Count;
            w.Dispatch(b, Msg.Update, 0);
            Assert.Equal(1, Of(w, ObjectKind.FireExpl).Count - fe);
            Assert.Equal(2, Of(w, ObjectKind.FragExpl).Count - fr);
            Assert.Equal(1, Of(w, ObjectKind.HitHero).Count - hh);
            var fire = w.Obj(Of(w, ObjectKind.FireExpl)[^1]);
            var hit = w.Obj(Of(w, ObjectKind.HitHero)[^1]);
            Assert.InRange(fire.X - o.X, -15, 44); Assert.InRange(fire.Y - o.Y, -15, 52);
            Assert.InRange(hit.X - o.X, -12, 47); Assert.InRange(hit.Y - o.Y, -17, 50);
            minFx = Math.Min(minFx, fire.X - o.X); maxFx = Math.Max(maxFx, fire.X - o.X);
            minFy = Math.Min(minFy, fire.Y - o.Y); maxFy = Math.Max(maxFy, fire.Y - o.Y);
            minHx = Math.Min(minHx, hit.X - o.X); maxHx = Math.Max(maxHx, hit.X - o.X);
            minHy = Math.Min(minHy, hit.Y - o.Y); maxHy = Math.Max(maxHy, hit.Y - o.Y);
        }
        Assert.True(minFx < -8 && maxFx > 36 && minFy < -8 && maxFy > 44);
        Assert.True(minHx < -5 && maxHx > 40 && minHy < -10 && maxHy > 42);
    }

    [Fact]
    public void DeathSequenceShakesBossAlongDieTables()
    {
        var (w, b) = Boss(50);
        var o = w.Obj(b);
        int[] xdie = { 4, -4, 4, -4, 4, -4, 4, -4 };
        int[] ydie = { 4, -2, 6, -4, 8, -6, 8, -6 };
        int x = o.X, y = o.Y;
        for (int f = 1; f <= 20; f++)
        {
            w.Dispatch(b, Msg.Update, 0);
            int i = (f & 15) / 2;
            x += xdie[i]; y += ydie[i];
            Assert.Equal((x, y), (o.X, o.Y));
            Assert.Equal(3, o.State);
        }
    }

    [Fact]
    public void DeathSequenceDimsOnlyWhenCounterHalfIsZeroAndSetsColour250()
    {
        var (w, b) = Boss(50);
        var s = w.Screen;
        int c = 0; // brightest source colour so dim and undim differ
        for (int i = 1; i < 250; i++)
            if (s.Dac[i * 3] + s.Dac[i * 3 + 1] + s.Dac[i * 3 + 2] > s.Dac[c * 3] + s.Dac[c * 3 + 1] + s.Dac[c * 3 + 2]) c = i;
        s.Undim();
        uint undimmed = s.Palette[c];
        s.Dim();
        uint dimmed = s.Palette[c];
        Assert.NotEqual(dimmed, undimmed);
        for (int f = 1; f <= 32; f++)
        {
            s.SetColor6(250, 1, 2, 3);
            w.Dispatch(b, Msg.Update, 0);
            int half = (f & 15) / 2;
            Assert.Equal(half == 0 ? dimmed : undimmed, s.Palette[c]);
            Assert.Equal(Pack(20, 20, 23), s.Palette[250]); // set every frame
        }
    }

    [Fact]
    public void DeathWaitsForEscThenWinsGame()
    {
        var (w, b) = Boss(50);
        for (int f = 1; f <= 59; f++)
        {
            w.Dispatch(b, Msg.Update, 0);
            Assert.Null(w.TakeEscWait());
        }
        w.Dispatch(b, Msg.Update, 0); // ++StateCount reaches 60 on the 60th update
        var cont = w.TakeEscWait();
        Assert.NotNull(cont);
        Assert.False(w.GameWon);
        Assert.Equal(GameOver.None, GameFlow.PollGameWon(w));
        cont!();
        Assert.True(w.GameWon);
        Assert.Equal(GameOver.Won, GameFlow.PollGameWon(w));
        Assert.Equal(GameOver.None, GameFlow.PollGameWon(w));
        Assert.Equal(Pack(20, 20, 23), w.Screen.Palette[250]);
    }

    [Fact]
    public void DestroyedBossIsNotHittable()
    {
        var (w, b) = Boss(50);
        w.Obj(0).State = 0; // vulnerable (the start state is invincible)
        int health = w.Pl.Health;
        int z = w.AddObj(ObjectKind.Rock, 100, 40, 0, 0);
        int objs = w.Objects.Count;
        Assert.Equal(0, w.Dispatch(b, Msg.Touch, z));
        Assert.Equal(50, w.Obj(b).SubState);
        Assert.Equal(ObjectKind.Rock, w.Obj(z).Kind);
        Assert.Equal(objs, w.Objects.Count);
        Assert.Equal(0, w.Dispatch(b, Msg.Touch, 0));
        Assert.Equal(health, w.Pl.Health);
    }

    private static byte[] Render(ObjectManager w)
    {
        var s = new VgaScreen();
        s.Clear(0);
        w.DrawFore(s, TestWorld.Vp);
        return (byte[])s.Pixels.Clone();
    }

    [Fact]
    public void BossIsDrawnUntilStatecount59ThenHidden()
    {
        var (w, b) = Boss(50);
        var o = w.Obj(b);
        o.StateCount = 58;
        var visible = Render(w);
        o.StateCount = 59;
        var hidden = Render(w);
        Assert.NotEqual(visible, hidden);
        o.Kind = ObjectKind.Killme;
        Assert.Equal(Render(w), hidden); // hidden == boss absent
    }

    [Fact]
    public void BossUsesSecondFrameWhileStatePositive()
    {
        var (w, b) = Boss();
        var o = w.Obj(b);
        o.State = 0;
        var calm = Render(w);
        o.State = 3;
        var angry = Render(w);
        Assert.NotEqual(calm, angry);
        o.State = 1;
        Assert.Equal(angry, Render(w)); // any positive state is the same frame
    }
}
