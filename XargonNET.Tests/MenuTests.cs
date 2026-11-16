using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Ui;
using XargonNET.Video;
using Xunit;

namespace XargonNET.Tests;

public class MenuTests
{
    // Exact strings from XARGON.C (menu1 minus the dropped ORDERING INFO, menu2, menu3).
    private const string GameMenuText =
        "7    - GAME OPTIONS -    \r3RETURN TO GAME\r2QUIT GAME\r2SAVE GAME\r2LOAD GAME\r2INSTRUCTIONS\r2ADDITIONAL INVENTORY\r2BUY EXTRA ITEMS\r2GRANNY MODE is    \r2NOISE is    \r\r";
    private const string BuyMenuText =
        "7  What would you like to buy?\r3RETURN TO GAME\r2A. HEALTH UNIT 15 EMERALDS\r2B. EXTRA LASER 10 EMERALDS\r2C. RAPID FIRE  25 EMERALDS\r2D. 1 FIREBALL   5 EMERALDS\r2E. 5 FIREBALLS 20 EMERALDS\r2F. INVINCIBLE  30 EMERALDS\r\r";

    private static (VgaScreen screen, ShapeManager shapes, TextRenderer text) Fixture()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        return (screen, shapes, new TextRenderer(shapes));
    }

    private static int CrCount(string s) => s.Count(c => c == '\r');

    [Fact]
    public void MainMenuWindowHeightIsCrCountBased()
    {
        Assert.Equal(10, CrCount(GameApp.MainMenuText));
        var m = new Menu(GameApp.MainMenuText, GameApp.MainMenuKeys, 1, 8, 24, 11, 8, 24, 1);
        Assert.Equal((CrCount(GameApp.MainMenuText) / 2 + 1) * 16, m.Inside.H); // 96
        Assert.Equal(96, m.Inside.H);
        Assert.Equal(112, m.Border.H); // identical to the pre-change geometry (Split gave 11 segments -> 6 rows)
        Assert.Equal((88, 24, 144, 112), (m.Border.X, m.Border.Y, m.Border.W, m.Border.H));
    }

    [Fact]
    public void GameMenuWindowIsSixRows()
    {
        Assert.Equal(11, CrCount(GameMenuText));
        var m = new Menu(GameMenuText, "RQSLIABGN", 1, 9, 24, 9, 10, 24, 2);
        Assert.Equal(96, m.Inside.H);
        Assert.Equal((72, 24, 176, 112), (m.Border.X, m.Border.Y, m.Border.W, m.Border.H));
        Assert.Equal((80, 32, 160, 96), (m.Inside.X, m.Inside.Y, m.Inside.W, m.Inside.H));
    }

    [Fact]
    public void BuyMenuWindowIsFiveRows()
    {
        Assert.Equal(9, CrCount(BuyMenuText));
        var m = new Menu(BuyMenuText, "RABCDEF", 1, 7, 24, 7, 12, 32, 0); // domenu(menu3,..,1,7,0,24,7,12,32,0)
        Assert.Equal(80, m.Inside.H);
        Assert.Equal((56, 32, 208, 96), (m.Border.X, m.Border.Y, m.Border.W, m.Border.H));
    }

    [Fact]
    public void MoveSelectionClampsAtBothEnds()
    {
        Assert.Equal(0, Menu.MoveSelection(0, -1, 8));
        Assert.Equal(7, Menu.MoveSelection(7, +1, 8));
        Assert.Equal(1, Menu.MoveSelection(0, +1, 8));
        Assert.Equal(6, Menu.MoveSelection(7, -1, 8));
        Assert.Equal(0, Menu.MoveSelection(0, 0, 8));
    }

    [Fact]
    public void ResolveKeyMapsLikeDomenu()
    {
        const string keys = "PLISDHCE";
        Assert.Equal('E', Menu.ResolveKey(Menu.KeyEsc, keys, 3, 'E'));
        Assert.Equal('R', Menu.ResolveKey(Menu.KeyEsc, "RQSLIABGN", 3, 'R'));
        Assert.Equal('I', Menu.ResolveKey(Menu.KeyF1, keys, 0, 'E'));
        Assert.Equal('S', Menu.ResolveKey('S', keys, 0, 'E'));
        Assert.Equal('L', Menu.ResolveKey('\r', keys, 1, 'E'));   // Return picks the highlighted item
        Assert.Equal('C', Menu.ResolveKey(' ', keys, 6, 'E'));    // Space too
        Assert.Equal('\0', Menu.ResolveKey('Z', keys, 0, 'E'));   // not in keytab
        Assert.Equal('\0', Menu.ResolveKey('\0', keys, 0, 'E'));
    }

    private static byte Px(VgaScreen s, int x, int y) => s.Pixels[y * VgaScreen.Width + x];

    private static byte[] Render(string text, int flag, bool granny, bool noise)
    {
        var (screen, shapes, tr) = Fixture();
        var m = new Menu(text, "RQSLIABGN", 1, 9, 24, 9, 10, 24, flag);
        m.Draw(screen, tr, shapes, 0, 0, granny, noise);
        return (byte[])screen.Pixels.Clone();
    }

    private static (int six, int four, int other, int outside) DiffStats(byte[] a, byte[] b, int rx, int ry, int rw, int rh)
    {
        int six = 0, four = 0, other = 0, outside = 0;
        for (int y = 0; y < VgaScreen.Height; y++)
            for (int x = 0; x < VgaScreen.Width; x++)
            {
                byte pa = a[y * VgaScreen.Width + x], pb = b[y * VgaScreen.Width + x];
                if (pa == pb) continue;
                if (x < rx || x >= rx + rw || y < ry || y >= ry + rh) { outside++; continue; }
                if (pa == 6) six++; else if (pa == 4) four++; else other++;
            }
        return (six, four, other, outside);
    }

    [Fact]
    public void OnOffOverlayDrawsColour6WhenOnAndColour4WhenOff()
    {
        // Inside origin of the game menu: (80, 32). GRANNY state at inside-(118,70), NOISE at (82,78).
        const int ix = 80, iy = 32;
        var baseline = Render(GameMenuText, 0, false, false); // same lines, no overlay

        // Granny ON (2 chars = 12px wide), noise OFF (3 chars = 18px wide).
        var a = Render(GameMenuText, 2, true, false);
        var g = DiffStats(a, baseline, ix + 118, iy + 70, 12, 6);
        Assert.True(g.six > 0);
        Assert.Equal(0, g.four);
        Assert.Equal(0, g.other);
        var n = DiffStats(a, baseline, ix + 82, iy + 78, 18, 6);
        Assert.True(n.four > 0);
        Assert.Equal(0, n.six);
        Assert.Equal(0, n.other);

        // Flipped: granny OFF -> 4, noise ON -> 6.
        var b = Render(GameMenuText, 2, false, true);
        var g2 = DiffStats(b, baseline, ix + 118, iy + 70, 18, 6);
        Assert.True(g2.four > 0);
        Assert.Equal(0, g2.six);
        var n2 = DiffStats(b, baseline, ix + 82, iy + 78, 12, 6);
        Assert.True(n2.six > 0);
        Assert.Equal(0, n2.four);
    }

    [Fact]
    public void OverlayOnlyForMenuFlag2()
    {
        var plain0 = Render(GameMenuText, 0, false, false);
        var withFlags0 = Render(GameMenuText, 0, true, true);
        Assert.Equal(plain0, withFlags0);
        var withFlags1 = Render(GameMenuText, 1, true, true);
        var plain1 = Render(GameMenuText, 1, false, false);
        Assert.Equal(plain1, withFlags1);
    }

    [Fact]
    public void CursorUsesTwoToneColour8()
    {
        // The cursor glyph (font shapes 1..6) uses fontcolor 8 = two-tone 6/14, never solid 14 only.
        var (screen, shapes, tr) = Fixture();
        var m = new Menu(GameMenuText, "RQSLIABGN", 1, 9, 24, 9, 10, 24, 0);
        m.Draw(screen, tr, shapes, 0, 0);
        bool has6 = false, has14 = false;
        // cursor at inside x=8..16, y = 6 + 1*8 = 14
        for (int y = 32 + 14; y < 32 + 14 + 8; y++)
            for (int x = 80 + 8; x < 80 + 8 + 8; x++)
            {
                byte p = Px(screen, x, y);
                if (p == 6) has6 = true;
                if (p == 14) has14 = true;
            }
        Assert.True(has6 && has14);
    }
}
