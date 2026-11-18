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
        int cp = w.AddObj(ObjectKind.Checkpt, 200, 120, 0, 0);
        w.Obj(cp).Counter = 33;
        for (int i = 0; i < 59; i++) w.Dispatch(r, Msg.Update, 0);
        Assert.Null(w.TakeEscWait());
        w.Dispatch(r, Msg.Update, 0);
        Assert.Null(w.NewLevel);
        var cont = w.TakeEscWait();
        Assert.NotNull(cont);
        Assert.Null(w.NewLevel);
        cont!();
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
        cont!();
        Assert.True(w.GameWon);
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
    public void EscRequestedOncePerSequence()
    {
        var (w, r) = Reactor(13, 1, 2);
        for (int i = 0; i < 80; i++) w.Dispatch(r, Msg.Update, 0);
        Assert.NotNull(w.TakeEscWait());
        Assert.Null(w.TakeEscWait());
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
