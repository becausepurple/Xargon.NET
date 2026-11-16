namespace XargonNET.Engine;

/// <summary>
/// Per-user writable data directory (config/high scores, later save games). The original wrote to the
/// current directory; the port never writes next to the game data or into the repository. The
/// directory is created lazily, only when something is written.
/// </summary>
public sealed class UserDataStore
{
    /// <summary>Root directory, or null for an unavailable store (no per-user location could be determined).</summary>
    public string? Root { get; }

    /// <summary>False when no safe per-user directory exists: Load yields defaults, writes fail.</summary>
    public bool IsAvailable => Root is not null;

    /// <summary>Explicit root; must be fully qualified (a relative, drive-relative "C:foo" or root-relative "/foo" path could land in the game-data folder or repo).</summary>
    public UserDataStore(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (!Path.IsPathFullyQualified(root))
            throw new ArgumentException("User data root must be a fully qualified path.", nameof(root));
        Root = root;
    }

    private UserDataStore() { Root = null; }

    /// <summary>
    /// LocalApplicationData/XargonNET, falling back to UserProfile/.xargonnet when the former is empty
    /// (containers/services without a profile). If neither exists the store is unavailable rather than
    /// using a relative or temp path: scores are silently not persisted instead of being written next
    /// to the game data or into the repository. Never throws.
    /// </summary>
    public static UserDataStore Default() => FromFolders(Environment.GetFolderPath);

    /// <summary>Testable core of <see cref="Default"/>.</summary>
    public static UserDataStore FromFolders(Func<Environment.SpecialFolder, string> getFolder)
    {
        try
        {
            var local = getFolder(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(local) && Path.IsPathFullyQualified(local))
                return new UserDataStore(Path.Combine(local, "XargonNET"));
            var profile = getFolder(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(profile) && Path.IsPathFullyQualified(profile))
                return new UserDataStore(Path.Combine(profile, ".xargonnet"));
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or PlatformNotSupportedException
                                      or System.Security.SecurityException)
        {
        }
        return new UserDataStore();
    }

    public string PathFor(string fileName) =>
        Root is null ? throw new InvalidOperationException("User data store is unavailable.")
                     : Path.Combine(Root, fileName);

    /// <summary>Creates the root directory if needed. Returns false on any IO failure (never throws).</summary>
    public bool EnsureDirectory()
    {
        if (Root is null) return false;
        try
        {
            Directory.CreateDirectory(Root);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException
                                      or ArgumentException or System.Security.SecurityException)
        {
            return false;
        }
    }
}
