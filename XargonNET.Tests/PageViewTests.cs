using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Objects;
using Xunit;

namespace XargonNET.Tests;

public class PageViewTests
{
    private static Obj Anchor(int xd = 0, int yd = 0, int counter = 0, int x = 0, int y = 0) =>
        new() { Kind = ObjectKind.Checkpt, Xd = xd, Yd = yd, Counter = counter, X = x, Y = y };

    private static Board Story(int episode) =>
        AssetSet.Mount(AssetSet.FindAssetsDir(), episode).LoadBoard("STORY");

    // ---- navigation table (xargon.c pageview) ----

    [Fact]
    public void FirstPageCannotGoBack() =>
        Assert.Equal(PageNav.Stay, PageView.Navigate(Anchor(xd: 1), 0, PageKey.Back));

    [Fact]
    public void MiddlePageBackAndForward()
    {
        var a = Anchor();
        Assert.Equal(PageNav.GoTo(4), PageView.Navigate(a, 5, PageKey.Back));
        Assert.Equal(PageNav.GoTo(6), PageView.Navigate(a, 5, PageKey.Forward));
    }

    [Fact]
    public void LastPageForwardExitsBackStillWorks()
    {
        var a = Anchor(xd: 2);
        Assert.Equal(PageNav.Exit, PageView.Navigate(a, 9, PageKey.Forward));
        Assert.Equal(PageNav.GoTo(8), PageView.Navigate(a, 9, PageKey.Back));
    }

    [Fact]
    public void EscapeAlwaysExits()
    {
        Assert.Equal(PageNav.Exit, PageView.Navigate(Anchor(), 3, PageKey.Escape));
        Assert.Equal(PageNav.Exit, PageView.Navigate(Anchor(xd: 1), 0, PageKey.Escape));
    }

    [Fact]
    public void SinglePageAnchorForwardExitsBackStays()
    {
        var a = Anchor(yd: 1);
        Assert.Equal(PageNav.Exit, PageView.Navigate(a, 15, PageKey.Forward));
        Assert.Equal(PageNav.Stay, PageView.Navigate(a, 15, PageKey.Back));
    }

    [Fact]
    public void FirstPageForwardAdvances() =>
        Assert.Equal(PageNav.GoTo(1), PageView.Navigate(Anchor(xd: 1), 0, PageKey.Forward));

    // ---- FindAnchor ----

    [Fact]
    public void Ep1StoryAnchorsMatchSpec()
    {
        var b = Story(1);
        var expect = new (int page, int x, int y)[]
        {
            (0, 352, 0), (1, 688, 0), (2, 1024, 0), (3, 1360, 0), (4, 1696, 0),
            (5, 16, 224), (6, 352, 224), (7, 688, 224), (8, 1024, 224), (9, 1360, 224),
        };
        foreach (var (page, x, y) in expect)
        {
            var a = PageView.FindAnchor(b, page);
            Assert.NotNull(a);
            Assert.Equal((x, y), (a!.X, a.Y));
        }
        Assert.Equal(1, PageView.FindAnchor(b, 0)!.Xd);
        Assert.Equal(2, PageView.FindAnchor(b, 9)!.Xd);
        Assert.Null(PageView.FindAnchor(b, 11));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Ep2And3StoryHaveThreePages(int ep)
    {
        var b = Story(ep);
        for (int p = 0; p <= 2; p++) Assert.NotNull(PageView.FindAnchor(b, p));
        Assert.Null(PageView.FindAnchor(b, 3));
    }

    [Fact]
    public void Ep1IntroHasCreditsPages()
    {
        var b = AssetSet.Mount(AssetSet.FindAssetsDir(), 1).LoadBoard("INTRO");
        Assert.NotNull(PageView.FindAnchor(b, 20));
        Assert.NotNull(PageView.FindAnchor(b, 21));
    }

    [Fact]
    public void LastMatchingAnchorWins()
    {
        var b = new Board();
        b.Objects.Add(Anchor(counter: 7, x: 10, y: 10));
        b.Objects.Add(new Obj { Kind = ObjectKind.Emerald, Counter = 7, X = 5 });
        b.Objects.Add(Anchor(counter: 7, x: 20, y: 30));
        b.Objects.Add(Anchor(counter: 8, x: 99, y: 99));
        var a = PageView.FindAnchor(b, 7);
        Assert.Equal((20, 30), (a!.X, a.Y));
    }
}
