using System.Buffers.Binary;
using System.Text;

namespace XargonNET.Assets;

/// <summary>
/// Parses board/level files (XARGON.C loadboard). The layout is 16-bit large-model Turbo C:
/// int = 2 bytes, long = 4 bytes, far pointer = 4 bytes, structs byte-packed. See
/// docs/FORMATS.md. A correct parse consumes the file to exactly EOF — enforced here as the
/// primary correctness check.
/// </summary>
public static class BoardLoader
{
    public static Board Load(string path) => Parse(File.ReadAllBytes(path), Path.GetFileName(path));

    public static Board Parse(byte[] data, string name = "board")
    {
        var s = data.AsSpan();
        int p = 0;
        var b = new Board();

        // bd[128][64] as int16. C stores bd[x][y] row-major over x then y, so file order is
        // x outer, y inner.
        for (int x = 0; x < Board.W; x++)
            for (int y = 0; y < Board.H; y++)
            {
                b.Cells[x, y] = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(p, 2));
                p += 2;
            }

        int numObjs = BinaryPrimitives.ReadInt16LittleEndian(s.Slice(p, 2)); p += 2;

        for (int i = 0; i < numObjs; i++)
        {
            var o = new Obj { Kind = s[p] };
            p += 1;
            o.X = I16(s, ref p); o.Y = I16(s, ref p);
            o.Xd = I16(s, ref p); o.Yd = I16(s, ref p);
            o.Xl = I16(s, ref p); o.Yl = I16(s, ref p);
            o.State = I16(s, ref p); o.SubState = I16(s, ref p);
            o.StateCount = I16(s, ref p); o.Counter = I16(s, ref p);
            o.Flags = I16(s, ref p);
            // objtype order (XARGON.H): the `inside` far pointer sits between objflags and
            // info1/zaphold — not at the end of the struct.
            uint inside = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(p, 4)); p += 4;
            o.HadInside = inside != 0;
            o.Info1 = I16(s, ref p); o.ZapHold = I16(s, ref p);
            b.Objects.Add(o);
        }

        // pltype pl
        b.Player.Level = I16(s, ref p);
        b.Player.Health = I16(s, ref p);
        b.Player.Emeralds = I16(s, ref p);
        b.Player.NumInv = I16(s, ref p);
        for (int i = 0; i < 30; i++) b.Player.Inv[i] = I16(s, ref p);
        b.Player.Ouched = I16(s, ref p);
        p += 22; // char pad[22]
        b.Player.Score = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(p, 4)); p += 4;

        b.FruitCount = s[p]; p += 1;

        // Inside-text blocks, in object order, for objects whose saved pointer was non-zero:
        // len:int16 then len+1 bytes (text + NUL).
        foreach (var o in b.Objects)
        {
            if (!o.HadInside) continue;
            int len = BinaryPrimitives.ReadInt16LittleEndian(s.Slice(p, 2)); p += 2;
            o.Inside = Encoding.ASCII.GetString(s.Slice(p, len));
            p += len + 1; // includes the trailing NUL written by saveboard
        }

        if (p != data.Length)
            throw new InvalidDataException(
                $"{name}: parsed {p} bytes but file is {data.Length} (format mismatch)");

        return b;
    }

    private static int I16(ReadOnlySpan<byte> s, ref int p)
    {
        int v = BinaryPrimitives.ReadInt16LittleEndian(s.Slice(p, 2));
        p += 2;
        return v;
    }
}
