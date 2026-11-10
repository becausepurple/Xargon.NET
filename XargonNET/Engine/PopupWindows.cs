using SDL3;
using XargonNET.Objects;
using XargonNET.Text;
using XargonNET.Ui;
using XargonNET.Video;

namespace XargonNET.Engine;

/// <summary>Scroll direction for the long-message form: up moves toward the first line, down toward the last.</summary>
public enum ScrollKey { None, Up, Down }

/// <summary>A popup key classified into the logical inputs the modal loop reacts to (checkctrl0's
/// dx1/dy1/key/fire1/fire2 reduced to the cases dotextmsg cares about).</summary>
public enum PopupInput { None, Up, Down, Space, Enter, Escape, Fire1, Fire2, Other }

/// <summary>The form and vertical placement chosen for a message of <c>NumLines</c> CRs.</summary>
public readonly record struct PopupLayout(bool Scroll, int StartY, int VisibleRows);

/// <summary>
/// The blocking text-popup implementation (xargon.c dotextmsg). It composes a window over the last
/// displayed frame (GameApp's <c>_screen</c>; no board re-render) and runs a nested modal loop that
/// pumps SDL events, paces on the tick clock, and mirrors the C's release-wait-then-dismiss and the
/// long-message scroll form. The layout/scroll/dismiss <b>decisions</b> are pure static methods so
/// they can be unit-tested; only the SDL loop itself (key timing, real window) is untestable headlessly.
/// </summary>
public sealed class PopupWindows : IPopupHost
{
    /// <summary>textys: the inside viewport is 96 px tall, 6 px per row -> 16 visible rows.</summary>
    public const int VisibleRows = 16;

    // ---- pure decision helpers (unit tested) ------------------------------------------------

    /// <summary>Short vs scroll form and the centred body start-y. linecount &lt;= textys is the short
    /// form (`y = (96 - 6*(linecount-1))/2`); more lines scroll from y0 = 0.</summary>
    public static PopupLayout Layout(int numLines)
    {
        if (numLines <= VisibleRows)
            return new PopupLayout(false, (96 - 6 * (numLines - 1)) / 2, numLines);
        return new PopupLayout(true, 0, VisibleRows);
    }

    /// <summary>Apply one scroll step and clamp the offset to [0, numLines-16] (max offset shows one
    /// trailing blank row, as the C's `y0 = linecount - textys` does).</summary>
    public static int ScrollTarget(int offset, int numLines, ScrollKey key)
    {
        int max = Math.Max(0, numLines - VisibleRows);
        int o = key switch { ScrollKey.Up => offset - 1, ScrollKey.Down => offset + 1, _ => offset };
        return Math.Clamp(o, 0, max);
    }

    /// <summary>Short form dismissal: Space / Enter / Fire1 (Shift). Esc and Fire2 do NOT dismiss.</summary>
    public static bool DismissShort(PopupInput k) => k is PopupInput.Space or PopupInput.Enter or PopupInput.Fire1;

    /// <summary>Scroll form dismissal: Space / Enter / Esc / Fire1 (Shift). Fire2 does NOT dismiss.</summary>
    public static bool DismissScroll(PopupInput k) =>
        k is PopupInput.Space or PopupInput.Enter or PopupInput.Escape or PopupInput.Fire1;

    /// <summary>Whether a held key blocks the initial "wait for all keys released" phase.</summary>
    public static bool IsReleaseBlocking(PopupInput k) => k is PopupInput.Up or PopupInput.Down
        or PopupInput.Space or PopupInput.Enter or PopupInput.Escape or PopupInput.Fire1 or PopupInput.Fire2;

    /// <summary>The scroll step a logical input requests (Up/Left/PgUp = up a line, Down/Right/PgDn = down).</summary>
    public static ScrollKey ToScroll(PopupInput k) => k switch
    {
        PopupInput.Up => ScrollKey.Up,
        PopupInput.Down => ScrollKey.Down,
        _ => ScrollKey.None,
    };

