using SDL3;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Ui;

/// <summary>
/// The slot cursor of loadsavewin: clamped to 0..6 (no wrap). The C moves at most when
/// <c>abs(clock - moveclock) &gt; 1</c> (a held key moves every other tick at best). Here a fresh key press always
/// moves (a deliberate tap should never be swallowed) and OS auto-repeats are throttled by that rule.
/// </summary>
public sealed class SlotCursor
{
    public int Cur { get; private set; }
    private long _moveClock = long.MinValue / 2;

    /// <summary>Apply a Down/Right (+1) or Up/Left (-1) key at tick <paramref name="nowTick"/>. Returns false when
    /// nothing was processed (delta 0, or a repeat inside the throttle window).</summary>
    public bool Move(int delta, long nowTick, bool repeat)
    {
        if (delta == 0) return false;
        if (repeat && Math.Abs(nowTick - _moveClock) <= 1) return false;
        _moveClock = nowTick; // set even when the move clamps, as the C does
        Cur = Math.Max(0, Math.Min(Cur + Math.Sign(delta), SaveGames.Slots - 1));
        return true;
    }
}

/// <summary>
/// The SAVE GAME / LOAD GAME slot window (xargon.c loadsavewin, 364-410). <see cref="Render"/> is pure drawing (no
/// SDL); <see cref="Pick"/> is the blocking modal. The window stays on screen after <see cref="Pick"/> returns, as in
/// the C, because savegame() types the new name into the chosen row.
/// </summary>
public sealed class SaveLoadWindow
{
    public const int Font = TextRenderer.Font6;
    public const int TitleX = 12, TitleY = 4, RuleY1 = 8, RuleY2 = 84;
    public const int NumberX = 16, NameX = 28, FirstRowY = 20, RowPitch = 10;
    public const int EscX = 6, AbortX = 42, EscY = 100;
    public const int CursorX = 4, StripCells = 11, StripPitch = 6;
    public const string Rule = "____________";

    /// <summary>defwin(&amp;menu_win,13,16,6,7,0,0,textbox): border 104,16 112x128; inside 112,24 96x112.</summary>
    public static Window Frame() => Window.DefTextbox(13, 16, 6, 7);

    /// <summary>y of slot row <paramref name="slot"/> inside the window.</summary>
    public static int RowY(int slot) => FirstRowY + slot * RowPitch;

    /// <summary>The animated cursor glyph (font char 1..8). The C's counter is 11 after the strip loop and advances
    /// <c>c=(c&amp;7)+1</c> once per loop pass, so the first glyph is 4 and the cycle is 4,5,6,7,8,1,2,3.</summary>
    public static int CursorGlyphAt(int tick) => (((tick + 3) % 8) + 8) % 8 + 1;

