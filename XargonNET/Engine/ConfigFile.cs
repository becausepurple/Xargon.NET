using System.Buffers.Binary;
using System.Text;

namespace XargonNET.Engine;

/// <summary>
/// The original <c>config.xrE</c> file (xargon.c loadcfg/savecfg, 232 bytes):
/// 0 hiname[10][10], 100 hiscore[10] u32 LE, 140 savename[7][10], 210 cf (11 x int16 = 22 bytes, opaque here).
/// Names are NUL-terminated within their 10-byte field, so at most 9 characters are stored.
/// Any file that is not exactly 232 bytes is treated as corrupt (defaults); Load/Save never throw.
/// </summary>
public sealed class ConfigFile
{
    public const int NumHighs = 10, NumSaves = 7, NameLen = 10, TailLen = 22;
    public const int HiNamesOffset = 0, HiScoresOffset = 100, SaveNamesOffset = 140, TailOffset = 210, FileSize = 232;

    public string[] HiNames { get; } = new string[NumHighs];
    public uint[] HiScores { get; } = new uint[NumHighs];
    public string[] SaveNames { get; } = new string[NumSaves];
    /// <summary>The DOS <c>cf</c> struct, preserved verbatim; zero for new files.</summary>
    public byte[] Tail { get; } = new byte[TailLen];

    public ConfigFile()
    {
        Array.Fill(HiNames, "");
        Array.Fill(SaveNames, "");
    }

    public static string FileNameFor(int episode) => $"config.xr{episode}";

    private static bool ValidEpisode(int episode) => episode is >= 1 and <= 3;

    /// <summary>Why <see cref="TryLoad"/> returned what it did.</summary>
    public enum LoadStatus
    {
        /// <summary>A valid 232-byte file was read.</summary>
        Ok,
        /// <summary>No store, invalid episode, or no file yet: defaults, and writing is safe.</summary>
        Missing,
        /// <summary>The file is not exactly 232 bytes: treated as corrupt (defaults); overwriting it is intended.</summary>
        Corrupt,
        /// <summary>The file exists but could not be read (lock, permissions): defaults, and writing would destroy real data.</summary>
        ReadFailed,
    }

    /// <summary>Returns defaults for a missing, unreadable or wrong-length file. Never throws.</summary>
    public static ConfigFile Load(UserDataStore store, int episode) =>
        TryLoad(store, episode, out var c) == LoadStatus.Ok ? c : new ConfigFile();

    /// <summary>Like <see cref="Load"/> but says why defaults came back. <paramref name="readAll"/> is a test seam for
    /// <c>File.ReadAllBytes</c>. Never throws.</summary>
    public static LoadStatus TryLoad(UserDataStore store, int episode, out ConfigFile config, Func<string, byte[]>? readAll = null)
    {
        config = new ConfigFile();
        if (!ValidEpisode(episode) || !store.IsAvailable) return LoadStatus.Missing;
        try
        {
            var path = store.PathFor(FileNameFor(episode));
            var info = new FileInfo(path);
            if (!info.Exists) return LoadStatus.Missing;
            if (info.Length != FileSize) return LoadStatus.Corrupt;
            var data = (readAll ?? File.ReadAllBytes)(path);
            if (data.Length != FileSize) return LoadStatus.Corrupt;
            config = Parse(data);
            return LoadStatus.Ok;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException
                                      or ArgumentException or System.Security.SecurityException)
        {
            return LoadStatus.ReadFailed;
        }
    }

    /// <summary>Decodes a 232-byte image; any other length yields defaults.</summary>
    public static ConfigFile Parse(ReadOnlySpan<byte> data)
    {
        var c = new ConfigFile();
        if (data.Length != FileSize) return c;
        for (int i = 0; i < NumHighs; i++)
        {
            c.HiNames[i] = DecodeName(data.Slice(HiNamesOffset + i * NameLen, NameLen));
            c.HiScores[i] = BinaryPrimitives.ReadUInt32LittleEndian(data[(HiScoresOffset + i * 4)..]);
        }
        for (int i = 0; i < NumSaves; i++)
            c.SaveNames[i] = DecodeName(data.Slice(SaveNamesOffset + i * NameLen, NameLen));
        data.Slice(TailOffset, TailLen).CopyTo(c.Tail);
        return c;
    }

    /// <summary>Encodes the exact 232-byte original layout.</summary>
    public byte[] ToBytes()
    {
        var b = new byte[FileSize];
        for (int i = 0; i < NumHighs; i++)
        {
            EncodeName(HiNames[i], b.AsSpan(HiNamesOffset + i * NameLen, NameLen));
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(HiScoresOffset + i * 4), HiScores[i]);
        }
        for (int i = 0; i < NumSaves; i++)
            EncodeName(SaveNames[i], b.AsSpan(SaveNamesOffset + i * NameLen, NameLen));
        Tail.CopyTo(b.AsSpan(TailOffset, TailLen));
        return b;
    }

    /// <summary>
    /// Writes the file atomically via <see cref="AtomicFile"/> (flushed temp file in the same directory, then move over the target).
    /// Returns false on an invalid episode or any IO failure; never throws.
    /// </summary>
    public bool Save(UserDataStore store, int episode)
    {
        if (!ValidEpisode(episode) || !store.IsAvailable) return false;
        try
        {
            return store.EnsureDirectory() && AtomicFile.TryWrite(store.PathFor(FileNameFor(episode)), ToBytes());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException
                                      or ArgumentException or System.Security.SecurityException)
        {
            return false;
        }
    }

    private static string DecodeName(ReadOnlySpan<byte> field)
    {
        var sb = new StringBuilder(NameLen);
        foreach (var by in field)
        {
            if (by == 0) break;
            if (by is >= 0x20 and <= 0x7E) sb.Append((char)by);
        }
        return sb.ToString();
    }

    private static void EncodeName(string? name, Span<byte> field)
    {
        field.Clear();
        if (name is null) return;
        int n = 0;
        foreach (var ch in name)
        {
            if (n >= NameLen - 1) break; // keep the terminating NUL
            if (ch is >= ' ' and <= '~') field[n++] = (byte)ch;
        }
    }
}