    // ---- level banners (putlevelmsg / donelevelmsg) -------------------------------------------

    /// <summary>One banner text line: x/y inside the 152x64 banner interior, colour digit, text.</summary>
    public readonly record struct BannerLine(int X, int Y, int Color, string Text);

    /// <summary>The banner window: <c>defwin(5,36,14,4,4,0,plain)</c> (inside 152x64, hero face at TopLeft).</summary>
    public static Window BannerWindow() => Window.Def(5, 36, 14, 4, 4, 0, WindowStyle.Plain);

    /// <summary>
    /// putlevelmsg's text layout. numlines() counts CR characters; every line (index 0..n-1, no title) is
    /// a body line parsed like getline (optional colour digit '0'..'7', default 7; NOT uppercased) and
    /// centred by wprint at x = (152 - 6*len)/2, starting at y = (64 - 6*(n-1))/2 and stepping 6.
    /// A null or empty text yields no lines.
    /// </summary>
    public static IReadOnlyList<BannerLine> BannerLayout(string? text)
    {
        var result = new List<BannerLine>();
        if (string.IsNullOrEmpty(text)) return result;
        int n = 0;
        foreach (char ch in text) if (ch == '\r') n++;
        int y = (64 - 6 * (n - 1)) / 2;
        for (int i = 0; i < n; i++)
        {
            int c = 0, a = 0;
            while (a < i && c < text.Length) a += text[c++] == '\r' ? 1 : 0;
            while (c < text.Length && text[c] < 32 && text[c] != '\r') c++;
            int color = 7;
            if (c < text.Length && text[c] >= '0' && text[c] <= '7') color = text[c++] - '0';
            int start = c;
            while (c < text.Length && text[c] != '\r' && c - start < 77) c++;
            string line = text.Substring(start, c - start);
            result.Add(new BannerLine((152 - 6 * line.Length) / 2, y, color, line));
            y += 6;
        }
        return result;
    }

    /// <summary>donelevelmsg's dismissal test (after the initial wait for all keys to be released):
    /// Esc/Enter at once; any key or Fire1 once dt &gt;= 2; otherwise automatically at dt &gt;= 4.</summary>
    public static bool BannerDismiss(int dt, bool esc, bool enter, bool anyKeyOrFire)
    {
        if (esc || enter) return true;
        if (dt >= 2 && anyKeyOrFire) return true;
        return dt >= 4;
    }

    /// <summary>dt = (clock - levelmsgclock) / 10 on the 18.2 Hz tick counter.</summary>
    public static int BannerDt(long startTick, long nowTick) => (int)((nowTick - startTick) / 10);

    /// <summary>Draw the banner (frame, cleared interior, reset hero palette, face, text) onto a screen.
    /// Level text null / out of range draws nothing at all (the C reads out of bounds or returns early).</summary>
    internal static void RenderBannerInto(VgaScreen screen, ShapeManager shapes, TextRenderer text, string? levelText)
    {
        if (levelText is null) return;
        var win = BannerWindow();
        win.Draw(screen, shapes);
        win.ClearInside(screen, 129);
        Effects.ResetHeroColors(screen);
        for (int c = 0; c < 16; c++)
            shapes.DrawShape(screen, win.TopLeft, FaceShape(false, c), 16 * (c & 3), 16 * (c / 4));
        foreach (var l in BannerLayout(levelText))
            text.Draw(screen, win.Inside, l.X, l.Y, TextRenderer.Font6, l.Color, 129, l.Text);
    }

    /// <summary>putlevelmsg(episode, level): compose the banner over the current frame, present it, and return
    /// the tick the banner clock started on (levelmsgclock is set even when nothing is drawn).</summary>
    public long ShowLevelBanner(int episode, int level)
    {
        long start = _clock?.NowTicks ?? 0;
        if (QuitLatched) return start; // window already closed: skip the banner
        RenderBannerInto(_screen, _shapes(), _text(), GameStrings.LevelText(episode, level));
        _presenter?.Present(_screen);
        return start;
    }

