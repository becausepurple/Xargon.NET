using XargonNET.Video;

namespace XargonNET.Ui;

/// <summary>
/// A selectable menu in a text-box window (domenu in XARGON.C). The menu string is '\r'-separated
/// lines; each line begins with a colour digit followed by its text. Lines from index
/// <c>y0</c> onward are the selectable, indented items; <c>keytab</c> maps each item to a result
/// key (also selectable by pressing that key directly). Navigation/dispatch is driven by the
/// caller; this class owns parsing, geometry, and drawing.
/// </summary>
public sealed class Menu
{
    private readonly record struct Line(int Color, string Text);

    private readonly List<Line> _lines = new();
    private readonly Window _window;
    private readonly int _y0;
    private readonly int _textx0;
    private readonly int _menuFlag;

    public string Keytab { get; }
    public int ItemCount { get; }

    public Menu(string menuText, string keytab, int y0, int num, int textx0,
               int winx8, int winxl16, int winy, int menuFlag)
    {
        Keytab = keytab;
        _y0 = y0;
        ItemCount = num;
        _textx0 = textx0;
        _menuFlag = menuFlag;

        foreach (var seg in menuText.Split('\r'))
        {
            if (seg.Length == 0) { _lines.Add(new Line(7, "")); continue; }
            int color = char.IsDigit(seg[0]) ? seg[0] - '0' : 7;
            _lines.Add(new Line(color, char.IsDigit(seg[0]) ? seg[1..] : seg));
        }

        _window = Window.DefTextbox(winx8, winy, winxl16, _lines.Count / 2 + 1);
    }

    public void Draw(VgaScreen screen, TextRenderer text, ShapeManager shapes, int selected, int animFrame)
    {
        _window.Draw(screen, shapes);

        int ybase = _menuFlag == 1 ? 13 : 6;
        if (_menuFlag == 1)
            shapes.DrawShape(screen, _window.Inside, 0x210a, 22, 4); // menu title graphic

        for (int i = 0; i < _lines.Count; i++)
        {
            if (_lines[i].Text.Length == 0) continue;
            int x = 4 + (i >= _y0 ? _textx0 : 0);
            text.Draw(screen, _window.Inside, x, ybase + i * 8, TextRenderer.Font6,
                _lines[i].Color, -1, _lines[i].Text);
        }

        // Animated cursor glyph (font chars 1..6) to the left of the selected item.
        int cx = (_textx0 - 12) & ~7;
        int cy = ybase + (_y0 + selected) * 8;
        char glyph = (char)(1 + animFrame % 6);
        text.Draw(screen, _window.Inside, cx, cy, TextRenderer.Font6, 14, -1, glyph.ToString());
    }
}
