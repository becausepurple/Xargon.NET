namespace XargonNET.Engine;

/// <summary>
/// The high-score table of one episode: a thin pure view over a <see cref="ConfigFile"/>'s
/// <c>HiNames</c>/<c>HiScores</c> (sorted descending, index 0 = best) implementing printhi's
/// qualification and insertion (xargon.c:740-783). Mutations write straight through to the config
/// so it can be saved as is.
/// </summary>
public sealed class HighScoreTable
{
    /// <summary>winput(..., hiname[posn], 8): names are at most 8 characters.</summary>
    public const int MaxNameLen = 8;

    private readonly ConfigFile _cfg;

    public HighScoreTable(ConfigFile cfg) => _cfg = cfg;

    public int Count => ConfigFile.NumHighs;
    public string Name(int i) => _cfg.HiNames[i];
    public uint Score(int i) => _cfg.HiScores[i];

    /// <summary>
    /// The insert position (0 = top) or -1. The C scans from the bottom while
    /// <c>score &gt; hiscore[posn-1]</c> (strictly greater), so a tie ranks below the equal entry,
    /// and a score of 0 never beats even an all-zero table.
    /// </summary>
    public int Qualifies(uint score)
    {
        int posn = Count;
        while (posn > 0 && score > _cfg.HiScores[posn - 1]) posn--;
        return posn >= Count ? -1 : posn;
    }

    /// <summary>The C shift loop (<c>for c = numhighs-2 down to posn</c>: the last entry is dropped),
    /// then the new entry at <paramref name="pos"/>. An empty name is allowed (the row stays empty until typed).</summary>
    public void Insert(int pos, string name, uint score)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pos);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pos, Count);
        for (int c = Count - 2; c >= pos; c--)
        {
            _cfg.HiScores[c + 1] = _cfg.HiScores[c];
            _cfg.HiNames[c + 1] = _cfg.HiNames[c];
        }
        _cfg.HiScores[pos] = score;
        SetName(pos, name);
    }

    public void SetName(int pos, string name)
    {
        name ??= "";
        _cfg.HiNames[pos] = name.Length > MaxNameLen ? name[..MaxNameLen] : name;
    }

    /// <summary><c>hiname[posn][0] = '\0'</c>.</summary>
    public void ClearName(int pos) => _cfg.HiNames[pos] = "";

    /// <summary>x of a score inside the window: the last digit ends at x=120 (<c>120 - strlen*6</c>).</summary>
    public static int ScoreX(uint score) => 120 - 6 * score.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
}

/// <summary>
/// Loads/saves an episode's <see cref="ConfigFile"/> for the high-score screens. If a save fails (no
/// per-user store, read-only disk) the config is kept in memory for the rest of the session so the
/// player still sees the new score; it is simply not persisted and a new process starts from disk.
/// Never throws.
/// </summary>
public sealed class HighScoreStore
{
    private readonly UserDataStore _store;
    private readonly Dictionary<int, ConfigFile> _sessionOnly = new();
    private readonly HashSet<int> _readFailed = new(); // episodes whose config exists but could not be read: never overwrite it
    private readonly Func<string, byte[]>? _readAll;

    /// <param name="readAll">Test seam for the file read (default <c>File.ReadAllBytes</c>).</param>
    public HighScoreStore(UserDataStore store, Func<string, byte[]>? readAll = null)
    {
        _store = store;
        _readAll = readAll;
    }

    public ConfigFile Load(int episode)
    {
        if (_sessionOnly.TryGetValue(episode, out var kept)) return kept;
        var status = ConfigFile.TryLoad(_store, episode, out var cfg, _readAll);
        if (status == ConfigFile.LoadStatus.ReadFailed) _readFailed.Add(episode); else _readFailed.Remove(episode);
        return cfg;
    }

    /// <summary>savecfg(). Returns false when it could not be persisted (the table stays in memory). When the last read
    /// of an existing file FAILED (a transient lock) the defaults shown are not the real scores, so nothing is written.</summary>
    public bool Save(int episode, ConfigFile cfg)
    {
        if (!_readFailed.Contains(episode) && cfg.Save(_store, episode))
        {
            _sessionOnly.Remove(episode);
            return true;
        }
        if (episode is >= 1 and <= 3) _sessionOnly[episode] = cfg;
        return false;
    }
}
