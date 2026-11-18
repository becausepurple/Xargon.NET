using Xunit;
using XargonNET.Objects;

namespace XargonNET.Tests;

public class ReactorTests
{
    /// <summary>Level 1 world with a destroyed reactor (info1 40) of the given state/counter.</summary>
    private static (ObjectManager w, int r) Reactor(int state, int counter, int episode = 1)
    {
        var w = TestWorld.Load(episode, "BOARD_01");
        int r = w.AddObj(ObjectKind.Front, 100, 60, 0, 0);
        var o = w.Obj(r);
        o.State = state; o.Info1 = 40; o.Counter = counter;
        return (w, r);
    }

    private static void Run(ObjectManager w, int frames)
    {
        for (int i = 0; i < frames; i++) w.UpdateObjects(TestWorld.Vp);
    }

    [Fact]
    public void DestroyedReactorGivesScoreAndFreezesHeroOnFirstFrame()
    {
        var (w, r) = Reactor(13, 0);
        w.Pl.Health = 2;
        uint score = w.Pl.Score;
        w.Dispatch(r, Msg.Update, 0);
        Assert.Equal(score + 20000, w.Pl.Score);
        Assert.Equal(5, w.Pl.Health);
        Assert.Equal(1, w.Obj(0).State);
        Assert.Equal(1, w.Obj(r).StateCount);
        // Frame-0 effects are applied once only.
        w.Obj(0).State = 0; w.Pl.Health = 3;
        for (int i = 0; i < 4; i++) w.Dispatch(r, Msg.Update, 0);
        Assert.Equal(score + 20000, w.Pl.Score);
        Assert.Equal(3, w.Pl.Health);
        Assert.Equal(0, w.Obj(0).State);
    }

    [Fact]
    public void Counter2NeverAnnouncesDestruction()
    {
        var (w, r) = Reactor(13, 2, 3);
        for (int i = 0; i < 59; i++) w.Dispatch(r, Msg.Update, 0);
        Assert.DoesNotContain("REACTOR DESTROYED", w.Status.Message);
    }

    [Fact]
    public void ReactorAnnouncesDestructionAtFrame58()
    {
        var (w, r) = Reactor(13, 0);
        for (int i = 0; i < 58; i++) w.Dispatch(r, Msg.Update, 0);
        Assert.DoesNotContain("REACTOR DESTROYED", w.Status.Message);
        w.Dispatch(r, Msg.Update, 0);
        Assert.Equal("REACTOR DESTROYED! Press ESC to continue", w.Status.Message);
        Assert.Equal(7, w.Status.Color);
    }

    [Fact]
    public void VolumeOneWaitsForEscThenQueuesBoard33()
    {
        var (w, r) = Reactor(14, 0);
        var popups = new RecordingPopups();
        w.Popups = popups;
        int cp = w.AddObj(ObjectKind.Checkpt, 200, 120, 0, 0);
        w.Obj(cp).Counter = 33;
        for (int i = 0; i < 59; i++) w.Dispatch(r, Msg.Update, 0);
        Assert.Null(w.TakeEscWait());
        w.Dispatch(r, Msg.Update, 0);
        Assert.Null(w.NewLevel);
        var cont = w.TakeEscWait();
        Assert.NotNull(cont);
        Assert.Null(w.NewLevel);
        Assert.Empty(popups.Dialogs);
        cont!();
        Assert.Equal(new[] { (22, false) }, popups.Dialogs);
        Assert.Equal("board_33", w.NewLevel);
        Assert.Equal(33, w.Pl.Level);
        Assert.Equal(200, w.Obj(0).X);
        Assert.Equal(96, w.Obj(0).Y);
        Assert.Equal(4, w.Obj(0).State);
        Assert.Equal(0, w.Obj(0).StateCount);
    }

    [Fact]
    public void VolumeTwoEscSetsGameWon()
    {
        var (w, r) = Reactor(13, 1, 2);
        for (int i = 0; i < 60; i++) w.Dispatch(r, Msg.Update, 0);
        Assert.False(w.GameWon);
        var cont = w.TakeEscWait();
        Assert.NotNull(cont);
        Assert.Null(w.NewLevel);
        cont!();
        Assert.True(w.GameWon);
        Assert.Null(w.NewLevel);
    }

    [Fact]
    public void VolumeThreeNeverWaitsAndKillsItselfWithTrigger()
    {
        var (w, r) = Reactor(13, 2, 3);
        int t = w.AddObj(ObjectKind.Turret, 300, 60, 0, 0);
        w.Obj(t).Counter = 2;
        w.Obj(t).State = 0;
        for (int i = 0; i < 60; i++) w.Dispatch(r, Msg.Update, 0);
        Assert.Null(w.TakeEscWait());
        Assert.Equal(ObjectKind.Killme, w.Obj(r).Kind);
        Assert.Equal(1, w.Obj(t).State); // toggled by Msg.Trigger
        Assert.Equal(0, w.Obj(0).State);
        Assert.Contains("MALVINEOUS", w.Status.Message);
    }

