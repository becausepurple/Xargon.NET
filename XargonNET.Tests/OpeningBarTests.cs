using Xunit;
using XargonNET.Assets;
using XargonNET.Ui;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>The opening/menu screen's bottom display: <c>statwin(0)</c> in xargon.c:130-163.</summary>
public class OpeningBarTests
{
    private static (VgaScreen screen, ShapeManager shapes, TextRenderer text) Rig(int episode = 1)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        return (screen, shapes, new TextRenderer(shapes));
    }

    private static void Fill(VgaScreen s, byte c) => Array.Fill(s.Pixels, c);

    [Fact]
    public void OpeningBarMatchesStatwinZero()
    {
        var (screen, shapes, text) = Rig();
        Fill(screen, 200); // sentinel so we can see exactly what gets drawn
        Hud.DrawOpening(screen, shapes, text);

        // Expected, written out from the C: clrvp(&statvp,121); ten art pieces 0x2100+n at x=32n in statvp
        // (0,160,320x28); the frame pieces at y=188; then textvp (12,190,296x8) cleared to 121 with the
        // 18-character caption in font 2 (6x6), colour 6, at x = 148-3*18.
        var (screen2, _, _) = Rig();
        Fill(screen2, 200);
        var statvp = new Viewport(0, 160, 320, 28);
        var mainvp = new Viewport(0, 0, 320, 200);
        var textvp = new Viewport(12, 190, 296, 8);
        for (int y = 160; y < 188; y++)
            for (int x = 0; x < 320; x++) screen2.Pixels[y * 320 + x] = 121;
        int n = 0;
        for (int x = 0; x < 289; x += 32) shapes.DrawShape(screen2, statvp, 0x2100 + n++, x, 0);
        shapes.DrawShape(screen2, mainvp, 0x1e0a, 0, 188);
        for (int x = 32; x < 257; x += 32) shapes.DrawShape(screen2, mainvp, 0x1e0b, x, 188);
        shapes.DrawShape(screen2, mainvp, 0x1e0c, 288, 188);
        for (int y = 190; y < 198; y++)
            for (int x = 12; x < 308; x++) screen2.Pixels[y * 320 + x] = 121;
        text.Draw(screen2, textvp, 148 - 18 * 3, 1, TextRenderer.Font6, 6, 121, "THE XARGON TRILOGY");

        Assert.Equal(screen2.Pixels, screen.Pixels);
    }

    [Fact]
    public void OpeningBarDrawsNothingAboveTheStatusArea()
    {
        var (screen, shapes, text) = Rig();
        Fill(screen, 200);
        Hud.DrawOpening(screen, shapes, text);
        for (int y = 0; y < 160; y++)
            for (int x = 0; x < 320; x++)
                Assert.Equal(200, screen.Pixels[y * 320 + x]);
    }

    [Fact]
    public void CaptionIsDrawnInColourSixInsideTheTextStrip()
    {
        var (screen, shapes, text) = Rig();
        Hud.DrawOpening(screen, shapes, text);
        int sixes = 0;
        for (int y = 190; y < 198; y++)
            for (int x = 12; x < 308; x++)
            {
                byte p = screen.Pixels[y * 320 + x];
                if (p == 6)
                {
                    sixes++;
                    // The caption is 18 glyphs of 6px centred on x=160, i.e. x in [94, 202).
                    Assert.InRange(x, 12 + 148 - 54, 12 + 148 - 54 + 108);
                }
            }
        Assert.True(sixes > 20, "the caption should draw a visible number of colour-6 pixels");
    }

    [Fact]
    public void OpeningBarIsDifferentFromTheInGameBar()
    {
        var (a, shapes, text) = Rig();
        Fill(a, 200);
        Hud.DrawOpening(a, shapes, text);
        var (b, _, _) = Rig();
        Fill(b, 200);
        Hud.Draw(b, shapes, text, new PlayerState { Health = 5 }, new StatusLine(), 0);
        Assert.NotEqual(b.Pixels, a.Pixels);
    }
}
