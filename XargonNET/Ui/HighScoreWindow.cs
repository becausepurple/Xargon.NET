using XargonNET.Engine;
using XargonNET.Video;

namespace XargonNET.Ui;

/// <summary>
/// The HIGH SCORES window (xargon.c printhi). <see cref="Frame"/>/<see cref="Render"/> are pure drawing
/// (no SDL); the instance methods are the two blocking modals: <see cref="Show"/> = <c>printhi(0)</c> and
/// <see cref="ShowAfterGame"/> = <c>printhi(1)</c>. The window is drawn over whatever is on the screen.
/// </summary>
public sealed class HighScoreWindow
{
    public const int Font = TextRenderer.Font6;
    public const int TitleX = 18, TitleY = 4, UnderlineX = 15, UnderlineY = 8;
    public const int NameX = 8, FirstRowY = 20, RowPitch = 7, NameCells = 8;
    public const int TitleColor = 7, UnderlineColor = 4, NameColor = 3, ScoreColor = 2;
    /// <summary>fontcolor(7, back=1) for the new-score row while its name is typed.</summary>
    public const int EntryColor = 7, EntryBack = 1;

    /// <summary>defwin(11,24,8,6,0,0,textbox): border 88,24 144x112; inside 96,32 128x96.</summary>
    public static Window Frame() => Window.DefTextbox(11, 24, 8, 6);

    /// <summary>y of table row <paramref name="row"/> inside the window.</summary>
    public static int RowY(int row) => FirstRowY + row * RowPitch;

    /// <summary>
    /// drawwin + title + underline + the ten names and scores. If <paramref name="highlightRow"/> is 0..9 that
    /// row's name cell is then painted as 8 spaces in white on colour 1 (the C's <c>wprint "        "</c> before
    /// winput); pass -1 for none.
    /// </summary>
    public static Window Render(VgaScreen screen, ShapeManager shapes, TextRenderer text, HighScoreTable table,
        int highlightRow)
    {
        var win = Frame();
        win.Draw(screen, shapes);
        var vp = win.Inside;
        text.Draw(screen, vp, TitleX, TitleY, Font, TitleColor, -1, "- HIGH SCORES -");
        text.Draw(screen, vp, UnderlineX, UnderlineY, Font, UnderlineColor, -1, "________________");
        for (int i = 0; i < table.Count; i++)
            text.Draw(screen, vp, NameX, RowY(i), Font, NameColor, -1, table.Name(i));
        for (int i = 0; i < table.Count; i++)
            text.Draw(screen, vp, HighScoreTable.ScoreX(table.Score(i)), RowY(i), Font, ScoreColor, -1,
                table.Score(i).ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (highlightRow >= 0 && highlightRow < table.Count)
            text.Draw(screen, vp, NameX, RowY(highlightRow), Font, EntryColor, EntryBack, new string(' ', NameCells));
        return win;
    }

    // ---- modals (SDL; not headlessly testable beyond the pure parts above) ----

    private readonly VgaScreen _screen;
    private readonly Func<ShapeManager> _shapes;
    private readonly Func<TextRenderer> _text;
    private readonly SdlPresenter? _presenter;
    private readonly PopupWindows _popups;
    private readonly TextInputBox _input;
    private readonly HighScoreStore _store;
    private readonly Func<int> _episode;

    public HighScoreWindow(VgaScreen screen, Func<ShapeManager> shapes, Func<TextRenderer> text,
        SdlPresenter? presenter, PopupWindows popups, TextInputBox input, HighScoreStore store, Func<int> episode)
    {
        _screen = screen;
        _shapes = shapes;
        _text = text;
        _presenter = presenter;
        _popups = popups;
        _input = input;
        _store = store;
        _episode = episode;
    }

    /// <summary>printhi(0): the current episode's table over the current screen, then rest().</summary>
    public void Show()
    {
        var table = new HighScoreTable(_store.Load(_episode()));
        Render(_screen, _shapes(), _text(), table, -1);
        _presenter?.Present(_screen);
        _popups.Rest();
    }

    /// <summary>
    /// printhi(1): if <paramref name="score"/> beats entry 9, insert it with an empty name, redraw, let the
    /// player type the name (max 8, Enter or Esc commits), save, and return true with no further wait. If it
    /// does not qualify (a 0 never does), show the table then rest() and return false. A failed save is
    /// tolerated: the score stays in memory for this session (<see cref="HighScoreStore"/>).
    /// </summary>
    public bool ShowAfterGame(uint score)
    {
        int episode = _episode();
        var cfg = _store.Load(episode);
        var table = new HighScoreTable(cfg);
        int pos = table.Qualifies(score);
        if (pos < 0)
        {
            Render(_screen, _shapes(), _text(), table, -1);
            _presenter?.Present(_screen);
            _popups.Rest();
            return false;
        }

        table.Insert(pos, "", score);
        var win = Render(_screen, _shapes(), _text(), table, pos);
        _presenter?.Present(_screen);
        string? name = _input.Run(win.Inside, NameX, RowY(pos), Font, "", HighScoreTable.MaxNameLen,
            escCommits: true, hi: EntryColor, back: EntryBack);
        if (name is not null) table.SetName(pos, name); // null only when the window was closed: keep the empty name
        _store.Save(episode, cfg);
        return true;
    }
}
