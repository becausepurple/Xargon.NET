using XargonNET.Assets;
using XargonNET.Ui;
using XargonNET.Video;
using Xunit;

namespace XargonNET.Tests;

public class WindowTests
{
    private static (VgaScreen screen, ShapeManager shapes, TextRenderer text) Fixture()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        return (screen, shapes, new TextRenderer(shapes));
    }

    private static void AssertVp(Viewport v, int x, int y, int w, int h)
    {
        Assert.Equal((x, y, w, h), (v.X, v.Y, v.W, v.H));
    }

    private static byte Px(VgaScreen s, int x, int y) => s.Pixels[y * VgaScreen.Width + x];

    [Fact]
    public void PlainGeometry()
    {
        var w = Window.Def(6, 18, 13, 6, 0, 0, WindowStyle.Plain);
        AssertVp(w.Border, 48, 18, 224, 124);
        AssertVp(w.Inside, 56, 34, 208, 96);
    }

    [Fact]
    public void DialogPortraitGeometry()
    {
        var w = Window.Def(2, 16, 14, 4, 4, 0, WindowStyle.Dialog);
        AssertVp(w.Border, 16, 16, 240, 92);
        AssertVp(w.Inside, 96, 32, 152, 64);
        AssertVp(w.TopLeft, 24, 32, 64, 64);
    }

    [Fact]
    public void DialogPortraitGeometryAt8()
    {
        var w = Window.Def(8, 52, 14, 4, 4, 0, WindowStyle.Dialog);
        Assert.Equal(64, w.Border.X);
        Assert.Equal(144, w.Inside.X);
        Assert.Equal((72, 68), (w.TopLeft.X, w.TopLeft.Y));
    }

    [Fact]
    public void PlainPortraitGeometry()
    {
        var w = Window.Def(5, 36, 14, 4, 4, 0, WindowStyle.Plain);
        Assert.Equal((40, 36), (w.Border.X, w.Border.Y));
        Assert.Equal((120, 52), (w.Inside.X, w.Inside.Y));
    }

    [Fact]
    public void TextboxGeometryUnchanged()
    {
        var a = Window.DefTextbox(8, 24, 11, 5);
        var b = Window.Def(8, 24, 11, 5, 0, 0, WindowStyle.Textbox);
        AssertVp(a.Border, 64, 24, 192, 96);
        AssertVp(a.Inside, 72, 32, 176, 80);
        AssertVp(b.Border, 64, 24, 192, 96);
        AssertVp(b.Inside, 72, 32, 176, 80);
    }

    [Theory]
    [InlineData(WindowStyle.Plain)]
    [InlineData(WindowStyle.Dialog)]
    [InlineData(WindowStyle.Textbox)]
    public void DrawPaintsFrameCornersAndEdges(WindowStyle style)
    {
        var (screen, shapes, _) = Fixture();
        screen.Clear(10);
        var w = Window.Def(6, 18, 13, 6, 0, 0, style);
        w.Draw(screen, shapes);
        // Border clear to 248 happens first; frame shapes must overwrite it somewhere on each side.
        int bx = w.Border.X, by = w.Border.Y, bw = w.Border.W, bh = w.Border.H;
        int Count(int x0, int y0, int ww, int hh)
        {
            int n = 0;
            for (int y = y0; y < y0 + hh; y++)
                for (int x = x0; x < x0 + ww; x++) if (Px(screen, x, y) != 248) n++;
            return n;
        }
        Assert.True(Count(bx, by, 8, 8) > 0, "top-left corner");
        Assert.True(Count(bx + bw - 8, by, 8, 8) > 0, "top-right corner");
        Assert.True(Count(bx, by + bh - 8, 8, 8) > 0, "bottom-left corner");
        Assert.True(Count(bx + bw - 8, by + bh - 8, 8, 8) > 0, "bottom-right corner");
        Assert.True(Count(bx + 40, by, 16, 8) > 0, "top edge");
        Assert.True(Count(bx, by + 40, 8, 16) > 0, "left edge");
        // Nothing outside the border viewport changed.
        Assert.Equal(10, Px(screen, bx - 1, by));
        Assert.Equal(10, Px(screen, bx + bw, by));
    }

    // Expected (id, x, y) placements written out independently from WINDOWS.C drawwin (plain 0x44xx / dialog 0x47xx),
    // for a window with xl16=14, yl16=4, v16=2 and h16=4 (portrait) or 0: winxl=224, winyl=64, winh=64.
    // The top/bottom edge at c==h16 and the left/divider edge at c==v16 are skipped by the C conditions.
    private static List<(int id, int x, int y)> ExpectedFrame(int b, bool portrait)
    {
        var l = new List<(int id, int x, int y)>
        {
            (b + 2, 0, 0),
            (b + 7, 232, 80),
            (b + 5, 0, 80),
        };
        for (int c = 0; c < 14; c++)
            if (c == 0 || c != (portrait ? 4 : 0))
            {
                l.Add((b + 4, c * 16 + 8, 0));
                l.Add((b + 6, c * 16 + 8, 80));
            }
        l.Add((b + 3, 232, 0));
        for (int c = 0; c < 4; c++)
        {
            l.Add((b + 1, 232, 16 * c + 16));
            if (c == 0 || c != 2) l.Add((b + 0, 0, c * 16 + 16));
        }
        if (portrait)
        {
            l.Add((b + 10, 72, 0));
            for (int c = 0; c < 4; c++)
                if (c == 0 || c != 2) l.Add((b + 9, 72, c * 16 + 16));
            l.Add((b + 8, 72, 80));
        }
        return l;
    }

    [Theory]
    [InlineData(WindowStyle.Plain, 0x4400, false)]
    [InlineData(WindowStyle.Plain, 0x4400, true)]
    [InlineData(WindowStyle.Dialog, 0x4700, false)]
    [InlineData(WindowStyle.Dialog, 0x4700, true)]
    public void FramePiecesMatchDirectShapePlacements(WindowStyle style, int b, bool portrait)
    {
        var (screen, shapes, _) = Fixture();
        screen.Clear(248);
        var w = Window.Def(2, 16, 14, 4, portrait ? 4 : 0, 2, style);
        w.Draw(screen, shapes);

        var list = ExpectedFrame(b, portrait);
        var expected = new VgaScreen();
        expected.Clear(248);
        foreach (var (id, x, y) in list)
            shapes.DrawShape(expected, w.Border, id, x, y);
        Assert.Equal(expected.Pixels, screen.Pixels);

        // Every piece must be individually observable: omitting any one placement changes the image
        // (guards against a piece being fully masked by overlap and so left unpinned by the whole-image compare).
        for (int skip = 0; skip < list.Count; skip++)
        {
            var e2 = new VgaScreen();
            e2.Clear(248);
            for (int i = 0; i < list.Count; i++)
                if (i != skip) shapes.DrawShape(e2, w.Border, list[i].id, list[i].x, list[i].y);
            Assert.False(e2.Pixels.AsSpan().SequenceEqual(screen.Pixels), $"piece {skip} {list[skip]} not observable");
        }
    }

    [Fact]
    public void PlainAndDialogFramesDiffer()
    {
        var (s1, shapes, _) = Fixture();
        var (s2, _, _) = Fixture();
        Window.Def(6, 18, 13, 6, 0, 0, WindowStyle.Plain).Draw(s1, shapes);
        Window.Def(6, 18, 13, 6, 0, 0, WindowStyle.Dialog).Draw(s2, shapes);
        Assert.NotEqual(s1.Pixels, s2.Pixels);
    }

    [Theory]
    [InlineData(WindowStyle.Plain)]
    [InlineData(WindowStyle.Dialog)]
    public void PortraitDividerColumnDrawn(WindowStyle style)
    {
        var (screen, shapes, _) = Fixture();
        var w = Window.Def(2, 16, 14, 4, 4, 0, style);
        w.Draw(screen, shapes);
        // Divider shapes 0x4709/0x470a sit at border x = winh+8 = 72 -> screen x 88..95; inside begins at 96.
        int x0 = w.Border.X + 72;
        int drawn = 0;
        for (int y = w.Inside.Y; y < w.Inside.Y + w.Inside.H; y++)
            for (int x = x0; x < x0 + 8; x++) if (Px(screen, x, y) != 248) drawn++;
        Assert.True(drawn > 0);
        // A no-portrait window has nothing there.
        var (s2, _, _) = Fixture();
        var plain = Window.Def(2, 16, 14, 4, 0, 0, style);
        plain.Draw(s2, shapes);
        int none = 0;
        for (int y = plain.Inside.Y; y < plain.Inside.Y + plain.Inside.H; y++)
            for (int x = x0; x < x0 + 8; x++) if (Px(s2, x, y) != 248) none++;
        Assert.Equal(0, none);
    }

    [Fact]
    public void ClearInsideFillsExactlyInside()
    {
        var (screen, shapes, _) = Fixture();
        screen.Clear(10);
        var w = Window.Def(2, 16, 14, 4, 4, 0, WindowStyle.Dialog);
        w.ClearInside(screen, 121);
        for (int y = 0; y < VgaScreen.Height; y++)
            for (int x = 0; x < VgaScreen.Width; x++)
            {
                bool inside = x >= w.Inside.X && x < w.Inside.X + w.Inside.W && y >= w.Inside.Y && y < w.Inside.Y + w.Inside.H;
                Assert.Equal(inside ? 121 : 10, Px(screen, x, y));
            }
    }

    [Theory]
    [InlineData("HELLO", false, 16 + (224 + 0) / 2 - 20)]
    [InlineData("HELLO", true, 16 + (224 + 0) / 2 - 20 - 32)]
    public void TitleXMatchesC(string t, bool off, int expected)
    {
        var w = Window.Def(6, 18, 14, 6, 0, 0, WindowStyle.Plain);
        Assert.Equal(expected, Window.TitleX(w.WinXl, w.WinH, t.Length, off));
    }

    [Fact]
    public void TitleXUsesPortraitWidth()
    {
        Assert.Equal(16 + (224 + 64) / 2 - 4 * 3, Window.TitleX(224, 64, 3, false));
    }

    [Fact]
    public void TitleWinDrawsInBorderAtY4()
    {
        var (screen, shapes, text) = Fixture();
        var w = Window.Def(6, 18, 14, 6, 0, 0, WindowStyle.Plain);
        w.Draw(screen, shapes);
        var before = (byte[])screen.Pixels.Clone();
        w.TitleWin(screen, text, "HELLO", 15, false);
        int x0 = w.Border.X + Window.TitleX(w.WinXl, w.WinH, 5, false);
        bool changed = false;
        for (int y = w.Border.Y + 4; y < w.Border.Y + 12; y++)
            for (int x = x0; x < x0 + 40; x++)
                if (screen.Pixels[y * VgaScreen.Width + x] != before[y * VgaScreen.Width + x]) changed = true;
        Assert.True(changed);
    }
}