    [Fact]
    public void EscRequestedOnceAtFrame60()
    {
        // Take after every frame: a request must first appear exactly on the 60th update, and its
        // continuation must have its effect exactly once.
        var (w, r) = Reactor(13, 1, 2);
        for (int f = 1; f <= 59; f++)
        {
            w.Dispatch(r, Msg.Update, 0);
            Assert.Null(w.TakeEscWait());
        }
        w.Dispatch(r, Msg.Update, 0);
        var cont = w.TakeEscWait();
        Assert.NotNull(cont);
        Assert.False(w.GameWon);
        cont!();
        Assert.True(w.GameWon);
        // Ignored-while-pending: leaving it pending across more frames still yields a single request.
        w.GameWon = false;
        for (int f = 0; f < 20; f++) w.Dispatch(r, Msg.Update, 0);
        Assert.NotNull(w.TakeEscWait());
        Assert.Null(w.TakeEscWait());
    }

    private static uint Pack(int r, int g, int b) =>
        0xFF000000u | (uint)(r * 255 / 63) << 16 | (uint)(g * 255 / 63) << 8 | (uint)(b * 255 / 63);

    [Fact]
    public void VolumeThreeSetsExactPaletteColours()
    {
        var (w, r) = Reactor(13, 2, 3);
        for (int i = 0; i < 60; i++) w.Dispatch(r, Msg.Update, 0);
        var p = w.Screen.Palette;
        Assert.Equal(Pack(20, 20, 23), p[250]);
        Assert.Equal(Pack(23, 13, 33), p[162]);
        Assert.Equal(Pack(29, 19, 39), p[163]);
        Assert.Equal(Pack(35, 25, 45), p[164]);
        Assert.Equal(Pack(42, 32, 51), p[165]);
        Assert.Equal(Pack(49, 40, 57), p[166]);
        Assert.Equal(Pack(57, 50, 63), p[167]);
    }

    [Fact]
    public void Colour250IsSetOnlyForCounter2DuringDestruction()
    {
        var (w2, r2) = Reactor(13, 2, 3);
        w2.Dispatch(r2, Msg.Update, 0);
        Assert.Equal(Pack(20, 20, 23), w2.Screen.Palette[250]);
        var (w0, r0) = Reactor(13, 0);
        w0.Dispatch(r0, Msg.Update, 0);
        Assert.NotEqual(Pack(20, 20, 23), w0.Screen.Palette[250]);
    }

    /// <summary>Positions of every FireExpl after 59 updates (before the ending fires at 60).</summary>
    private static List<(int x, int y)> BurstPositions(int state)
    {
        var (w, r) = Reactor(state, 0);
        for (int i = 0; i < 59; i++) w.Dispatch(r, Msg.Update, 0);
        var l = new List<(int, int)>();
        for (int i = 0; i < w.Objects.Count; i++)
            if (w.Obj(i).Kind == ObjectKind.FireExpl) l.Add((w.Obj(i).X, w.Obj(i).Y));
        return l;
    }

    [Theory]
    [InlineData(13, 44)]
    [InlineData(14, 64)]
    public void FireExplOffsetsFollowStateExtentMinus15(int state, int extent)
    {
        var pos = BurstPositions(state);
        Assert.True(pos.Count >= 59);
        Assert.All(pos, q =>
        {
            Assert.InRange(q.x, 100 - 15, 100 + extent - 1 - 15);
            Assert.InRange(q.y, 60 - 15, 60 + extent - 1 - 15);
        });
        // The range is actually used: reaches both below the object origin and near the far bound.
        Assert.Contains(pos, q => q.x < 100);
        Assert.Contains(pos, q => q.x > 100 + extent - 1 - 15 - 12);
        Assert.Contains(pos, q => q.y > 60 + extent - 1 - 15 - 12);
    }

    [Fact]
    public void State14BurstsReachBeyondState13Bounds()
    {
        Assert.DoesNotContain(BurstPositions(13), q => q.x > 100 + 43 - 15);
        Assert.Contains(BurstPositions(14), q => q.x > 100 + 43 - 15);
    }

    private sealed class RecordingPopups : IPopupHost
    {
        public readonly List<(int, bool)> Dialogs = new();
        public void TextMessage(int n, PopupStyle style) { }
        public void DialogMessage(int n, bool eagle) => Dialogs.Add((n, eagle));
    }

    [Fact]
    public void DimFlickerAlternates()
    {
        var (w, r) = Reactor(13, 0);
        var s = w.Screen;
        int c = 0; // brightest source colour, so dim and undim differ
        for (int i = 1; i < 250; i++)
            if (s.Dac[i * 3] + s.Dac[i * 3 + 1] + s.Dac[i * 3 + 2] > s.Dac[c * 3] + s.Dac[c * 3 + 1] + s.Dac[c * 3 + 2]) c = i;
        var undimmed = new List<uint>();
        var dimmed = new List<uint>();
        for (int i = 1; i <= 16; i++)
        {
            w.Dispatch(r, Msg.Update, 0);
            (i % 8 == 0 ? dimmed : undimmed).Add(s.Palette[c]);
        }
        Assert.Equal(2, dimmed.Count);
        Assert.All(dimmed, d => Assert.Equal(dimmed[0], d));
        Assert.All(undimmed, u => Assert.Equal(undimmed[0], u));
        Assert.NotEqual(dimmed[0], undimmed[0]);
    }
}
