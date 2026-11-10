using System.Buffers.Binary;
using System.Text;

namespace XargonNET.Assets;

/// <summary>One entry of the info[] table: shape id, flags, and a name.</summary>
public sealed class InfoEntry
{
    public int Sh;       // shape id: table = (Sh>>8)&0x3f, shape = Sh&0xff
    public int Flags;
    public string Name = "";
}

/// <summary>
/// The info[] table (X_INFO.C init_info), built from TILES.XR*. Board cells are indices into
/// this table; a cell's shape is <c>info[cell].Sh</c>. Every entry defaults to sh=0x4500 and
/// flags = f_notvine|f_notstair|f_notwater, then the file overrides entries with records:
/// <c>index:i16, sh:i16, flagsXor:i16, len:u8, name[len]</c> (flags are XOR-applied).
/// </summary>
public sealed class InfoTable
{
    public const int Count = 900; // numinfotypes

    // f_notstair(2) | f_notvine(4) | f_notwater(16384)
    private const int FlagsDefault = 2 | 4 | 16384;

    public readonly InfoEntry[] Entries = new InfoEntry[Count];

    public InfoTable(byte[] tiles)
    {
        for (int c = 0; c < Count; c++)
            Entries[c] = new InfoEntry { Sh = 0x4500, Flags = FlagsDefault, Name = "" };

        var s = tiles.AsSpan();
        int p = 0;
        // Each record needs at least index(2)+sh(2)+flags(2)+len(1) = 7 bytes.
        while (p + 7 <= tiles.Length)
        {
            int index = BinaryPrimitives.ReadInt16LittleEndian(s.Slice(p, 2)); p += 2;
            int sh = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(p, 2)); p += 2;
            int flagsXor = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(p, 2)); p += 2;
            int len = s[p]; p += 1;
            if (p + len > tiles.Length) break;
            string name = Encoding.ASCII.GetString(s.Slice(p, len)); p += len;

            if ((uint)index < Count)
            {
                Entries[index].Sh = sh;
                Entries[index].Flags ^= flagsXor;
                Entries[index].Name = name;
            }
        }
    }

    public static InfoTable Load(string path) => new(File.ReadAllBytes(path));

    /// <summary>Look up by raw board cell value (masks off the runtime mod flags).</summary>
    public InfoEntry ForCell(int cell) => Entries[(cell & 0x3fff) % Count];
}
