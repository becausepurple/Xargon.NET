using XargonNET.Assets;
using XargonNET.Objects;

namespace XargonNET.Engine;

/// <summary>
/// savegame() and loadgame() (xargon.c:412-450) without SDL: the slot picker and the name entry are handed in as
/// delegates, so the decisions (map-only saving, blank/cancelled names, empty slots, rejection of bad files, what is
/// written where) are unit-testable. The caller repaints and, after a successful <see cref="Load"/>, runs the
/// post-load steps (<c>LevelSession.LoadSaved</c>, fades, camera).
/// </summary>
public sealed class SaveLoadFlow
{
    /// <summary>winput(..., savename, 9): the 10-byte name field holds 9 characters.</summary>
    public const int NameMaxLen = ConfigFile.NameLen - 1;

    /// <summary>Plan ruling 2: Esc in the save-name field cancels (the C saves the typed text, a bug).</summary>
    public const bool NameEscCommits = false;

    public const string SaveTitle = "SAVE GAME", LoadTitle = "LOAD GAME", LoadBlank = "<empty>";

    /// <summary>dotextmsg(4): "You may only SAVE a game from the MAP board."</summary>
    public const int SaveRefusedMessage = 4;

    /// <summary>Status-bar texts for failures. Port additions, not in the original (which has no error path).</summary>
    public const string SaveFailedText = "Game could not be saved.",
        NameNotStoredText = "Saved, but the slot name was not stored.",
        LoadFailedText = "Could not load that game.";

    private readonly UserDataStore _store;
    private readonly HighScoreStore _cfg;
    private readonly Func<int> _episode;
    private readonly IPopupHost _popups;
    private readonly Func<string, string, int> _pick;
    private readonly Func<int, string, string?> _askName;
    private readonly Action<string> _notify;

    /// <param name="pick">loadsavewin(title, blankText): the chosen slot 0..6 or -1 (Esc / window closed).</param>
    /// <param name="askName">winput over the slot's name cell, pre-filled with the existing name; null = cancelled.</param>
    /// <param name="notify">Short status-bar text for a failed save or load (port additions: the C has no error path).</param>
    public SaveLoadFlow(UserDataStore store, HighScoreStore cfg, Func<int> episode, IPopupHost popups,
        Func<string, string, int> pick, Func<int, string, string?> askName, Action<string>? notify = null)
    {
        _store = store;
        _cfg = cfg;
        _episode = episode;
        _popups = popups;
        _pick = pick;
        _askName = askName;
        _notify = notify ?? (_ => { });
    }

    /// <summary>
    /// savegame(). Only on the map (<see cref="GameFlow.IsOnMap"/>); elsewhere message 4 and nothing else. Picks a
    /// slot, asks for a name, and if it is non-blank writes the live <paramref name="board"/> with the live
    /// <paramref name="pl"/> (cnt_fruit = pl.Fruit) and then records the name. The name and config are only touched
    /// after the file was written, so a failed write changes nothing. Returns true when a save was made.
    /// </summary>
    public bool Save(ObjectManager live, Board board, PlayerState pl)
    {
        if (!GameFlow.IsOnMap(live))
        {
            _popups.TextMessage(SaveRefusedMessage, PopupStyle.Plain);
            return false;
        }
        int slot = _pick(SaveTitle, "");
        if ((uint)slot >= SaveGames.Slots) return false;

        int episode = _episode();
        string? typed = _askName(slot, _cfg.Load(episode).SaveNames[slot]);
        string name = Sanitise(typed);
        if (name.Length == 0) return false; // cancelled (Esc), window closed, or a blank name: nothing is saved

        if (!SaveGames.TryWrite(_store, episode, slot, board, pl))
        {
            _notify(SaveFailedText); // never let the player believe an unsaved game was saved
            return false;
        }
        var cfg = _cfg.Load(episode);       // read-modify-write: high scores and other slot names are kept
        cfg.SaveNames[slot] = name;
        if (!_cfg.Save(episode, cfg)) _notify(NameNotStoredText); // the slot name stays in memory for this session only
        return true;
    }

    /// <summary>
    /// loadgame(): pick a slot; only one with a non-empty name loads. Returns the validated saved board, or null for
    /// Esc, an empty slot, or a file that is missing/corrupt/implausible (the C would crash; the port just reports
    /// "nothing loaded" and the running game is left exactly as it was).
    /// </summary>
    public Board? Load()
    {
        int slot = _pick(LoadTitle, LoadBlank);
        if ((uint)slot >= SaveGames.Slots) return null;
        int episode = _episode();
        if (_cfg.Load(episode).SaveNames[slot].Length == 0) return null;
        var board = SaveGames.TryRead(_store, episode, slot);
        if (board is null) _notify(LoadFailedText); // a NAMED slot that cannot be loaded; an empty slot stays silent as in the C
        return board;
    }

    /// <summary>Keep what the config file can store: printable ASCII 32..126, at most 9 characters.</summary>
    internal static string Sanitise(string? name)
    {
        if (name is null) return "";
        var sb = new System.Text.StringBuilder(NameMaxLen);
        foreach (char c in name)
        {
            if (c is < ' ' or > '~') continue;
            if (sb.Length >= NameMaxLen) break;
            sb.Append(c);
        }
        return sb.ToString();
    }
}
