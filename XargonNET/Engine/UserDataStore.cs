namespace XargonNET.Engine;

/// <summary>
/// Per-user writable data directory (config/high scores, later save games). The original wrote to the
/// current directory; the port never writes next to the game data or into the repository. The
/// directory is created lazily, only when something is written.
/// </summary>
public sealed class UserDataStore
{
    public string Root { get; }

    public UserDataStore(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = root;
    }

    /// <summary>LocalApplicationData/XargonNET (not created until a write).</summary>
    public static UserDataStore Default() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XargonNET"));

    public string PathFor(string fileName) => Path.Combine(Root, fileName);

    /// <summary>Creates the root directory if needed. Returns false on any IO failure (never throws).</summary>
    public bool EnsureDirectory()
    {
        try
        {
            Directory.CreateDirectory(Root);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException
                                      or ArgumentException)
        {
            return false;
        }
    }
}
