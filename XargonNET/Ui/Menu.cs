using XargonNET.Video;

namespace XargonNET.Ui;

/// <summary>
/// A selectable menu in a text-box window (domenu in XARGON.C). The menu string is '\r'-separated
/// lines; each line begins with a colour digit followed by its text. Lines from index
/// <c>y0</c> onward are the selectable, indented items; <c>keytab</c> maps each item to a result
/// key (also selectable by pressing that key directly). Navigation/dispatch is driven by the
/// caller (see the pure helpers <see cref="MoveSelection"/> / <see cref="ResolveKey"/>); this class
/// owns parsing, geometry, and drawing.
/// </summary>
public sealed class Menu
{
    private readonly record struct Line(int Color, string Text);

    /// <summary>Pseudo key codes understood by <see cref="ResolveKey"/> besides upper-case letters.</summary>
    public const char KeyEsc = '\x1b';
    public const char KeyF1 = (char)187; // the C's F1 value (key==187)

    private readonly List<Line> _lines = new();
    private readonly Window _window;
    private readonly int _y0;
    private readonly int _textx0;
    private readonly int _menuFlag;

    public string Keytab { get; }
    public int ItemCount { get; }
    public Viewport Border => _window.Border;
    public Viewport Inside => _window.Inside;

    public Menu(string menuText, string keytab, int y0, int num, int textx0,
               int winx8, int winxl16, int winy, int menuFlag)
    {
        Keytab = keytab;
        _y0 = y0;
        ItemCount = num;
        _textx0 = textx0;
        _menuFlag = menuFlag;

        // numlines() (xargon.c:473) counts the CR characters; text after the last CR is not a line.
        int numLines = menuText.Count(c => c == '\r');
        foreach (var seg in menuText.Split('\r').Take(numLines))
        {
            if (seg.Length == 0) { _lines.Add(new Line(7, "")); continue; }
            int color = char.IsDigit(seg[0]) ? seg[0] - '0' : 7;
            _lines.Add(new Line(color, char.IsDigit(seg[0]) ? seg[1..] : seg));
        }

        _window = Window.DefTextbox(winx8, winy, winxl16, numLines / 2 + 1);
    }

    /// <summary>domenu cursor movement: <c>cur += delta</c> clamped to [0, num-1] (no wrap).</summary>
    public static int MoveSelection(int cur, int delta, int num) => Math.Min(num - 1, Math.Max(0, cur + delta));

    /// <summary>
    /// domenu key resolution (xargon.c:849-858). <paramref name="key"/> is an upper-cased letter, '\r'
    /// (Return), ' ', <see cref="KeyEsc"/> or <see cref="KeyF1"/>. Esc becomes <paramref name="escKey"/>
    /// ('E' main menu, 'R' otherwise), F1 becomes 'I'; Return/Space pick the highlighted item. Returns
    /// the chosen key or '\0' when the key selects nothing.
    /// </summary>
    public static char ResolveKey(char key, string keytab, int selected, char escKey)
    {
        if (key == KeyEsc) key = escKey;
        if (key == KeyF1) key = 'I';
        if (key == '\r' || key == ' ') return keytab[selected];
        return key != '\0' && keytab.IndexOf(key) >= 0 ? key : '\0';
    }

    public void Draw(VgaScreen screen, TextRenderer text, ShapeManager shapes, int selected, int animFrame,
                     bool granny = false, bool noise = false)
    {
        _window.Draw(screen, shapes);

        int ybase = _menuFlag == 1 ? 13 : 6;
        if (_menuFlag == 1)
            shapes.DrawShape(screen, _window.Inside, 0x210a, 22, 4); // menu title graphic
        else if (_menuFlag == 2)
        {
            // ON/OFF state of GRANNY MODE / NOISE (xargon.c:802-805; the C's uninitialised `cur` offset is 0).
            text.Draw(screen, _window.Inside, 118, 70, TextRenderer.Font6, granny ? 6 : 4, -1, granny ? "ON" : "OFF");
            text.Draw(screen, _window.Inside, 82, 78, TextRenderer.Font6, noise ? 6 : 4, -1, noise ? "ON" : "OFF");
        }

        for (int i = 0; i < _lines.Count; i++)
        {
            if (_lines[i].Text.Length == 0) continue;
            int x = 4 + (i >= _y0 ? _textx0 : 0);
            text.Draw(screen, _window.Inside, x, ybase + i * 8, TextRenderer.Font6,
                _lines[i].Color, -1, _lines[i].Text);
        }

        // Animated cursor glyph (font chars 1..6) left of the selected item; fontcolor 8 = two-tone 6/14.
        int cx = (_textx0 - 12) & ~7;
        int cy = ybase + (_y0 + selected) * 8;
        char glyph = (char)(1 + animFrame % 6);
        text.Draw(screen, _window.Inside, cx, cy, TextRenderer.Font6, 8, -1, glyph.ToString());
    }
}