    /// <summary>donelevelmsg(): wait until every key is released, then dismiss on Esc/Enter, on any key
    /// or Fire1 once dt &gt;= 2, or automatically at dt &gt;= 4 (see <see cref="BannerDismiss"/>).</summary>
    public void WaitLevelBanner(long startTick)
    {
        if (QuitLatched) return;
        bool released = false;
        while (!released && !_quit)
        {
            Pump(countRepeats: false);
            released = _keys is null || _keys.Count == 0;
            FrameTick();
        }
        bool done = false;
        while (!done && !_quit)
        {
            var pressed = Pump(countRepeats: false);
            bool esc = pressed.Contains(PopupInput.Escape);
            bool enter = pressed.Contains(PopupInput.Enter);
            bool fireHeld = false;
            if (_keys is not null) foreach (var k in _keys) if (Classify(k) == PopupInput.Fire1) fireHeld = true;
            int dt = BannerDt(startTick, _clock?.NowTicks ?? long.MaxValue / 2);
            done = BannerDismiss(dt, esc, enter, pressed.Count > 0 || fireHeld);
            FrameTick();
        }
        Dismiss();
    }

    // ---- dependencies (handed in by GameApp; nullable so the null-message guard is testable) -----

    private readonly VgaScreen _screen;
    private readonly Func<ShapeManager> _shapes;
    private readonly Func<TextRenderer> _text;
    private readonly Func<TextBank> _bank;
    private readonly SdlPresenter? _presenter;
    private readonly TickClock? _clock;
    private readonly Controls? _controls;
    private readonly HashSet<SDL.Keycode>? _keys;
    private readonly Action? _onQuit;

    private bool _quit;
    private readonly Func<bool>? _appQuit;

    /// <summary>A window close was already requested (here or by the app); popups/banners must not re-enter a
    /// modal loop, which has no auto-dismiss. Never cleared per call.</summary>
    internal bool QuitLatched => _quit || (_appQuit?.Invoke() ?? false);

    public PopupWindows(VgaScreen screen, Func<ShapeManager> shapes, Func<TextRenderer> text,
        Func<TextBank> bank, SdlPresenter? presenter, TickClock? clock, Controls? controls,
        HashSet<SDL.Keycode>? keys, Action? onQuit, Func<bool>? appQuit = null)
    {
        _screen = screen;
        _shapes = shapes;
        _text = text;
        _bank = bank;
        _presenter = presenter;
        _clock = clock;
        _controls = controls;
        _keys = keys;
        _onQuit = onQuit;
        _appQuit = appQuit;
    }

    // ---- IPopupHost -------------------------------------------------------------------------

    public void TextMessage(int n, PopupStyle style)
    {
        var msg = _bank().Get(n);
        if (msg is null) return; // text_get() NULL: do nothing, never draw (Review Focus 1)

        if (QuitLatched) return; // window already closed this frame: skip further popups
        RenderInto(_screen, _shapes(), _text(), msg, style, 0); // compose over the current frame
        if (!Layout(msg.NumLines).Scroll) RunShort();
        else RunScroll(msg, style, msg.NumLines);
        Dismiss();
    }

    /// <summary>dialogmsg(n, flg): the portrait dialog popup (hero or eagle face), short form only.</summary>
    public void DialogMessage(int n, bool eagle)
    {
        var msg = _bank().Get(n);
        if (msg is null) return; // text_get() NULL: nothing drawn

        if (QuitLatched) return;
        RenderDialogInto(_screen, _shapes(), _text(), msg, eagle);
        RunShort();
        Dismiss();
    }

    /// <summary>The dialog window: <c>defwin(2,16,14,4,4,0,dialog)</c> (hero) or <c>defwin(8,52,...)</c> (eagle).</summary>
    public static Window DialogWindow(bool eagle) =>
        Window.Def(eagle ? 8 : 2, eagle ? 52 : 16, 14, 4, 4, 0, WindowStyle.Dialog);

