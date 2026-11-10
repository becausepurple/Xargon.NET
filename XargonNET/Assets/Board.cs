namespace XargonNET.Assets;

/// <summary>A game object instance as stored in a board (objtype in XARGON.H).</summary>
public sealed class Obj
{
    public byte Kind;
    public int X, Y, Xd, Yd, Xl, Yl;
    public int State, SubState, StateCount, Counter;
    public int Flags, Info1, ZapHold;

    /// <summary>Whether the on-disk inside pointer was non-zero (i.e. this object carries text).</summary>
    public bool HadInside;

    /// <summary>Attached text ("inside"), for text/checkpoint objects.</summary>
    public string? Inside;

    /// <summary>Deep copy of this object (Inside strings are immutable, so shared safely).</summary>
    public Obj Clone() => new()
    {
        Kind = Kind, X = X, Y = Y, Xd = Xd, Yd = Yd, Xl = Xl, Yl = Yl,
        State = State, SubState = SubState, StateCount = StateCount, Counter = Counter,
        Flags = Flags, Info1 = Info1, ZapHold = ZapHold, HadInside = HadInside, Inside = Inside,
    };
}

/// <summary>Player state saved with a board (pltype in XARGON.H).</summary>
public sealed class PlayerState
{
    public int Level, Health, Emeralds, NumInv, Ouched;

    /// <summary>cnt_fruit (0..16, XARGON.C:45): 16 fruit earn a heart. Saved by stats(1)/restored by stats(0).</summary>
    public int Fruit;

    /// <summary>icon_fruit (0..3): shape offset of the last fruit picked up (0x1e48 + icon). NOT part of the stats() snapshot (Clone/CopyFrom leave it out on purpose).</summary>
    public int FruitIcon;
    public readonly int[] Inv = new int[30];
    public uint Score;

    /// <summary>Fresh-game state (init_inv in X_OBJMAN.C): 5 health, 0 emeralds, one laser.</summary>
    public static PlayerState NewGame()
    {
        var p = new PlayerState { Health = 5, Emeralds = 0, NumInv = 1 };
        p.Inv[0] = Objects.Inv.Laser;
        return p;
    }

    /// <summary>Deep copy of this player state (the o_pl snapshot used by stats(1)).</summary>
    public PlayerState Clone()
    {
        var p = new PlayerState
        {
            Level = Level, Health = Health, Emeralds = Emeralds, NumInv = NumInv,
            Ouched = Ouched, Score = Score, Fruit = Fruit,
        };
        Array.Copy(Inv, p.Inv, Inv.Length);
        return p;
    }

    /// <summary>Overwrite this state from another (the pl = o_pl memcpy in stats(0)).</summary>
    public void CopyFrom(PlayerState s)
    {
        Level = s.Level; Health = s.Health; Emeralds = s.Emeralds; NumInv = s.NumInv;
        Ouched = s.Ouched; Score = s.Score; Fruit = s.Fruit;
        Array.Copy(s.Inv, Inv, Inv.Length);
    }
}

/// <summary>
/// A loaded board/level: the 128x64 cell grid plus its objects, player state, and fruit count.
/// Cells hold raw values; use <see cref="CellIndex"/> to get the info[] index (low 14 bits).
/// </summary>
public sealed class Board
{
    public const int W = 128; // boardxs
    public const int H = 64;  // boardys

    /// <summary>bd[x][y] — raw cell values (top bits are runtime mod flags).</summary>
    public readonly int[,] Cells = new int[W, H];

    public readonly List<Obj> Objects = new();
    public PlayerState Player = new();
    public byte FruitCount;

    /// <summary>info[] index for a cell (board(x,y) macro: value &amp; 0x3fff).</summary>
    public int CellIndex(int x, int y) => Cells[x, y] & 0x3fff;

    /// <summary>Set a cell's value (setboard) — used by animated blocks and opening doors.</summary>
    public void SetCell(int x, int y, int value)
    {
        if ((uint)x < W && (uint)y < H) Cells[x, y] = value;
    }

    /// <summary>
    /// Deep copy of the board — cell grid, object list (each <see cref="Obj"/> cloned), player state,
    /// and fruit count. This is the port's saveboard(tempname): the live map is snapshotted when a
    /// level is entered so collected items and opened gates survive the return (XARGON.C saveboard
    /// serialises bd[][], objs[] and pl; killed objects persist as obj_killme).
    /// </summary>
    public Board Clone()
    {
        var b = new Board { FruitCount = FruitCount, Player = Player.Clone() };
        Array.Copy(Cells, b.Cells, Cells.Length);
        foreach (var o in Objects) b.Objects.Add(o.Clone());
        return b;
    }
}
