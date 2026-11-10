using System.Buffers.Binary;

namespace XargonNET.Assets;

/// <summary>A loaded shape table: a list of shapes plus its colour/flags metadata.</summary>
public sealed class ShapeTable
{
    public int NumColorBits;
    public int Flags;
    public readonly List<Shape> Shapes = new();

    /// <summary>shm_fontf — table holds a font (colour 0 transparent, identity colour map).</summary>
    public bool IsFont => (Flags & 1) != 0;

    public Shape? this[int i] => (uint)i < (uint)Shapes.Count ? Shapes[i] : null;
}

/// <summary>
/// Reads the GRAPHICS.XR* container (SHAPEMAN.C). The file starts with a 768-byte index:
/// 128 x uint32 table offsets, then 128 x uint16 table lengths. A table is present when its
/// length is non-zero; its bytes begin at the given absolute offset.
/// </summary>
public sealed class ShapeFile
{
    public const int MaxTables = 128;

    private readonly byte[] _data;
    private readonly uint[] _offsets = new uint[MaxTables];
    private readonly ushort[] _lengths = new ushort[MaxTables];

    public ShapeFile(byte[] data)
    {
        _data = data;
        var s = data.AsSpan();
        for (int i = 0; i < MaxTables; i++)
            _offsets[i] = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(i * 4, 4));
        int lenBase = MaxTables * 4;
        for (int i = 0; i < MaxTables; i++)
            _lengths[i] = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(lenBase + i * 2, 2));
    }

    public static ShapeFile Load(string path) => new(File.ReadAllBytes(path));

    public bool HasTable(int index) =>
        (uint)index < MaxTables && _lengths[index] != 0 && _offsets[index] != 0;

    public IEnumerable<int> PresentTables()
    {
        for (int i = 0; i < MaxTables; i++)
            if (HasTable(i)) yield return i;
    }

    /// <summary>Parse one shape table (xlate_table). Returns null if the table is absent.</summary>
    public ShapeTable? LoadTable(int index)
    {
        if (!HasTable(index)) return null;

        var s = _data.AsSpan();
        int p = (int)_offsets[index];

        var table = new ShapeTable();
        byte numShapes = s[p]; p += 1;
        p += 2; // numRots u16
        p += 6; // lenCGA, lenEGA, lenVGA (u16 each) — irrelevant to VGA decode
        table.NumColorBits = s[p]; p += 1;
        table.Flags = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(p, 2)); p += 2;

        // Non-font tables below 8-bit carry an inline palette of `numcolors` longs (4 bytes
        // each) — the original reads (longcolor>>16)&0xff per colour. Shipped VGA data is
        // 8-bit so this is skipped, but honour the layout for completeness.
        if (!table.IsFont && table.NumColorBits != 8)
            p += (1 << table.NumColorBits) * 4;

        for (int i = 0; i < numShapes; i++)
        {
            byte xs = s[p]; byte ys = s[p + 1]; byte store = s[p + 2]; p += 3;
            if (store != 0) // st_byte is the only form emitted in shipped data
                throw new InvalidDataException(
                    $"Table {index} shape {i}: unsupported storeType {store}");
            int len = xs * ys;
            byte[] px = s.Slice(p, len).ToArray(); p += len;
            table.Shapes.Add(new Shape(xs, ys, px));
        }

        return table;
    }
}
