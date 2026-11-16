using Xunit;
using XargonNET.Assets;
using XargonNET.Ui;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>Task 4: winput edit buffer, cursor animation and drawing (windows.c:161-215).</summary>
public class TextInputTests
{
    private static TextInputState Fresh(string initial = "", int max = 8, bool escCommits = false) =>
        new(initial, max, escCommits);

    private static void Type(TextInputState s, string t) { foreach (char c in t) s.Press(c); }

    [Fact]
    public void TypingAppendsCharacters()
    {
        var s = Fresh();
        Type(s, "ABC");
        Assert.Equal("ABC", s.Text);
    }

    [Fact]
    public void NeverExceedsMaxLen()
    {
        var s = Fresh(max: 4);
        Type(s, "ABCDEFG");
        Assert.Equal("ABCD", s.Text);
    }

    [Fact]
    public void BackspaceRemovesLastAndIsNoopOnEmpty()
    {
        var s = Fresh();
        s.Backspace();
        Assert.Equal("", s.Text);
        Type(s, "AB");
        s.Backspace();
        Assert.Equal("A", s.Text);
        s.Backspace(); s.Backspace();
        Assert.Equal("", s.Text);
    }

    [Fact]
    public void FirstPrintableKeyClearsPrefilledText()
    {
        var s = Fresh("OLDNAME");
        Assert.True(s.FirstKeyClears);
        s.Press('X');
        Assert.Equal("X", s.Text);
        Assert.False(s.FirstKeyClears);
        s.Press('Y');
        Assert.Equal("XY", s.Text);
    }

    [Fact]
    public void FirstKeyBackspaceKeepsTextButConsumesFirstFlag()
    {
        // C: firstflag is cleared after every key, so Backspace first edits in place, then typing appends.
        var s = Fresh("OLD");
        s.Backspace();
        Assert.Equal("OL", s.Text);
        s.Press('Z');
        Assert.Equal("OLZ", s.Text);
    }

    [Fact]
    public void IgnoredFirstKeyAlsoConsumesFirstFlag()
    {
        var s = Fresh("OLD");
        s.Press('\u0001'); // not printable: ignored, but the key was still read by the C loop
        Assert.Equal("OLD", s.Text);
        s.Press('Z');
        Assert.Equal("OLDZ", s.Text);
    }

    [Fact]
    public void FirstPrintableKeyOnFullPrefillStillClears()
    {
        var s = Fresh("12345678", max: 8);
        s.Press('A');
        Assert.Equal("A", s.Text);
    }

    [Fact]
    public void EnterCommitsEscDependsOnMode()
    {
        Assert.Equal(TextInputOutcome.Committed, Fresh().Enter());
        Assert.Equal(TextInputOutcome.Cancelled, Fresh(escCommits: false).Escape());
        Assert.Equal(TextInputOutcome.Committed, Fresh(escCommits: true).Escape());
    }

    [Fact]
    public void PressNeverEndsTheInput()
    {
        Assert.Equal(TextInputOutcome.Editing, Fresh().Press('A'));
        Assert.Equal(TextInputOutcome.Editing, Fresh().Backspace());
    }

    [Theory]
    [InlineData('\0')]
    [InlineData('\t')]
    [InlineData('\r')]
    [InlineData('\u001f')]
    [InlineData('\u0080')]
    [InlineData('é')]
    [InlineData('€')]
    public void RejectsControlAndNonAscii(char c)
    {
        var s = Fresh("");
        s.Press(c);
        Assert.Equal("", s.Text);
    }

    [Fact]
    public void AcceptsExactly32Through127()
    {
        var s = Fresh(max: 200);
        s.Press(' '); s.Press('~'); s.Press('\u007f');
        Assert.Equal(" ~\u007f", s.Text);
        Assert.True(TextInputState.IsAccepted(32));
        Assert.True(TextInputState.IsAccepted(127));
        Assert.False(TextInputState.IsAccepted(31));
        Assert.False(TextInputState.IsAccepted(128));
    }

    [Fact]
    public void TextIsNotTrimmed()
    {
        var s = Fresh();
        Type(s, "A  ");
        Assert.Equal("A  ", s.Text);
        var sp = Fresh();
        Type(sp, "   ");
        Assert.Equal("   ", sp.Text); // all-space name is still non-empty in the C
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(7, 8)]
    [InlineData(8, 1)]
    [InlineData(9, 2)]
    [InlineData(16, 1)]
    public void CursorGlyphCyclesOneToEightPerTick(int tick, int glyph) =>
        Assert.Equal(glyph, TextInputBox.CursorGlyphAt(tick));

    // ---- drawing (real assets) ----

    private static (VgaScreen screen, TextRenderer text, Viewport vp) Setup()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var text = new TextRenderer(new ShapeManager(set.Graphics));
        screen.Clear(200);
        return (screen, text, new Viewport(20, 30, 100, 40));
    }

    private static byte[] Cell(VgaScreen s, int x, int y, int w = 6, int h = 6)
    {
        var r = new byte[w * h];
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) r[j * w + i] = s.Pixels[(y + j) * VgaScreen.Width + x + i];
        return r;
    }

    [Fact]
    public void RenderPutsTextAtViewportPositionWithBackground()
    {
        var (screen, text, vp) = Setup();
        TextInputBox.Render(screen, text, vp, 8, 4, TextRenderer.Font6, 7, 1, "AB", 8, 0);
        // 'A' at screen (28,34); background colour 1 fills glyph "3" pixels, text colour 7 present.
        var a = Cell(screen, 28, 34);
        Assert.Contains((byte)7, a);
        Assert.Contains((byte)1, a);
        // the cell right after the text ("AB" -> x = 8+12 = 20 -> screen 40) is a blank = pure background.
        Assert.All(Cell(screen, 40, 34), p => Assert.Equal((byte)1, p));
        // nothing drawn left of the field
        Assert.All(Cell(screen, 22, 34), p => Assert.Equal((byte)200, p));
    }

    [Fact]
    public void CursorGlyphAppearsAfterTextAndVanishesWhenOff()
    {
        var (screen, text, vp) = Setup();
        TextInputBox.Render(screen, text, vp, 8, 4, TextRenderer.Font6, 7, 1, "AB", 8, 3);
        var on = Cell(screen, 40, 34);
        Assert.Contains((byte)7, on); // glyph 3 has foreground pixels
        TextInputBox.Render(screen, text, vp, 8, 4, TextRenderer.Font6, 7, 1, "AB", 8, 0);
        Assert.All(Cell(screen, 40, 34), p => Assert.Equal((byte)1, p));
    }

    [Fact]
    public void CursorGlyphsDifferAcrossTheCycle()
    {
        var (screen, text, vp) = Setup();
        var cells = new List<byte[]>();
        for (int g = 1; g <= 8; g++)
        {
            TextInputBox.Render(screen, text, vp, 8, 4, TextRenderer.Font6, 7, 1, "", 8, g);
            cells.Add(Cell(screen, 28, 34));
        }
        Assert.True(cells.Select(c => Convert.ToHexString(c)).Distinct().Count() > 1);
    }

    [Fact]
    public void ShorterTextErasesPreviousLongerTextInField()
    {
        var (screen, text, vp) = Setup();
        TextInputBox.Render(screen, text, vp, 8, 4, TextRenderer.Font6, 7, 1, "ABC", 8, 0);
        TextInputBox.Render(screen, text, vp, 8, 4, TextRenderer.Font6, 7, 1, "A", 8, 0);
        Assert.All(Cell(screen, 8 + 20 + 12, 34), p => Assert.Equal((byte)1, p)); // where 'C' was
    }
}
