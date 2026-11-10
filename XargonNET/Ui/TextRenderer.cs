using XargonNET.Video;

namespace XargonNET.Ui;

/// <summary>
/// Draws bitmap text (WINDOWS.C wprint). Font 1 = 8x8 glyphs in shape table 1, font 2 = 6x6 in
/// table 2; the glyph for a character is shape (font&lt;&lt;8 | ascii). Glyph pixels use values
/// 0..3 remapped by the font colour map: 0 = transparent, 1 = the text colour (hi), 2 = hi (or 14 for the hi==8 cursor), 3 =
/// the background (back), or transparent when back is -1 (the common menu case).
/// </summary>
public sealed class TextRenderer
{
    public const int Font8 = 1;
    public const int Font6 = 2;

    private readonly ShapeManager _shapes;

    public TextRenderer(ShapeManager shapes) => _shapes = shapes;

    public static int CharWidth(int font) => font == Font8 ? 8 : 6;

    /// <summary>Pixel width of a string in the given font.</summary>
    public static int Width(int font, string text) => CharWidth(font) * text.Length;

    /// <summary>
    /// Draw <paramref name="text"/> at (x,y) within a viewport. <paramref name="hi"/> is the text
    /// palette index; <paramref name="back"/> is the fill palette index behind glyph pixels, or
    /// -1 for a transparent background.
    /// </summary>
    public void Draw(VgaScreen screen, Viewport vp, int x, int y, int font, int hi, int back, string text)
    {
        int fx = CharWidth(font);
        for (int i = 0; i < text.Length; i++)
            DrawFontShape(screen, vp, (font << 8) | (text[i] & 0x7f), x + fx * i, y, hi, back);
    }

    /// <summary>
    /// Draw one font-flagged shape (id = table&lt;&lt;8 | index) with the fontcolor(hi, back) colour map, as
    /// drawshape() does for font tables (e.g. the score digits, table 3).
    /// </summary>
    public void DrawFontShape(VgaScreen screen, Viewport vp, int shapeId, int x, int y, int hi, int back)
    {
        // fontcolor() (WINDOWS.C): back 0 -> 248, hi 0 -> 11, hi 8 -> two-tone yellow cursor (6 / 14).
        if (back == 0) back = 248;
        if (hi == 0) hi = 11;
        int hi2 = hi;
        if (hi == 8) { hi = 6; hi2 = 14; }
        var glyph = _shapes.GetShape((shapeId >> 8) & 0x3f, shapeId & 0xff);
        if (glyph is not null)
            BlitGlyph(screen, vp, glyph, x, y, hi, hi2, back);
    }

    private static void BlitGlyph(VgaScreen screen, Viewport vp, Assets.Shape g, int x, int y, int hi, int hi2, int back)
    {
        int dx = vp.X + (x - vp.OriginX);
        int dy = vp.Y + (y - vp.OriginY);
        int clipL = Math.Max(vp.X, 0), clipT = Math.Max(vp.Y, 0);
        int clipR = Math.Min(vp.X + vp.W, VgaScreen.Width), clipB = Math.Min(vp.Y + vp.H, VgaScreen.Height);
        byte[] buf = screen.Pixels;

        for (int sy = 0; sy < g.Height; sy++)
        {
            int py = dy + sy;
            if (py < clipT || py >= clipB) continue;
            int rowSrc = sy * g.Width, rowDst = py * VgaScreen.Width;
            for (int sx = 0; sx < g.Width; sx++)
            {
                int px = dx + sx;
                if (px < clipL || px >= clipR) continue;
                int v = g.Pixels[rowSrc + sx];
                int color = v switch
                {
                    1 => hi,
                    2 => hi2,
                    3 => back,
                    _ => -1, // 0 (and anything else) = transparent
                };
                if (color >= 0) buf[rowDst + px] = (byte)color;
            }
        }
    }
}
