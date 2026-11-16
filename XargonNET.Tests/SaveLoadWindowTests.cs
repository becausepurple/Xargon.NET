using Xunit;
using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Ui;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>Task 10: loadsavewin (xargon.c:364-410). Expected images are composed independently of
/// SaveLoadWindow.Render from the literal coordinates/colours in the C (inside origin 112,24).</summary>
public sealed class SaveLoadWindowTests
{
    private const byte Fill = 17; // a colour the window never uses

    private static (VgaScreen screen, ShapeManager shapes, TextRenderer text) Fixture()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        return (screen, shapes, new TextRenderer(shapes));
    }

    private static string[] Names(params string[] some)
    {
        var n = new string[7];
        for (int i = 0; i < 7; i++) n[i] = i < some.Length ? some[i] : "";
        return n;
    }

    /// <summary>The C, line by line, with absolute coordinates.</summary>
    private static VgaScreen Expected(ShapeManager shapes, TextRenderer text, string title, string[] names,
        string blank, int cursorRow, int glyph)
    {
        var s = new VgaScreen();
        s.Clear(Fill);
        Window.Def(13, 16, 6, 7, 0, 0, WindowStyle.Textbox).Draw(s, shapes);   // defwin(&menu_win,13,16,6,7,0,0,textbox)
        var vp = new Viewport(112, 24, 96, 112);
        text.Draw(s, vp, 12, 4, 1, 7, -1, title);
        text.Draw(s, vp, 0, 8, 1, 4, -1, "____________");
        text.Draw(s, vp, 0, 84, 1, 4, -1, "____________");
        for (int c = 0; c < 7; c++) text.Draw(s, vp, 16, 20 + c * 10, 2, 3, -1, (c + 1).ToString());
        for (int c = 0; c < 7; c++) text.Draw(s, vp, 28, 20 + c * 10, 2, 6, -1, names[c].Length != 0 ? names[c] : blank);
        text.Draw(s, vp, 6, 100, 2, 3, -1, "<ESC>");
        text.Draw(s, vp, 42, 100, 2, 3, -1, "TO ABORT");
        for (int c = 0; c < 11; c++) text.Draw(s, vp, 4, 20 + c * 6, 2, 8, 1, " ");
        if (cursorRow >= 0) text.Draw(s, vp, 4, 20 + cursorRow * 10, 2, 8, 1, ((char)glyph).ToString());
        return s;
    }

    private static VgaScreen Actual(ShapeManager shapes, TextRenderer text, string title, string[] names,
        string blank, int cursorRow, int glyph)
    {
        var s = new VgaScreen();
        s.Clear(Fill);
        SaveLoadWindow.Render(s, shapes, text, title, names, blank, cursorRow, glyph);
        return s;
    }

    private static void AssertSame(VgaScreen exp, VgaScreen act)
    {
        for (int i = 0; i < exp.Pixels.Length; i++)
            if (exp.Pixels[i] != act.Pixels[i])
                Assert.Fail($"pixel ({i % VgaScreen.Width},{i / VgaScreen.Width}): expected {exp.Pixels[i]} got {act.Pixels[i]}");
    }

    [Fact]
    public void GeometryIsBorder104_16_112x128_Inside112_24_96x112()
    {
        var w = SaveLoadWindow.Frame();
        Assert.Equal((104, 16, 112, 128), (w.Border.X, w.Border.Y, w.Border.W, w.Border.H));
        Assert.Equal((112, 24, 96, 112), (w.Inside.X, w.Inside.Y, w.Inside.W, w.Inside.H));
    }

    [Theory]
    [InlineData("LOAD GAME", "<empty>", 0, 4)]
    [InlineData("SAVE GAME", "", 3, 8)]
    [InlineData("LOAD GAME", "<empty>", 6, 1)]
    public void RenderMatchesTheCForNamedAndBlankSlots(string title, string blank, int row, int glyph)
    {
        var (_, shapes, text) = Fixture();
        var names = Names("ALPHA", "", "THIRD", "", "", "SIX", "LAST");
        AssertSame(Expected(shapes, text, title, names, blank, row, glyph),
                   Actual(shapes, text, title, names, blank, row, glyph));
    }

    [Fact]
    public void BlankTextShowsOnlyForEmptyNames()
    {
        var (_, shapes, text) = Fixture();
        var withBlank = Actual(shapes, text, "LOAD GAME", Names("A"), "<empty>", -1, 0);
        var withoutBlank = Actual(shapes, text, "LOAD GAME", Names("A"), "", -1, 0);
        // Slot 1's name cell (28,20+10 inside) differs; slot 0's (a name) is identical in both.
        bool slot0Same = true, slot1Same = true;
        for (int y = 0; y < 6; y++)
            for (int x = 0; x < 40; x++)
            {
                int p0 = (24 + 20 + y) * VgaScreen.Width + 112 + 28 + x;
                int p1 = (24 + 30 + y) * VgaScreen.Width + 112 + 28 + x;
                slot0Same &= withBlank.Pixels[p0] == withoutBlank.Pixels[p0];
                slot1Same &= withBlank.Pixels[p1] == withoutBlank.Pixels[p1];
            }
        Assert.True(slot0Same);
        Assert.False(slot1Same);
    }

    [Fact]
    public void NothingIsDrawnOutsideTheBorderRectangle()
    {
        var (_, shapes, text) = Fixture();
        var s = Actual(shapes, text, "SAVE GAME", Names("123456789", "ABCDEFGHI"), "", 2, 5);
        for (int y = 0; y < VgaScreen.Height; y++)
            for (int x = 0; x < VgaScreen.Width; x++)
            {
                bool inside = x >= 104 && x < 216 && y >= 16 && y < 144;
                if (!inside) Assert.True(s.Pixels[y * VgaScreen.Width + x] == Fill, $"({x},{y}) was touched");
            }
    }

    [Fact]
    public void NineCharacterNameStaysInsideTheWindow()
    {
        var (_, shapes, text) = Fixture();
        // 28 + 9*6 = 82 < 96: the last glyph ends well before the 96-px inside width, so everything right of it
        // (to the border) is the same as for an empty list.
        string w9 = "WWWWWWWWW";
        var full = Actual(shapes, text, "SAVE GAME", Names(w9, w9, w9, w9, w9, w9, w9), "", -1, 0);
        var empty = Actual(shapes, text, "SAVE GAME", Names(), "", -1, 0);
        for (int y = 16; y < 144; y++)
            for (int x = 112 + 82; x < 216; x++)
                Assert.True(full.Pixels[y * VgaScreen.Width + x] == empty.Pixels[y * VgaScreen.Width + x], $"({x},{y})");
        // and the name itself is visible: some pixel in its span differs from the empty render
        bool differs = false;
        for (int x = 112 + 28; x < 112 + 82; x++)
            differs |= full.Pixels[(24 + 20 + 2) * VgaScreen.Width + x] != empty.Pixels[(24 + 20 + 2) * VgaScreen.Width + x];
        Assert.True(differs);
    }

    [Fact]
    public void CursorStripIsSixPixelsWideAndColourOne()
    {
        var (_, shapes, text) = Fixture();
        var s = Actual(shapes, text, "LOAD GAME", Names(), "<empty>", -1, 0);
        // 11 single-space cells of 6x6 at inside (4,20+c*6): x 116..121, y 44..109, back colour 1.
        for (int y = 44; y < 44 + 66; y++)
            for (int x = 116; x < 122; x++)
                Assert.True(s.Pixels[y * VgaScreen.Width + x] == 1, $"({x},{y}) = {s.Pixels[y * VgaScreen.Width + x]}");
        Assert.NotEqual((byte)1, s.Pixels[44 * VgaScreen.Width + 122]);
        Assert.NotEqual((byte)1, s.Pixels[44 * VgaScreen.Width + 115]);
    }

    [Fact]
    public void CursorGlyphIsTwoToneYellowOnColourOneAtTheCurrentRow()
    {
        var (_, shapes, text) = Fixture();
        var s = Actual(shapes, text, "LOAD GAME", Names(), "<empty>", 4, 3);
        var seen = new HashSet<byte>();
        for (int y = 24 + 20 + 40; y < 24 + 20 + 40 + 6; y++)
            for (int x = 116; x < 122; x++) seen.Add(s.Pixels[y * VgaScreen.Width + x]);
        Assert.Contains((byte)1, seen);                    // back
        Assert.True(seen.Contains(6) || seen.Contains(14)); // fontcolor 8 = two-tone 6 / 14
        // another row carries no glyph
        var other = new HashSet<byte>();
        for (int y = 24 + 20 + 10; y < 24 + 20 + 16; y++)
            for (int x = 116; x < 122; x++) other.Add(s.Pixels[y * VgaScreen.Width + x]);
        Assert.Equal(new HashSet<byte> { 1 }, other);
    }

    [Fact]
    public void CursorGlyphCyclesOneToEightStartingAtFourLikeTheC()
    {
        // c is 11 after the strip loop, so the first glyph is (11&7)+1 = 4; then (c&7)+1 each iteration.
        var seq = Enumerable.Range(0, 9).Select(SaveLoadWindow.CursorGlyphAt).ToArray();
        Assert.Equal(new[] { 4, 5, 6, 7, 8, 1, 2, 3, 4 }, seq);
        for (int t = 0; t < 100; t++) Assert.InRange(SaveLoadWindow.CursorGlyphAt(t), 1, 8);
    }

    // ---- SlotCursor: clamp, no wrap, throttle -------------------------------------------------

    [Fact]
    public void CursorClampsAtBothEndsWithoutWrapping()
    {
        var c = new SlotCursor();
        c.Move(-1, 100, repeat: false);
        Assert.Equal(0, c.Cur);
        for (int i = 0; i < 10; i++) c.Move(+1, 200 + i * 10, repeat: false);
        Assert.Equal(6, c.Cur);
        c.Move(+1, 400, repeat: false);
        Assert.Equal(6, c.Cur);
    }

    [Fact]
    public void FreshPressesAlwaysMoveByOne()
    {
        var c = new SlotCursor();
        c.Move(+1, 100, false); c.Move(+1, 100, false); c.Move(-1, 101, false);
        Assert.Equal(1, c.Cur);
    }

    [Fact]
    public void RepeatsMoveNoFasterThanEveryTwoTicks()
    {
        var c = new SlotCursor();
        Assert.True(c.Move(+1, 100, false));
        Assert.False(c.Move(+1, 100, true));   // same tick
        Assert.False(c.Move(+1, 101, true));   // abs(clock-moveclock) must be > 1
        Assert.True(c.Move(+1, 102, true));
        Assert.False(c.Move(+1, 103, true));
        Assert.True(c.Move(+1, 104, true));
        Assert.Equal(3, c.Cur);
    }

    [Fact]
    public void ZeroDeltaNeverMoves()
    {
        var c = new SlotCursor();
        Assert.False(c.Move(0, 5, false));
        Assert.Equal(0, c.Cur);
    }
}
