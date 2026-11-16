using SDL3;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Ui;

/// <summary>Result of feeding a key to <see cref="TextInputState"/>.</summary>
public enum TextInputOutcome { Editing, Committed, Cancelled }

/// <summary>
/// Pure model of WINDOWS.C <c>winput</c>'s edit buffer (no SDL, no drawing).
/// <list type="bullet">
/// <item>Accepts printable ASCII 32..127 (<c>key&gt;=32 &amp;&amp; key&lt;128</c>); other keys are ignored.</item>
/// <item>First-key-clears: the C's <c>firstflag</c> starts set; a printable first key discards the pre-filled
/// text before appending, and ANY key (printable or not, e.g. Backspace) clears the flag afterwards.</item>
/// <item>Appends only while <c>strlen &lt; maxlen</c>; Backspace on empty is a no-op. The C also treats
/// Left-arrow as Backspace; the SDL modal maps that to <see cref="Backspace"/>.</item>
/// <item>Enter commits. Esc ends the C's loop with the buffer untouched (so the C itself "keeps what is
/// typed"); the port's policy is <c>EscCommits</c>: true = commit (high-score names), false = cancel (saves).</item>
/// <item>The text is never trimmed (a name of only spaces is a non-empty string in the C).</item>
/// </list>
/// </summary>
public sealed class TextInputState
{
    public const int MinChar = 32;
    public const int MaxChar = 127;

    public string Text { get; private set; }
    public int MaxLen { get; }
    public bool EscCommits { get; }
    /// <summary>The C's <c>firstflag</c>: true until the first key of any kind has been processed.</summary>
    public bool FirstKeyClears { get; private set; } = true;

    public TextInputState(string initial, int maxLen, bool escCommits)
    {
        Text = initial ?? "";
        MaxLen = maxLen;
        EscCommits = escCommits;
    }

    public static bool IsAccepted(int c) => c >= MinChar && c <= MaxChar;

    public TextInputOutcome Press(char c)
    {
        if (IsAccepted(c))
        {
            if (FirstKeyClears) Text = "";
            if (Text.Length < MaxLen) Text += c;
        }
        FirstKeyClears = false;
        return TextInputOutcome.Editing;
    }

    public TextInputOutcome Backspace()
    {
        if (Text.Length > 0) Text = Text[..^1];
        FirstKeyClears = false;
        return TextInputOutcome.Editing;
    }

    /// <summary>Any other (ignored) key: consumed by the C loop, so it still clears the first-key flag.</summary>
    public TextInputOutcome OtherKey()
    {
        FirstKeyClears = false;
        return TextInputOutcome.Editing;
    }

    public TextInputOutcome Enter() => TextInputOutcome.Committed;

    public TextInputOutcome Escape() => EscCommits ? TextInputOutcome.Committed : TextInputOutcome.Cancelled;
}

/// <summary>
/// WINDOWS.C <c>winput</c>/<c>wgetkey</c> SDL modal. Dependencies are handed in the same way as
/// <see cref="PopupWindows"/>. The edit-buffer logic is <see cref="TextInputState"/>; drawing is the static
/// <see cref="Render"/>; only the event loop is untestable headlessly.
/// </summary>
public sealed class TextInputBox
{
    /// <summary>The C's cursor is an animation, not an on/off blink: <c>cursorchar=(cursorchar&amp;7)+1</c> once per
    /// 18.2 Hz clock tick, drawn as font glyph 1..8. <paramref name="tick"/> is the number of ticks elapsed since the
    /// loop began (0 = nothing advanced yet, glyph 1). Cycle: 1,2,..,8,1,...</summary>
    public static int CursorGlyphAt(int tick) => (((tick % 8) + 8) % 8) + 1;

    /// <summary>Draw the field and cursor. Clears the field (<c>maxLen+1</c> blank cells, painted with the
    /// background like the C's wprint of spaces), prints the text, then the cell after it holds either glyph
    /// <paramref name="cursorChar"/> (1..8) or a blank (0), as wgetkey does (it overwrites the cursor with " "
    /// when a key arrives).</summary>
    public static void Render(VgaScreen screen, TextRenderer text, Viewport vp, int x, int y, int font,
        int hi, int back, string s, int maxLen, int cursorChar)
    {
        text.Draw(screen, vp, x, y, font, hi, back, new string(' ', maxLen + 1));
        text.Draw(screen, vp, x, y, font, hi, back, s);
        int cx = x + TextRenderer.CharWidth(font) * s.Length;
        text.Draw(screen, vp, cx, y, font, hi, back, ((char)(cursorChar == 0 ? ' ' : cursorChar)).ToString());
    }

