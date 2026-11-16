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

    /// <summary>Returns defaults for a missing, unreadable or wrong-length file. Never throws.</summary>
    public static ConfigFile Load(UserDataStore store, int episode)
    {
        if (!ValidEpisode(episode)) return new ConfigFile();
        try
        {
            var path = store.PathFor(FileNameFor(episode));
            var info = new FileInfo(path);
            if (!info.Exists || info.Length != FileSize) return new ConfigFile();
            return Parse(File.ReadAllBytes(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException
                                      or ArgumentException)
        {
            return new ConfigFile();
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
    /// Writes the file atomically (temp file in the same directory, then move over the target).
    /// Returns false on an invalid episode or any IO failure; never throws.
    /// </summary>
    public bool Save(UserDataStore store, int episode)
    {
        if (!ValidEpisode(episode)) return false;
        string? temp = null;
        try
        {
            if (!store.EnsureDirectory()) return false;
            var path = store.PathFor(FileNameFor(episode));
            temp = store.PathFor($"config.xr{episode}.{Guid.NewGuid():N}.tmp");
            File.WriteAllBytes(temp, ToBytes());
            File.Move(temp, path, overwrite: true);
            temp = null;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException
                                      or ArgumentException)
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
