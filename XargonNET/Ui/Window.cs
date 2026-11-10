using XargonNET.Video;

namespace XargonNET.Ui;

/// <summary>Window style (WINDOWS.H flags): plain = neither flag, dialog = shape set 0x47, textbox = 0x44 tiled box.</summary>
public enum WindowStyle { Plain = 0, Dialog = 1, Textbox = 2 }

/// <summary>
/// A bordered window (WINDOWS.C wintype/defwin/drawwin/titlewin). Geometry mirrors defwin: position
/// in 8/1-pixel units, size in 16-pixel units, with an optional portrait area (h16/v16) on the left.
/// For non-textbox windows with h16 &gt; 0, <see cref="TopLeft"/>.Y/H are overwritten with the Inside's
/// (the C callers do <c>topleft.vpy=inside.vpy; topleft.vpyl=inside.vpyl</c> right after defwin);
/// this is done here in the constructor.
/// </summary>
public sealed class Window
{
    public readonly Viewport Border;
    public readonly Viewport Inside;
    public readonly Viewport TopLeft;
    public readonly WindowStyle Style;
    private readonly int _winxl;   // inside width in px (before portrait narrowing)
    private readonly int _winyl;   // inside height in px
    private readonly int _xl16;
    private readonly int _yl16;
    private readonly int _winh16;
    private readonly int _winv16;

    internal int WinXl => _winxl;
    internal int WinH => _winh16 * 16;

    private Window(int x8, int y, int xl16, int yl16, int h16, int v16, WindowStyle style)
    {
        Style = style;
        bool textbox = style == WindowStyle.Textbox;
        _xl16 = xl16;
        _yl16 = yl16;
        _winh16 = h16;
        _winv16 = v16;
        _winxl = xl16 * 16;
        _winyl = yl16 * 16;

        Border = new Viewport(x8 * 8, y, xl16 * 16 + 16, yl16 * 16 + (textbox ? 16 : 28));
        Inside = new Viewport(x8 * 8 + 8, y + (textbox ? 8 : 16), xl16 * 16, yl16 * 16);
        if (h16 != 0)
        {
            Inside.W -= h16 * 16 + 8;
            Inside.X += h16 * 16 + 8;
        }
        TopLeft = new Viewport(x8 * 8 + 8, y + 16, h16 * 16, v16 * 16 + 5);
        if (!textbox && h16 > 0)
        {
            TopLeft.Y = Inside.Y;
            TopLeft.H = Inside.H;
        }
    }

    /// <summary>defwin(x8, y, xl16, yl16, h16, v16, style).</summary>
    public static Window Def(int x8, int y, int xl16, int yl16, int h16, int v16, WindowStyle style) =>
        new(x8, y, xl16, yl16, h16, v16, style);

    /// <summary>defwin(..., textbox): a text-box window at (x8*8, y), inside size xl16*16 by yl16*16.</summary>
    public static Window DefTextbox(int x8, int y, int xl16, int yl16) =>
        new(x8, y, xl16, yl16, 0, 0, WindowStyle.Textbox);

    /// <summary>The caller-side clearvp(&amp;inside) after fontcolor(.., back): fill the Inside rectangle.</summary>
    public void ClearInside(VgaScreen screen, int back) => FillRect(screen, Inside, (byte)back);

    /// <summary>titlewin x position (border-relative): 16 + (winxl+winh)/2 - 4*len, minus 32 when offset.</summary>
    internal static int TitleX(int winxl, int winh, int len, bool dialogOffset) =>
        16 + (winxl + winh) / 2 - 4 * len - (dialogOffset ? 32 : 0);

    /// <summary>titlewin(): title in font 1 at y=4 of the border viewport, transparent back.</summary>
    public void TitleWin(VgaScreen screen, TextRenderer text, string title, int titleColor, bool dialogOffset) =>
        text.Draw(screen, Border, TitleX(_winxl, WinH, title.Length, dialogOffset), 4, TextRenderer.Font8, titleColor, -1, title);