    private readonly VgaScreen _screen;
    private readonly Func<TextRenderer> _text;
    private readonly SdlPresenter? _presenter;
    private readonly TickClock? _clock;
    private readonly Controls? _controls;
    private readonly HashSet<SDL.Keycode>? _keys;
    private readonly Action? _onQuit;
    private readonly Func<bool>? _appQuit;
    private readonly IntPtr _window;
    private bool _quit;

    /// <summary>A window close was requested (here or by the app); Run returns null at once.</summary>
    internal bool QuitLatched => _quit || (_appQuit?.Invoke() ?? false);

    public TextInputBox(VgaScreen screen, Func<TextRenderer> text, SdlPresenter? presenter, TickClock? clock,
        Controls? controls, HashSet<SDL.Keycode>? keys, Action? onQuit, Func<bool>? appQuit, IntPtr window)
    {
        _screen = screen;
        _text = text;
        _presenter = presenter;
        _clock = clock;
        _controls = controls;
        _keys = keys;
        _onQuit = onQuit;
        _appQuit = appQuit;
        _window = window;
    }

    /// <summary>winput(vp, x, y, font, text, maxlen). Returns the entered text on Enter (and on Esc when
    /// <paramref name="escCommits"/>), or null when cancelled or the window was closed.</summary>
    public string? Run(Viewport vp, int x, int y, int font, string initial, int maxLen, bool escCommits,
        int hi = 7, int back = 1)
    {
        if (QuitLatched) return null;
        var state = new TextInputState(initial, maxLen, escCommits);
        var outcome = TextInputOutcome.Editing;
        long startTick = _clock?.NowTicks ?? 0;
        int frames = 0;

        if (_window != IntPtr.Zero) SDL.StartTextInput(_window);
        try
        {
            while (outcome == TextInputOutcome.Editing && !_quit)
            {
                outcome = Pump(state);
                if (outcome != TextInputOutcome.Editing || _quit) break;
                int tick = _clock is null ? ++frames : (int)(_clock.NowTicks - startTick);
                Render(_screen, _text(), vp, x, y, font, hi, back, state.Text, maxLen, CursorGlyphAt(tick));
                _presenter?.Present(_screen);
                _clock?.Wait();
            }
        }
        finally
        {
            if (_window != IntPtr.Zero) SDL.StopTextInput(_window);
            _keys?.Clear();
            _clock?.Reset();
            if (_controls is not null) { _controls.Fire1Off = 1; _controls.Fire2Off = 1; }
        }

        if (_quit) return null;
        // Final frame: the cursor cell is blanked (wgetkey's wprint(" ")) before leaving.
        Render(_screen, _text(), vp, x, y, font, hi, back, state.Text, maxLen, 0);
        _presenter?.Present(_screen);
        return outcome == TextInputOutcome.Committed ? state.Text : null;
    }

    private TextInputOutcome Pump(TextInputState state)
    {
        var outcome = TextInputOutcome.Editing;
        while (outcome == TextInputOutcome.Editing && SDL.PollEvent(out SDL.Event e))
        {
            switch ((SDL.EventType)e.Type)
            {
                case SDL.EventType.Quit:
                    _quit = true;
                    _onQuit?.Invoke();
                    break;
                case SDL.EventType.TextInput:
                    foreach (char c in (System.Runtime.InteropServices.Marshal.PtrToStringUTF8(e.Text.Text) ?? "")) outcome = state.Press(c);
                    break;
                case SDL.EventType.KeyDown:
                    _keys?.Add(e.Key.Key);
                    switch (e.Key.Key)
                    {
                        case SDL.Keycode.Backspace:
                        case SDL.Keycode.Left:
                            outcome = state.Backspace();
                            break;
                        case SDL.Keycode.Return:
                        case SDL.Keycode.KpEnter:
                            if (!e.Key.Repeat) outcome = state.Enter();
                            break;
                        case SDL.Keycode.Escape:
                            if (!e.Key.Repeat) outcome = state.Escape();
                            break;
                    }
                    break;
                case SDL.EventType.KeyUp:
                    _keys?.Remove(e.Key.Key);
                    break;
            }
        }
        return outcome;
    }

}