    /// <summary>Face tile c (0..15): hero 0x4000+4*256+20+c, eagle 0x4000+7*256+11+c.</summary>
    public static int FaceShape(bool eagle, int c) => (eagle ? 0x470b : 0x4414) + c;

    /// <summary>dialogmsg body start y: (inside height - 6*(linecount-1)) / 2.</summary>
    public static int DialogStartY(int numLines) => (64 - 6 * (numLines - 1)) / 2;

    /// <summary>
    /// Draw the dialogmsg frame, cleared interior (129), title, hero-palette reset, face tiles and body
    /// over the given screen. Shared by the modal loop and the debug dump.
    /// </summary>
    internal static void RenderDialogInto(VgaScreen screen, ShapeManager shapes, TextRenderer text,
        TextMessage msg, bool eagle)
    {
        var win = DialogWindow(eagle);
        win.Draw(screen, shapes);
        win.ClearInside(screen, 129);
        var (titleColor, title) = msg.GetLine(0, upper: false);
        win.TitleWin(screen, text, title, titleColor, dialogOffset: true);
        Effects.ResetHeroColors(screen);
        for (int c = 0; c < 16; c++)
            shapes.DrawShape(screen, win.TopLeft, FaceShape(eagle, c), 16 * (c & 3), 16 * (c / 4));
        int y = DialogStartY(msg.NumLines);
        for (int c = 1; c < msg.NumLines; c++) { PrintLine(screen, text, win, msg, y, c, 129); y += 6; }
    }

    // ---- rendering (drawwin + printline port) -----------------------------------------------

    /// <summary>
    /// Draw the whole popup — frame, cleared interior, title, and body — over the given screen at a
    /// scroll <paramref name="offset"/> (0 for the short form). Shared by the modal loop (initial draw
    /// and each scroll step) and the debug dump, so both render identically with no event loop.
    /// </summary>
    internal static void RenderInto(VgaScreen screen, ShapeManager shapes, TextRenderer text,
        TextMessage msg, PopupStyle style, int offset)
    {
        int back = style == PopupStyle.Dialog ? 121 : 129;
        var winStyle = style == PopupStyle.Dialog ? WindowStyle.Dialog : WindowStyle.Plain;

        // defwin(6,18,13,6,0,0,flg) + drawwin + clearvp(inside) + title.
        var win = Window.Def(6, 18, 13, 6, 0, 0, winStyle);
        win.Draw(screen, shapes);
        win.ClearInside(screen, back);
        var (titleColor, title) = msg.GetLine(0, upper: false); // title is NOT uppercased
        win.TitleWin(screen, text, title, titleColor, dialogOffset: false);

        if (!Layout(msg.NumLines).Scroll)
        {
            int y = Layout(msg.NumLines).StartY;
            for (int c = 1; c < msg.NumLines; c++) { PrintLine(screen, text, win, msg, y, c, back); y += 6; }
        }
        else
        {
            // 16 visible rows; a trailing index past the end renders as the C's blank row. No screen-scroll.
            for (int r = 0; r < VisibleRows; r++) PrintLine(screen, text, win, msg, 6 * r, offset + 1 + r, back);
        }
    }

    /// <summary>printline(): fontcolor(colour, back); paint the row with 36 spaces (the all-3 glyph fills
    /// the back colour), then draw the uppercased line centred in the 208 px inside (font 2).</summary>
    private static void PrintLine(VgaScreen screen, TextRenderer text, Window win, TextMessage msg,
        int y, int lineIndex, int back)
    {
        var (color, line) = msg.GetLine(lineIndex, upper: true);
        text.Draw(screen, win.Inside, 0, y, TextRenderer.Font6, color, back, new string(' ', 36));
        if (line.Length > 0)
            text.Draw(screen, win.Inside, (win.Inside.W - 6 * line.Length) / 2, y, TextRenderer.Font6, color, back, line);
    }

    // ---- modal loop (mirrors RunPageView; not headlessly testable) --------------------------

