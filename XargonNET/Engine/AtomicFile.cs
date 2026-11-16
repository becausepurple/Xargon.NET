namespace XargonNET.Engine;

/// <summary>
/// Replace a file without ever leaving it half written: the bytes go to a temp file in the same directory
/// (flushed to disk), which is then moved over the target. If anything fails the target is untouched and the
/// temp file is removed. Never throws.
/// </summary>
public static class AtomicFile
{
    /// <summary>Writes <paramref name="data"/> to <paramref name="path"/> atomically. <paramref name="move"/> is a test
    /// seam standing in for <c>File.Move(temp, path, overwrite: true)</c>. Returns false on any IO failure.</summary>
    public static bool TryWrite(string path, byte[] data, Action<string, string>? move = null)
    {
        string? temp = null;
        try
        {
            string? dir = Path.GetDirectoryName(path);
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name)) return false;
            temp = Path.Combine(dir, $"{name}.{Guid.NewGuid():N}.tmp");
            using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                fs.Write(data, 0, data.Length);
                fs.Flush(flushToDisk: true);
            }
            (move ?? ((from, to) => File.Move(from, to, overwrite: true)))(temp, path);
            temp = null;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException
                                      or ArgumentException or System.Security.SecurityException)
        {
            return false;
        }
        finally
        {
            if (temp is not null)
            {
                try { File.Delete(temp); } catch (Exception) { /* best effort */ }
            }
        }
    }
}
