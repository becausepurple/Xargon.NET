using System.Buffers.Binary;
using System.Text;

namespace XargonNET.Assets;

/// <summary>
/// Serialises a board plus player state in the original saveboard() layout (XARGON.C 337-362) — the
/// exact inverse of <see cref="BoardLoader.Parse"/>. Used for save games (<c>save_N.xrE</c>).
/// <code>
/// bd[128][64]   int16 LE, x outer / y inner              (sizeof(bd) = 16384)
/// numobjs       int16
/// objs[numobjs] 31 bytes each: kind(1) x y xd yd xl yl state substate statecount counter flags (int16 each),
///               inside far pointer (4), info1 zaphold (int16 each)
/// pl            96 bytes: level health emeralds numinv inv[30] ouched (int16), pad[22], score (uint32)
/// cnt_fruit     1 byte
/// per object with a non-NULL inside, in order: strlen (int16), then the text and its NUL
/// </code>
/// The saved <c>inside</c> pointer is a stale far pointer from the DOS process; loadboard only tests it
/// for non-NULL, so any non-zero value is equivalent (a fixed sentinel is written). pad[] is
/// uninitialised C memory; zeros are written. Fruit comes from <c>pl.Fruit</c> (cnt_fruit is part of the live
/// player state in the port). icon_fruit is never saved.
/// </summary>
public static class BoardWriter
{
    /// <summary>maxobjs (xargon.h): the engine's object array limit.</summary>
    public const int MaxObjects = 256;

    /// <summary>Largest <c>inside</c> length representable by the int16 length prefix.</summary>
    public const int MaxInsideLength = short.MaxValue;

    /// <summary>Non-zero stand-in for the saved inside far pointer.</summary>
    private const uint InsideSentinel = 1;

    public static byte[] Write(Board board, PlayerState pl)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(pl);
        if (board.Objects.Count > MaxObjects)
            throw new ArgumentException($"board has {board.Objects.Count} objects (max {MaxObjects})", nameof(board));

        // Encode inside strings first (also validates lengths before any output is built).
        var inside = new byte[board.Objects.Count][];
        for (int i = 0; i < inside.Length; i++)
        {
            var o = board.Objects[i];
            if (!o.HadInside && o.Inside is null) continue;
            string s = o.Inside ?? "";
            int nul = s.IndexOf('\0');
            if (nul >= 0) s = s[..nul]; // C strlen stops at the first NUL
            if (s.Length > MaxInsideLength)
                throw new ArgumentException($"object {i} inside text is {s.Length} chars (max {MaxInsideLength})", nameof(board));
            inside[i] = Encoding.ASCII.GetBytes(s);
        }

        using var ms = new MemoryStream(Board.W * Board.H * 2 + 2 + board.Objects.Count * 31 + 97);
        var t = new byte[4];

        void U16(int v) { BinaryPrimitives.WriteUInt16LittleEndian(t, unchecked((ushort)v)); ms.Write(t, 0, 2); }
        void U32(uint v) { BinaryPrimitives.WriteUInt32LittleEndian(t, v); ms.Write(t, 0, 4); }

        for (int x = 0; x < Board.W; x++)
            for (int y = 0; y < Board.H; y++)
                U16(board.Cells[x, y]);

        U16(board.Objects.Count);
        for (int i = 0; i < board.Objects.Count; i++)
        {
            var o = board.Objects[i];
            ms.WriteByte(o.Kind);
            U16(o.X); U16(o.Y); U16(o.Xd); U16(o.Yd); U16(o.Xl); U16(o.Yl);
            U16(o.State); U16(o.SubState); U16(o.StateCount); U16(o.Counter);
            U16(o.Flags);
            U32(inside[i] is null ? 0u : InsideSentinel);
            U16(o.Info1); U16(o.ZapHold);
        }

        U16(pl.Level); U16(pl.Health); U16(pl.Emeralds); U16(pl.NumInv);
        for (int i = 0; i < 30; i++) U16(pl.Inv[i]);
        U16(pl.Ouched);
        ms.Write(new byte[22]);
        U32(pl.Score);

        ms.WriteByte(unchecked((byte)pl.Fruit));

        for (int i = 0; i < inside.Length; i++)
        {
            if (inside[i] is null) continue;
            U16(inside[i].Length);
            ms.Write(inside[i]);
            ms.WriteByte(0);
        }

        return ms.ToArray();
    }
}