    private void RunShort()
    {
        // do { checkctrl0(1); } while (dx1||dy1||key||fire1||fire2): wait for every key/button released.
        bool released = false;
        while (!released && !_quit) { Pump(countRepeats: false); released = !AnyBlockingHeld(); FrameTick(); }
        // do { checkctrl0(1); } while (key!=' ' && key!=enter && !fire1): wait for a fresh dismiss press.
        bool done = false;
        while (!done && !_quit)
        {
            foreach (var k in Pump(countRepeats: false)) if (DismissShort(k)) done = true;
            FrameTick();
        }
    }

    private void RunScroll(TextMessage msg, PopupStyle style, int numLines)
    {
        int offset = 0;
        bool released = false;
        while (!released && !_quit) { Pump(countRepeats: true); released = !AnyBlockingHeld(); FrameTick(); }
        bool done = false;
        while (!done && !_quit)
        {
            // checkctrl0(0): event mode — each KeyDown (incl. key repeat) is one step.
            foreach (var k in Pump(countRepeats: true))
            {
                int next = ScrollTarget(offset, numLines, ToScroll(k));
                if (next != offset) { offset = next; RenderInto(_screen, _shapes(), _text(), msg, style, offset); }
                if (DismissScroll(k)) done = true;
            }
            FrameTick();
        }
    }

    /// <summary>Drain SDL events: update the held-key set and return each KeyDown's logical input (key
    /// repeats included only when <paramref name="countRepeats"/>). An SDL Quit sets the quit flag.</summary>
    private List<PopupInput> Pump(bool countRepeats)
    {
        var pressed = new List<PopupInput>();
        while (SDL.PollEvent(out SDL.Event e))
        {
            switch ((SDL.EventType)e.Type)
            {
                case SDL.EventType.Quit:
                    _quit = true;
                    _onQuit?.Invoke();
                    break;
                case SDL.EventType.KeyDown:
                    _keys?.Add(e.Key.Key);
                    if (countRepeats || !e.Key.Repeat) pressed.Add(Classify(e.Key.Key));
                    break;
                case SDL.EventType.KeyUp:
                    _keys?.Remove(e.Key.Key);
                    break;
            }
        }
        return pressed;
    }

    private bool AnyBlockingHeld()
    {
        if (_keys is null) return false;
        foreach (var k in _keys) if (IsReleaseBlocking(Classify(k))) return true;
        return false;
    }

    private void FrameTick()
    {
        _presenter?.Present(_screen);
        _clock?.Wait();
    }

    /// <summary>After dismissal: clear held keys and reset the clock. Clearing <c>_keys</c> is what stops a held
    /// Space/Shift/Ctrl from immediately shooting or jumping (the next Controls.Poll sees no keys until they are
    /// re-pressed; a deliberate deviation from the C). Fire1Off/Fire2Off are set as belt and braces only: that
    /// next Poll with an empty key set resets both masks, so they do not carry the guarantee.</summary>
    private void Dismiss()
    {
        _keys?.Clear();
        _clock?.Reset();
        if (_controls is not null) { _controls.Fire1Off = 1; _controls.Fire2Off = 1; }
    }

    private static PopupInput Classify(SDL.Keycode k) => k switch
    {
        SDL.Keycode.Up or SDL.Keycode.Left or SDL.Keycode.Pageup => PopupInput.Up,
        SDL.Keycode.Down or SDL.Keycode.Right or SDL.Keycode.Pagedown => PopupInput.Down,
        SDL.Keycode.Space => PopupInput.Space,
        SDL.Keycode.Return or SDL.Keycode.KpEnter => PopupInput.Enter,
        SDL.Keycode.Escape => PopupInput.Escape,
        SDL.Keycode.LShift or SDL.Keycode.RShift => PopupInput.Fire1,
        SDL.Keycode.LCtrl or SDL.Keycode.RCtrl or SDL.Keycode.LAlt or SDL.Keycode.RAlt => PopupInput.Fire2,
        _ => PopupInput.Other,
    };
}
