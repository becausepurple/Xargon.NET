using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Video;
using Xunit;

namespace XargonNET.Tests;

public class GameFlowTests
{
    [Fact]
    public void WonShowsWinPagesThenScores() =>
        Assert.Equal(new EndSteps(true, true), GameFlow.Plan(GameOver.Won, demo: false));

    [Fact]
    public void QuitShowsScoresOnly() =>
        Assert.Equal(new EndSteps(false, true), GameFlow.Plan(GameOver.Quit, demo: false));

    [Fact]
    public void DemoAndNoneShowNothing()
    {
        Assert.Equal(new EndSteps(false, false), GameFlow.Plan(GameOver.Quit, demo: true));
        Assert.Equal(new EndSteps(false, false), GameFlow.Plan(GameOver.Won, demo: true));
        Assert.Equal(new EndSteps(false, false), GameFlow.Plan(GameOver.None, demo: false));
    }

    private static ObjectManager Objects()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        return new LevelSession(n => set.LoadBoard(n.ToUpperInvariant()), new ShapeManager(set.Graphics), set.Info,
            screen, new Viewport(0, 2, VgaScreen.Width, 158), PlayerState.NewGame()).Load("MAP").Objects;
    }

    [Fact]
    public void GameWonRaisesWonExactlyOnce()
    {
        var o = Objects();
        Assert.Equal(GameOver.None, GameFlow.PollGameWon(o));
        o.GameWon = true;
        Assert.Equal(GameOver.Won, GameFlow.PollGameWon(o));
        Assert.False(o.GameWon);
        Assert.Equal(GameOver.None, GameFlow.PollGameWon(o));
    }

    [Theory]
    [InlineData("won", GameOver.Won, GameFlow.DebugDefaultTicks)]
    [InlineData("QUIT", GameOver.Quit, GameFlow.DebugDefaultTicks)]
    [InlineData(" won@5 ", GameOver.Won, 5)]
    [InlineData("quit@0", GameOver.Quit, 0)]
    public void DebugEnvParses(string v, GameOver reason, int ticks) =>
        Assert.Equal(new DebugGameOver(reason, ticks), GameFlow.ParseDebug(v));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("none")]
    [InlineData("won@x")]
    [InlineData("won@-1")]
    [InlineData("1")]
    public void DebugEnvRejectsGarbage(string? v) => Assert.Null(GameFlow.ParseDebug(v));

    [Theory]
    [InlineData(1, "BOARD_33", 200, 206)]
    [InlineData(2, "BOARD_32", 200, 201)]
    [InlineData(3, "BOARD_32", 200, 206)]
    public void WinPagesFoundOnFinalBoards(int ep, string name, int first, int last)
    {
        var b = AssetSet.Mount(AssetSet.FindAssetsDir(), ep).LoadBoard(name);
        Assert.NotNull(GameFlow.WinAnchor(b));
        Assert.Same(PageView.FindAnchor(b, GameFlow.WinPage), GameFlow.WinAnchor(b));
        for (int p = first; p <= last; p++) Assert.NotNull(PageView.FindAnchor(b, p));
    }

    [Fact]
    public void MissingWinPageIsNullNotAThrow()
    {
        var b = AssetSet.Mount(AssetSet.FindAssetsDir(), 1).LoadBoard("MAP");
        Assert.Null(GameFlow.WinAnchor(b));
        Assert.Null(GameFlow.WinAnchor(new Board()));
    }
}