    /// <summary>drawwin(): clear the border region and draw the border + tiled interior fill.</summary>
    public void Draw(VgaScreen screen, ShapeManager shapes)
    {
        // Clear border viewport to its background colour (248).
        FillRect(screen, Border, 248);
        if (Style != WindowStyle.Textbox)
        {
            DrawFrame(screen, shapes, Style == WindowStyle.Dialog ? 0x4700 : 0x4400);
            return;
        }

        // Corners.
        shapes.DrawShape(screen, Border, 0x440b, 0, 0);
        shapes.DrawShape(screen, Border, 0x440d, _winxl + 8, 0);
        shapes.DrawShape(screen, Border, 0x4410, 0, _winyl + 8);
        shapes.DrawShape(screen, Border, 0x4412, _winxl + 8, _winyl + 8);
        // Top / bottom edges.
        for (int c = 1; c < _xl16 * 2 + 1; c++)
        {
            shapes.DrawShape(screen, Border, 0x440c, c * 8, 0);
            shapes.DrawShape(screen, Border, 0x4411, c * 8, _winyl + 8);
        }
        // Left / right edges.
        for (int c = 1; c < _yl16 * 2 + 1; c++)
        {
            shapes.DrawShape(screen, Border, 0x440e, 0, c * 8);
            shapes.DrawShape(screen, Border, 0x440f, _winxl + 8, c * 8);
        }
        // Interior fill.
        for (int c = 0; c < _xl16 * 2; c++)
            for (int d = 0; d < _yl16 * 2; d++)
                shapes.DrawShape(screen, Inside, 0x4413, c * 8, d * 8);
    }

    /// <summary>drawwin() plain/dialog branch; the two differ only in the shape-id base (0x44xx / 0x47xx).</summary>
    private void DrawFrame(VgaScreen screen, ShapeManager shapes, int b)
    {
        int winh = _winh16 * 16;
        shapes.DrawShape(screen, Border, b + 2, 0, 0);
        shapes.DrawShape(screen, Border, b + 7, _winxl + 8, _winyl + 16);
        shapes.DrawShape(screen, Border, b + 5, 0, _winyl + 16);
        for (int c = 0; c < _xl16; c++)
        {
            if (c == 0 || c != _winh16)
            {
                shapes.DrawShape(screen, Border, b + 4, c * 16 + 8, 0);
                shapes.DrawShape(screen, Border, b + 6, c * 16 + 8, _winyl + 16);
            }
        }
        shapes.DrawShape(screen, Border, b + 3, _winxl + 8, 0);
        for (int c = 0; c < _yl16; c++)
        {
            shapes.DrawShape(screen, Border, b + 1, _winxl + 8, 16 * c + 16);
            if (c == 0 || c != _winv16)
                shapes.DrawShape(screen, Border, b + 0, 0, c * 16 + 16);
        }
        if (_winh16 > 0)
        {
            shapes.DrawShape(screen, Border, b + 0x0a, winh + 8, 0);
            for (int c = 0; c < _yl16; c++)
                if (c == 0 || c != _winv16)
                    shapes.DrawShape(screen, Border, b + 9, winh + 8, c * 16 + 16);
            shapes.DrawShape(screen, Border, b + 8, winh + 8, _winyl + 16);
        }
    }

    private static void FillRect(VgaScreen screen, Viewport vp, byte color)
    {
        int x0 = Math.Max(vp.X, 0), y0 = Math.Max(vp.Y, 0);
        int x1 = Math.Min(vp.X + vp.W, VgaScreen.Width), y1 = Math.Min(vp.Y + vp.H, VgaScreen.Height);
        for (int y = y0; y < y1; y++)
        {
            int row = y * VgaScreen.Width;
            for (int x = x0; x < x1; x++) screen.Pixels[row + x] = color;
        }
    }
}
