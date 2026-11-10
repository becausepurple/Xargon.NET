using XargonNET.Assets;
using XargonNET.Video;

namespace XargonNET.Ui;

/// <summary>
/// The in-level status bar (statwin/drawstats from XARGON.C), drawn in the bottom viewport
/// (statvp = 0,160,320x28) below the 320x158 play area: the decorative bar graphic, the score,
/// five heart icons for health, and the emerald counter. Status-bar art lives in shape table 0x1E.
/// </summary>
public static class Hud
{
    private static readonly Viewport StatVp = new(0, 160, 320, 28);
    private static readonly Viewport TextVp = new(12, 190, 296, 8);
    private static readonly Viewport MainVp = new(0, 0, VgaScreen.Width, VgaScreen.Height);

    public static void Draw(VgaScreen screen, ShapeManager shapes, TextRenderer text, PlayerState pl,
        StatusLine status, int gamecount)
    {
        // Status-bar background + border (statwin, in-game variant).
        FillRect(screen, StatVp, 121);
        int num = 0;
        for (int x = 0; x < 289; x += 32) shapes.DrawShape(screen, StatVp, 0x1e00 + num++, x, 0);
        shapes.DrawShape(screen, StatVp, 0x1e1c, 206, 13); // emerald icon
        shapes.DrawShape(screen, MainVp, 0x1e0a, 0, 188);
        for (int x = 32; x < 257; x += 32) shapes.DrawShape(screen, MainVp, 0x1e0b, x, 188);
        shapes.DrawShape(screen, MainVp, 0x1e0c, 288, 188);

        // Score.
        string score = pl.Score.ToString();
        text.Draw(screen, StatVp, 54 - (1 + score.Length * 6), 14, TextRenderer.Font6, 6, 121, score);

        // Health (5 hearts: full = 0x1e0d, empty = 0x1e0e).
        for (int c = 0; c < 5; c++)
            shapes.DrawShape(screen, StatVp, c <= pl.Health - 1 ? 0x1e0d : 0x1e0e, c * 8 + 69, 12);

        // Inventory key slots (4 blanks; key icons added with the inventory-shape table later).
        for (int c = 0; c < 4; c++) shapes.DrawShape(screen, StatVp, 0x1e1b, 122 + c * 12, 13);

        // Emeralds.
        string em = pl.Emeralds.ToString();
        text.Draw(screen, StatVp, 245 - em.Length * 8, 14, TextRenderer.Font8, 5, 121, em);

        // Fruit counter (xargon.c:228-233): blank slot, icon 0x1e48+icon_fruit, count in font 6x6 colour 3,
        // right-aligned to x=197.
        shapes.DrawShape(screen, StatVp, 0x1e4c, 170, 13);
        shapes.DrawShape(screen, StatVp, 0x1e48 + pl.FruitIcon, 170, 13);
        string fruit = pl.Fruit.ToString();
        text.Draw(screen, StatVp, 185, 15, TextRenderer.Font6, 3, 121, "  ");
        text.Draw(screen, StatVp, 197 - (1 + fruit.Length * 6), 15, TextRenderer.Font6, 3, 121, fruit);

        // Bottom status line (drawstats, xargon.c:235-238): textvp = x12,y190,296x8 filled with 121, message
        // in font 6x6 centred on x=160. Colour 3 (pickups) flashes 7/4: the C uses 7 - pagedraw*3, where
        // pagedraw toggles on each page flip. Assumption: even Gamecount = page 0 (colour 7), odd = colour 4.
        FillRect(screen, TextVp, 121);
        int hi = status.Color == 3 ? 7 - (gamecount & 1) * 3 : status.Color;
        text.Draw(screen, TextVp, 148 - status.Message.Length * 3, 1, TextRenderer.Font6, hi, 121, status.Message);
    }

    private static void FillRect(VgaScreen screen, Viewport vp, byte color)
    {
        int y1 = Math.Min(vp.Y + vp.H, VgaScreen.Height);
        int x1 = Math.Min(vp.X + vp.W, VgaScreen.Width);
        for (int y = Math.Max(vp.Y, 0); y < y1; y++)
        {
            int row = y * VgaScreen.Width;
            for (int x = Math.Max(vp.X, 0); x < x1; x++) screen.Pixels[row + x] = color;
        }
    }
}