    /// <summary>
    /// drawwin + title, rules, slot numbers, names (or <paramref name="blankText"/> for an empty name), the ESC hint and
    /// the cursor column. <paramref name="cursorRow"/> 0..6 puts glyph <paramref name="cursorGlyph"/> (1..8) on that
    /// slot; -1 (or glyph 0) draws only the colour-1 strip. Returns the window.
    /// </summary>
    public static Window Render(VgaScreen screen, ShapeManager shapes, TextRenderer text, string title,
        string[] slotNames, string blankText, int cursorRow, int cursorGlyph)
    {
        var win = Frame();
        win.Draw(screen, shapes);
        var vp = win.Inside;
        text.Draw(screen, vp, TitleX, TitleY, TextRenderer.Font8, 7, -1, title);
        text.Draw(screen, vp, 0, RuleY1, TextRenderer.Font8, 4, -1, Rule);
        text.Draw(screen, vp, 0, RuleY2, TextRenderer.Font8, 4, -1, Rule);
        for (int c = 0; c < SaveGames.Slots; c++)
            text.Draw(screen, vp, NumberX, RowY(c), Font, 3, -1, (c + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        for (int c = 0; c < SaveGames.Slots; c++)
        {
            string n = c < slotNames.Length ? slotNames[c] : "";
            text.Draw(screen, vp, NameX, RowY(c), Font, 6, -1, n.Length != 0 ? n : blankText);
        }
        text.Draw(screen, vp, EscX, EscY, Font, 3, -1, "<ESC>");
        text.Draw(screen, vp, AbortX, EscY, Font, 3, -1, "TO ABORT");
        DrawCursor(screen, text, vp, cursorRow, cursorGlyph);
        return win;
    }

    /// <summary>The cursor column: fontcolor(8,1), 11 blank cells at (4, 20+c*6) (a 6-px colour-1 strip), then the glyph.</summary>
    public static void DrawCursor(VgaScreen screen, TextRenderer text, Viewport vp, int cursorRow, int cursorGlyph)
    {
        for (int c = 0; c < StripCells; c++)
            text.Draw(screen, vp, CursorX, FirstRowY + c * StripPitch, Font, 8, 1, " ");
        if (cursorRow >= 0 && cursorRow < SaveGames.Slots && cursorGlyph > 0)
            text.Draw(screen, vp, CursorX, RowY(cursorRow), Font, 8, 1, ((char)cursorGlyph).ToString());
    }

    // ---- modal (SDL; not headlessly testable beyond the pure parts above) ----

    private readonly VgaScreen _screen;
    private readonly Func<ShapeManager> _shapes;
    private readonly Func<TextRenderer> _text;
    private readonly SdlPresenter? _presenter;
    private readonly TickClock? _clock;
    private readonly Controls? _controls;
    private readonly HashSet<SDL.Keycode>? _keys;
    private readonly Action? _onQuit;
    private readonly Func<bool>? _appQuit;
    private readonly Func<string[]> _names;
    private bool _quit;

    /// <summary>A window close was requested (here or by the app); Pick returns -1 at once.</summary>
    internal bool QuitLatched => _quit || (_appQuit?.Invoke() ?? false);

    /// <param name="names">The current episode's slot names (re-read on every Pick).</param>
    public SaveLoadWindow(VgaScreen screen, Func<ShapeManager> shapes, Func<TextRenderer> text,
        SdlPresenter? presenter, TickClock? clock, Controls? controls, HashSet<SDL.Keycode>? keys,
        Action? onQuit, Func<bool>? appQuit, Func<string[]> names)
    {
        _screen = screen;
        _shapes = shapes;
        _text = text;
        _presenter = presenter;
        _clock = clock;
        _controls = controls;
        _keys = keys;
        _onQuit = onQuit;
        _appQuit = appQuit;
        _names = names;
    }

    /// <summary>
    /// loadsavewin(title, blankText): the window over the current screen. Up/Left and Down/Right move the cursor
    /// (clamped), Enter, Space or Shift select it, Esc returns -1; digits do nothing. A window close (or one already
    /// latched) returns -1. On exit held keys are cleared, the clock restarts and fire is masked, like every modal.
    /// </summary>
    public int Pick(string title, string blankText)
    {
        if (QuitLatched) return -1;
        var text = _text();
        string[] names = _names();
        Window win = Render(_screen, _shapes(), text, title, names, blankText, 0, CursorGlyphAt(0));
        _presenter?.Present(_screen);

        var cursor = new SlotCursor();
        long startTick = _clock?.NowTicks ?? 0;
        int frames = 0;
        int result = -2; // -2 = still choosing
        try
        {
            while (result == -2 && !_quit)
            {
                int tick = _clock is null ? ++frames : (int)(_clock.NowTicks - startTick);
                result = Pump(cursor, _clock?.NowTicks ?? frames);
                if (result != -2 || _quit) break;
                DrawCursor(_screen, text, win.Inside, cursor.Cur, CursorGlyphAt(tick));
                _presenter?.Present(_screen);
                _clock?.Wait();
            }
        }
        finally
        {
            _keys?.Clear();
            _clock?.Reset();
            if (_controls is not null) { _controls.Fire1Off = 1; _controls.Fire2Off = 1; }
        }

        if (_quit) return -1;
        DrawCursor(_screen, text, win.Inside, -1, 0); // the C erases the glyph (wprint " ") before it returns
        _presenter?.Present(_screen);
        return result;
    }

    private int Pump(SlotCursor cursor, long now)
    {
        while (SDL.PollEvent(out SDL.Event e))
        {
            switch ((SDL.EventType)e.Type)
            {
                case SDL.EventType.Quit:
                    _quit = true;
                    _onQuit?.Invoke();
                    return -1;
                case SDL.EventType.KeyUp:
                    _keys?.Remove(e.Key.Key);
                    break;
                case SDL.EventType.KeyDown:
                    _keys?.Add(e.Key.Key);
                    switch (e.Key.Key)
                    {
                        case SDL.Keycode.Up: case SDL.Keycode.Left: cursor.Move(-1, now, e.Key.Repeat); break;
                        case SDL.Keycode.Down: case SDL.Keycode.Right: cursor.Move(+1, now, e.Key.Repeat); break;
                        case SDL.Keycode.Return: case SDL.Keycode.KpEnter: case SDL.Keycode.Space:
                        case SDL.Keycode.LShift: case SDL.Keycode.RShift: // fire1 (the port also maps Space)
                            if (!e.Key.Repeat) return cursor.Cur;
                            break;
                        case SDL.Keycode.Escape:
                            if (!e.Key.Repeat) return -1;
                            break;
                    }
                    break;
            }
        }
        return -2;
    }
}
