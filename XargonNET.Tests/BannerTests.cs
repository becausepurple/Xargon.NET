using System.Linq;
using XargonNET.Engine;
using XargonNET.Text;
using Xunit;

namespace XargonNET.Tests;

/// <summary>Task 8: level banner layout (putlevelmsg) and dismissal logic (donelevelmsg). The SDL wait
/// loop itself needs a real window; the decisions are pure and tested here.</summary>
public class BannerTests
{
    [Fact]
    public void Ep1Level3LaysOutFiveCentredLinesColour7()
    {
        var lines = PopupWindows.BannerLayout(GameStrings.LevelText(1, 3));
        Assert.Equal(5, lines.Count);
        Assert.All(lines, l => Assert.Equal(7, l.Color));
        Assert.Equal("Malvineous enters", lines[0].Text);
        Assert.Equal((152 - 6 * 17) / 2, lines[0].X);
        Assert.Equal(25, lines[0].X);
        Assert.Equal(new[] { 20, 26, 32, 38, 44 }, lines.Select(l => l.Y).ToArray());
        Assert.Equal("", lines[1].Text);
        Assert.Equal("the forest of", lines[2].Text);
        Assert.Equal((152 - 6 * 13) / 2, lines[2].X);
    }

    [Fact]
    public void LevelOutOfRangeOrNullDrawsNothing()
    {
        Assert.Empty(PopupWindows.BannerLayout(GameStrings.LevelText(1, 16)));
        Assert.Empty(PopupWindows.BannerLayout(GameStrings.LevelText(1, -1)));
        Assert.Empty(PopupWindows.BannerLayout(null));
    }

    [Fact]
    public void EmptyStringDrawsZeroLines() => Assert.Empty(PopupWindows.BannerLayout(GameStrings.LevelText(1, 11)));

    [Fact]
    public void EmbeddedColourDigitIsHonoured()
    {
        var l = PopupWindows.BannerLayout("3red\rplain\r");
        Assert.Equal(3, l[0].Color);
        Assert.Equal("red", l[0].Text);
        Assert.Equal(7, l[1].Color);
    }

    [Fact]
    public void EveryNonEmptyLevelTextHasFiveLines()
    {
        for (int ep = 1; ep <= 3; ep++)
            for (int lv = 0; lv < 16; lv++)
            {
                var t = GameStrings.LevelText(ep, lv)!;
                int n = PopupWindows.BannerLayout(t).Count;
                Assert.Equal(t.Length == 0 ? 0 : 5, n);
            }
    }

    [Theory]
    [InlineData(0, true, false, false, true)]   // Esc at once
    [InlineData(0, false, true, false, true)]   // Enter at once
    [InlineData(1, false, false, true, false)]  // key before dt 2: ignored
    [InlineData(2, false, false, true, true)]   // key at dt 2
    [InlineData(3, false, false, false, false)] // nothing, not yet 4
    [InlineData(4, false, false, false, true)]  // auto at dt 4
    public void DismissTruthTable(int dt, bool esc, bool enter, bool any, bool expected)
        => Assert.Equal(expected, PopupWindows.BannerDismiss(dt, esc, enter, any));

    [Fact]
    public void DtIsTicksDividedByTen()
    {
        Assert.Equal(0, PopupWindows.BannerDt(100, 109));
        Assert.Equal(2, PopupWindows.BannerDt(100, 120));
        Assert.Equal(4, PopupWindows.BannerDt(100, 140));
    }
}
