using Xunit;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>msg_score draws its digits (font-flagged shape table 3, pixel values 1/2/3) under
/// fontcolor(&amp;gamevp, 2 + (counter &amp; 3), -1): pixels 1 and 2 become the colour, 3 is transparent.</summary>
public class ScoreColourTests
{
    private sealed record ScoreDraw(List<HashSet<int>> PerDigit);

    private const int Back = 200; // sentinel the digit pixels are drawn over (not a palette value the shapes use)

    private static ScoreDraw DrawScore(int state, int counter)
    {
        var w = TestWorld.Load(1, "BOARD_01");
        int n = w.AddObj(ObjectKind.Score, 200, 100, 0, 0);
        w.Obj(n).State = state; w.Obj(n).Counter = counter;
        var vp = new Viewport(0, 2, 320, 158) { OriginX = 150, OriginY = 50 };
        w.Screen.Clear(Back);
        w.DrawFore(w.Screen, vp);
        // object at world (200,100) -> screen (50, 52); 8px per digit, 8 tall
        int len = state.ToString().Length;
        var perDigit = new List<HashSet<int>>();
        for (int c = 0; c < len; c++)
        {
            var set = new HashSet<int>();
            for (int y = 52; y < 60; y++)
                for (int x = 50 + 8 * c; x < 58 + 8 * c; x++)
                {
                    int v = w.Screen.Pixels[y * VgaScreen.Width + x];
                    if (v != Back) set.Add(v);
                }
            perDigit.Add(set);
        }
        return new ScoreDraw(perDigit);
    }

    [Theory]
    [InlineData(30, 4)]
    [InlineData(29, 3)]
    [InlineData(28, 2)]
    [InlineData(27, 5)]
    public void DigitsUseCycleColour(int counter, int expected)
    {
        Assert.Equal(expected, 2 + (counter & 3));
        var d = DrawScore(500, counter);
        Assert.Equal(3, d.PerDigit.Count);
        foreach (var set in d.PerDigit)
            Assert.Equal(new HashSet<int> { expected }, set);
    }

    [Fact]
    public void MultiDigitScoreSharesOneColour()
    {
        var d = DrawScore(1000, 29);
        Assert.Equal(4, d.PerDigit.Count);
        foreach (var set in d.PerDigit) Assert.Equal(new HashSet<int> { 3 }, set);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12345)]
    public void EdgeScoresDoNotThrow(int state)
    {
        var d = DrawScore(state, 10);
        foreach (var set in d.PerDigit) Assert.Equal(new HashSet<int> { 4 }, set);
    }
}
