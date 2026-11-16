using Xunit;
using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Ui;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>Task 8: inv_win() (xargon.c:1001-1060). Expected images are composed independently of
/// InventoryWindow.Render from literal ids/positions read off the C.</summary>
public sealed class InventoryWindowTests
{
    private const byte Fill = 17; // a colour the window never uses, so untouched pixels are recognisable

    private static (VgaScreen screen, ShapeManager shapes, TextRenderer text) Fixture()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        return (screen, shapes, new TextRenderer(shapes));
    }

    private static byte Px(VgaScreen s, int x, int y) => s.Pixels[y * VgaScreen.Width + x];

    private static InventoryCounts Representative() => new(Laser: 3, Fire: 12, Rock: 0, MapKey: 2, Icon1: 1, Icon2: 0, Icon3: 0);

    private static void FillRect(VgaScreen s, int x, int y, int w, int h, byte c)
    {
        for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) s.Pixels[yy * VgaScreen.Width + xx] = c;
    }

    /// <summary>The C, line by line, with absolute coordinates (inside origin 48,24).</summary>
    private static VgaScreen Expected(ShapeManager shapes, TextRenderer text, InventoryCounts n, bool icon1, bool icon2, bool icon3)
    {
        var s = new VgaScreen();
        s.Clear(Fill);
        Window.Def(5, 16, 14, 7, 0, 0, WindowStyle.Textbox).Draw(s, shapes);
        var vp = new Viewport(48, 24, 224, 112);
        text.Draw(s, vp, 20, 4, 1, 7, -1, "INVENTORY STATUS WINDOW");
        text.Draw(s, vp, 20, 12, 1, 7, -1, "-----------------------");
        shapes.DrawShape(s, vp, 0x1e26, 5, 26);
        text.Draw(s, vp, 24, 25, 2, 6, -1, n.Laser.ToString());
        text.Draw(s, vp, 34, 25, 2, 4, -1, "LASER BULLETS");
        shapes.DrawShape(s, vp, 0x251c, 132, 20);
        text.Draw(s, vp, 152, 25, 2, 6, -1, n.Fire.ToString());
        text.Draw(s, vp, 162, 25, 2, 4, -1, "FIREBALLS");
        shapes.DrawShape(s, vp, 0x1e14, 4, 40);
        text.Draw(s, vp, 24, 45, 2, 6, -1, n.Rock.ToString());
        text.Draw(s, vp, 34, 45, 2, 4, -1, "ROCKS to throw");
        shapes.DrawShape(s, vp, 0x1e10, 132, 40);
        text.Draw(s, vp, 152, 45, 2, 6, -1, n.MapKey.ToString());
        text.Draw(s, vp, 162, 45, 2, 4, -1, "GATE KEYS");
        text.Draw(s, vp, 60, 60, 1, 7, -1, "POWER OBJECTS");
        for (int c = 0; c < 3; c++)
        {
            int x = 48 + 68 + 35 * c;
            FillRect(s, x, 24 + 72, 18, 18, 1);            // 18x18 box, colour 1
            FillRect(s, x + 18, 24 + 78, 6, 12, 248);      // right shadow (back 0 -> 248)
            FillRect(s, x + 6, 24 + 90, 18, 6, 248);       // bottom shadow
        }
        if (icon1) shapes.DrawShape(s, vp, 0x1e1f, 68, 72);
        if (icon2) shapes.DrawShape(s, vp, 0x1e20, 103, 72);
        if (icon3) shapes.DrawShape(s, vp, 0x1e21, 138, 72);
        shapes.DrawShape(s, vp, 0x1e40, 26, 61);
        shapes.DrawShape(s, vp, 0x1e40, 174, 61);
        text.Draw(s, vp, 69, 100, 1, 7, -1, "#1");
        text.Draw(s, vp, 104, 100, 1, 7, -1, "#2");
        text.Draw(s, vp, 139, 100, 1, 7, -1, "#3");
        return s;
    }

    private static VgaScreen Rendered(InventoryCounts n)
    {
        var (screen, shapes, text) = Fixture();
        screen.Clear(Fill);
        InventoryWindow.Render(screen, shapes, text, n);
        return screen;
    }

    [Fact]
    public void FrameGeometry()
    {
        var w = InventoryWindow.Frame();
        Assert.Equal((40, 16, 240, 128), (w.Border.X, w.Border.Y, w.Border.W, w.Border.H));
        Assert.Equal((48, 24, 224, 112), (w.Inside.X, w.Inside.Y, w.Inside.W, w.Inside.H));
    }

    [Fact]
    public void WholeImageMatchesIndependentComposition_Representative()
    {
        var n = Representative();
        var (_, shapes, text) = Fixture();
        var exp = Expected(shapes, text, n, true, false, false);
        Assert.Equal(exp.Pixels, Rendered(n).Pixels);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    public void PowerIconsDrawnOnlyWhenPresent(bool i1, bool i2, bool i3)
    {
        var n = new InventoryCounts(1, 2, 3, 4, i1 ? 1 : 0, i2 ? 1 : 0, i3 ? 1 : 0);
        var (_, shapes, text) = Fixture();
        Assert.Equal(Expected(shapes, text, n, i1, i2, i3).Pixels, Rendered(n).Pixels);
    }

    [Fact]
    public void IconCountGreaterThanOneStillDrawsOnce()
    {
        var one = Rendered(new InventoryCounts(1, 1, 1, 1, 1, 0, 0));
        var many = Rendered(new InventoryCounts(1, 1, 1, 1, 5, 0, 0));
        Assert.Equal(one.Pixels, many.Pixels);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(12)]
    [InlineData(123)]
    [InlineData(29)]
    public void CountDigitsAtPositionInColour6(int count)
    {
        var n = new InventoryCounts(count, count, count, count, 0, 0, 0);
        var (_, shapes, text) = Fixture();
        var exp = Expected(shapes, text, n, false, false, false);
        var got = Rendered(n);
        Assert.Equal(exp.Pixels, got.Pixels);

        // The digit cells (48+24.., 24+25..) hold colour 6 and no colour 4; the label cell holds 4 and no 6.
        // (only the first digit cell: from the 2nd digit on the count overlaps the label at x=34, as in the C)
        var digitCols = new HashSet<byte>();
        for (int y = 24 + 25; y < 24 + 31; y++) for (int x = 48 + 24; x < 48 + 24 + 6; x++) digitCols.Add(Px(got, x, y));
        Assert.Contains((byte)6, digitCols);
        Assert.DoesNotContain((byte)4, digitCols);
        var labelCols = new HashSet<byte>();
        for (int y = 24 + 25; y < 24 + 31; y++) for (int x = 48 + 24 + 6 * count.ToString().Length; x < 48 + 34 + 6 * 13; x++) labelCols.Add(Px(got, x, y));
        Assert.Contains((byte)4, labelCols);
        Assert.DoesNotContain((byte)6, labelCols);
    }

    [Fact]
    public void ZeroCountRendersZero()
    {
        var zero = Rendered(new InventoryCounts(0, 0, 0, 0, 0, 0, 0));
        var (screen, shapes, text) = Fixture();
        // Rock count "0" at (24,45) in colour 6.
        screen.Clear(Fill);
        text.Draw(screen, new Viewport(48, 24, 224, 112), 24, 45, 2, 6, -1, "0");
        for (int y = 24 + 45; y < 24 + 51; y++)
            for (int x = 48 + 24; x < 48 + 30; x++)
                if (Px(screen, x, y) != Fill) Assert.Equal(Px(screen, x, y), Px(zero, x, y));
        Assert.Contains((byte)6, Enumerable.Range(0, 36).Select(i => Px(zero, 48 + 24 + i % 6, 24 + 45 + i / 6)));
    }

    [Fact]
    public void SlotBoxesAndShadowsWhenAllAbsent()
    {
        var got = Rendered(new InventoryCounts(1, 1, 1, 1, 0, 0, 0));
        for (int c = 0; c < 3; c++)
        {
            int x = 48 + 68 + 35 * c, y = 24 + 72;
            for (int yy = 0; yy < 18; yy++) for (int xx = 0; xx < 18; xx++) Assert.Equal(1, Px(got, x + xx, y + yy));
            for (int yy = 6; yy < 18; yy++) for (int xx = 18; xx < 24; xx++) Assert.Equal(248, Px(got, x + xx, y + yy));
            for (int yy = 18; yy < 24; yy++) for (int xx = 6; xx < 24; xx++) Assert.Equal(248, Px(got, x + xx, y + yy));
        }
    }

    [Fact]
    public void PowerIconShapeIdsAreIconsTablePlus31To33()
    {
        Assert.Equal(0x1e00, KindDefs.Get(ObjectKind.Icons).ShapeTable * 256);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(-5)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void NothingDrawnOutsideTheWindow(int count)
    {
        var got = Rendered(new InventoryCounts(count, count, count, count, 1, 1, 1));
        for (int y = 0; y < VgaScreen.Height; y++)
            for (int x = 0; x < VgaScreen.Width; x++)
                if (x < 40 || x >= 280 || y < 16 || y >= 144) Assert.Equal(Fill, Px(got, x, y));
    }

    [Fact]
    public void CountsReadThroughInvCount()
    {
        var pl = PlayerState.NewGame();
        pl.Inv[pl.NumInv++] = Inv.Laser; pl.Inv[pl.NumInv++] = Inv.Fire; pl.Inv[pl.NumInv++] = Inv.Icon2;
        int Count(int id) { int c = 0; for (int i = 0; i < pl.NumInv; i++) if (pl.Inv[i] == id) c++; return c; }
        Assert.Equal(new InventoryCounts(2, 1, 0, 0, 0, 1, 0), InventoryCounts.From(Count));
    }

    [Fact]
    public void RestDismissalReusesPopupHelper()
    {
        Assert.True(PopupWindows.RestDismisses(PopupInput.Escape));
        Assert.True(PopupWindows.RestDismisses(PopupInput.Space));
        Assert.False(PopupWindows.RestDismisses(PopupInput.Other));
    }
}
