using Xunit;
using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>Task 3: BoardWriter is the exact inverse of BoardLoader; LevelSession.LoadSaved is loadgame().</summary>
public class BoardWriterTests
{
    private static string Dir => AssetSet.FindAssetsDir();

    public static IEnumerable<object[]> AllBoards()
    {
        string dir = AssetSet.FindAssetsDir();
        for (int ep = 1; ep <= 3; ep++)
            foreach (var path in Directory.EnumerateFiles(dir, $"*.XR{ep}").OrderBy(p => p, StringComparer.Ordinal))
            {
                string stem = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
                if (stem.StartsWith("BOARD_") || stem is "INTRO" or "MAP" or "STORY")
                    yield return new object[] { ep, stem };
            }
    }

    // ---- comparison helpers ----------------------------------------------------------------

    internal static void AssertPlayerEqual(PlayerState a, PlayerState b)
    {
        Assert.Equal(a.Level, b.Level);
        Assert.Equal(a.Health, b.Health);
        Assert.Equal(a.Emeralds, b.Emeralds);
        Assert.Equal(a.NumInv, b.NumInv);
        Assert.Equal(a.Ouched, b.Ouched);
        Assert.Equal(a.Score, b.Score);
        Assert.Equal(a.Fruit, b.Fruit);
        Assert.Equal(a.Inv, b.Inv);
    }

    internal static void AssertObjEqual(Obj a, Obj b, string ctx)
    {
        Assert.True(a.Kind == b.Kind && a.X == b.X && a.Y == b.Y && a.Xd == b.Xd && a.Yd == b.Yd &&
                    a.Xl == b.Xl && a.Yl == b.Yl && a.State == b.State && a.SubState == b.SubState &&
                    a.StateCount == b.StateCount && a.Counter == b.Counter && a.Flags == b.Flags &&
                    a.Info1 == b.Info1 && a.ZapHold == b.ZapHold, $"{ctx}: scalar field mismatch");
        Assert.True(a.HadInside == b.HadInside, $"{ctx}: HadInside");
        Assert.True(a.Inside == b.Inside, $"{ctx}: Inside '{a.Inside}' vs '{b.Inside}'");
    }

    internal static void AssertBoardEqual(Board a, Board b)
    {
        for (int x = 0; x < Board.W; x++)
            for (int y = 0; y < Board.H; y++)
                Assert.True(a.Cells[x, y] == b.Cells[x, y], $"cell {x},{y}: {a.Cells[x, y]} vs {b.Cells[x, y]}");
        Assert.Equal(a.Objects.Count, b.Objects.Count);
        for (int i = 0; i < a.Objects.Count; i++) AssertObjEqual(a.Objects[i], b.Objects[i], $"obj {i}");
        Assert.Equal(a.FruitCount, b.FruitCount);
        AssertPlayerEqual(a.Player, b.Player);
    }

    // ---- round trips over every shipped board -------------------------------------------------

    [Theory]
    [MemberData(nameof(AllBoards))]
    public void ShippedBoardRoundTripsSemantically(int ep, string name)
    {
        var b = BoardLoader.Load(Path.Combine(Dir, $"{name}.XR{ep}"));
        byte[] w = BoardWriter.Write(b, b.Player);
        AssertBoardEqual(b, BoardLoader.Parse(w, name));
        Assert.Equal(w, BoardWriter.Write(b, b.Player)); // deterministic
    }

    /// <summary>
    /// Byte-for-byte equality with the original file EXCEPT the 4-byte saved inside far pointer of objects
    /// that carry text: those are stale DOS heap addresses (differ per object, meaningless on reload) and
    /// cannot be reproduced; only their non-zero-ness is preserved. Everything else (incl. pad[22] = 0) is exact.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllBoards))]
    public void ShippedBoardWritesOriginalBytesExceptStaleInsidePointers(int ep, string name)
    {
        byte[] orig = File.ReadAllBytes(Path.Combine(Dir, $"{name}.XR{ep}"));
        var b = BoardLoader.Parse(orig, name);
        byte[] w = BoardWriter.Write(b, b.Player);
        Assert.Equal(orig.Length, w.Length);
        const int objsAt = Board.W * Board.H * 2 + 2;
        var masked = new bool[orig.Length];
        for (int i = 0; i < b.Objects.Count; i++)
        {
            int ptr = objsAt + i * 31 + 23;
            Assert.Equal(b.Objects[i].HadInside, w[ptr] != 0 || w[ptr + 1] != 0 || w[ptr + 2] != 0 || w[ptr + 3] != 0);
            if (b.Objects[i].HadInside) for (int k = 0; k < 4; k++) masked[ptr + k] = true;
        }
        for (int i = 0; i < orig.Length; i++)
            if (!masked[i]) Assert.True(orig[i] == w[i], $"{name}.XR{ep}: byte {i} differs ({orig[i]:X2} vs {w[i]:X2})");
    }

    [Fact]
    public void BoardsWithoutInsideTextAreByteIdentical()
    {
        foreach (var args in AllBoards())
        {
            byte[] orig = File.ReadAllBytes(Path.Combine(Dir, $"{args[1]}.XR{args[0]}"));
            var b = BoardLoader.Parse(orig);
            if (b.Objects.Any(o => o.HadInside)) continue;
            Assert.True(orig.AsSpan().SequenceEqual(BoardWriter.Write(b, b.Player)), $"{args[1]}.XR{args[0]}");
        }
    }

    // ---- synthetic edge cases ---------------------------------------------------------------------

    private static Board Synth(params Obj[] objs)
    {
        var b = new Board();
        b.Objects.AddRange(objs);
        return b;
    }

    private static (Board, PlayerState) RoundTrip(Board b, PlayerState pl)
    {
        var r = BoardLoader.Parse(BoardWriter.Write(b, pl));
        return (r, r.Player);
    }

    [Fact]
    public void ZeroObjectsRoundTrips()
    {
        var b = Synth();
        b.Cells[127, 63] = 0xC123; b.Cells[0, 1] = 7;
        var (r, _) = RoundTrip(b, new PlayerState());
        Assert.Empty(r.Objects);
        Assert.Equal(0xC123, r.Cells[127, 63]);
        Assert.Equal(7, r.Cells[0, 1]);
    }

    [Fact]
    public void InsideStringsEmptyLongAndNone()
    {
        string longS = new string('x', 32767);
        var b = Synth(
            new Obj { Kind = 1, Inside = "" },
            new Obj { Kind = 2, Inside = longS },
            new Obj { Kind = 3 },
            new Obj { Kind = 4, Inside = "hello world" });
        var (r, _) = RoundTrip(b, new PlayerState());
        Assert.True(r.Objects[0].HadInside); Assert.Equal("", r.Objects[0].Inside);
        Assert.Equal(longS, r.Objects[1].Inside);
        Assert.False(r.Objects[2].HadInside); Assert.Null(r.Objects[2].Inside);
        Assert.Equal("hello world", r.Objects[3].Inside);
    }

    [Fact]
    public void InsideNonAsciiBecomesQuestionMarkAndNulTruncates()
    {
        var b = Synth(new Obj { Kind = 1, Inside = "café" }, new Obj { Kind = 2, Inside = "ab\0cd" });
        var (r, _) = RoundTrip(b, new PlayerState());
        Assert.Equal("caf?", r.Objects[0].Inside);
        Assert.Equal("ab", r.Objects[1].Inside);
    }

    [Fact]
    public void HadInsideWithNullInsideWritesEmptyString()
    {
        var (r, _) = RoundTrip(Synth(new Obj { Kind = 1, HadInside = true }), new PlayerState());
        Assert.Equal("", r.Objects[0].Inside);
    }

    [Fact]
    public void MaxObjectsAcceptedAndOverLimitRefused()
    {
        var b = Synth();
        for (int i = 0; i < 256; i++) b.Objects.Add(new Obj { Kind = (byte)(i & 0xff), X = i });
        var (r, _) = RoundTrip(b, new PlayerState());
        Assert.Equal(256, r.Objects.Count);
        b.Objects.Add(new Obj());
        Assert.Throws<ArgumentException>(() => BoardWriter.Write(b, new PlayerState()));
    }

    [Fact]
    public void OverlongInsideRefused()
    {
        var b = Synth(new Obj { Kind = 1, Inside = new string('y', 32768) });
        Assert.Throws<ArgumentException>(() => BoardWriter.Write(b, new PlayerState()));
    }

    [Fact]
    public void PlayerStateAllFieldsIncludingFruitRoundTrip()
    {
        var pl = new PlayerState { Level = 127, Health = 3, Emeralds = 99, NumInv = 4, Ouched = 2, Score = 4000000000u, Fruit = 11, FruitIcon = 2 };
        for (int i = 0; i < 30; i++) pl.Inv[i] = i * 3 - 5;
        var (_, p) = RoundTrip(Synth(), pl);
        AssertPlayerEqual(pl, p);
        Assert.Equal(0, p.FruitIcon); // icon_fruit is never saved
    }

    [Fact]
    public void WriterUsesPlParameterNotBoardPlayer()
    {
        var b = Synth(); b.Player.Score = 5; b.Player.Fruit = 1;
        var pl = new PlayerState { Score = 99, Fruit = 9 };
        var (_, p) = RoundTrip(b, pl);
        Assert.Equal(99u, p.Score); Assert.Equal(9, p.Fruit);
    }

    // ---- LoadSaved ------------------------------------------------------------------------------

    private static LevelHarness Harness(PlayerState pl)
    {
        var set = AssetSet.Mount(Dir, 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        return new LevelHarness(set.LoadBoard, new ShapeManager(set.Graphics), set.Info, screen, pl, "MAP");
    }

    private static PlayerState MapPl() => new() { Health = 5, Level = LevelSession.MapLevel, NumInv = 1 };

    [Fact]
    public void LoadSavedRestoresWorldAndPlayerExactly()
    {
        var pl = MapPl();
        var h = Harness(pl);
        // mid-game state: consumed object, changed cell, edited player incl. fruit and inventory
        int victim = Enumerable.Range(1, h.Board.Objects.Count - 1).First(i => h.Board.Objects[i].Kind != ObjectKind.Checkpt);
        h.Objects.KillObj(victim);
        h.Board.Cells[10, 10] = 0x2345;
        pl.Score = 12345; pl.Health = 3; pl.Emeralds = 42; pl.Fruit = 9; pl.Level = 2;
        pl.NumInv = 3; pl.Inv[1] = Inv.Key(1); pl.Inv[2] = Inv.Rock;
        var expect = h.Board.Clone();
        var expectPl = pl.Clone();
        byte[] save = BoardWriter.Write(h.Board, pl);
        expect.Objects[victim].Xl = expect.Objects[victim].Yl = 0; // ObjectManager ctor re-sizes killed objects (kind Killme) to 0x0; irrelevant, never drawn or collided
        expect.Player = expectPl;
        expect.FruitCount = (byte)pl.Fruit;

        // a different session in a level, with dirtier state
        var pl2 = MapPl();
        var h2 = Harness(pl2);
        pl2.Level = 1; pl2.Score = 7;
        h2.Enter("BOARD_01");
        Assert.True(h2.Session.InLevel);

        var parsed = BoardLoader.Parse(save, "save_0");
        h2.LoadSaved(parsed, parsed.Player);

        for (int i = 0; i < expect.Objects.Count; i++)
        {
            var a = expect.Objects[i]; var c = h2.Board.Objects[i];
            Assert.True(a.Kind == c.Kind && a.X == c.X && a.Y == c.Y && a.Xd == c.Xd && a.Yd == c.Yd, $"obj {i} pos {a.Kind}/{c.Kind} {a.X},{a.Y} vs {c.X},{c.Y}");
            Assert.True(a.Xl == c.Xl && a.Yl == c.Yl, $"obj {i} size {a.Xl}x{a.Yl} vs {c.Xl}x{c.Yl}");
            Assert.True(a.State == c.State && a.SubState == c.SubState && a.StateCount == c.StateCount && a.Counter == c.Counter, $"obj {i} st {a.State},{a.SubState},{a.StateCount},{a.Counter} vs {c.State},{c.SubState},{c.StateCount},{c.Counter}");
            Assert.True(a.Flags == c.Flags && a.Info1 == c.Info1 && a.ZapHold == c.ZapHold, $"obj {i} fl {a.Flags},{a.Info1},{a.ZapHold} vs {c.Flags},{c.Info1},{c.ZapHold}");
        }
        AssertBoardEqual(expect, h2.Board);
        AssertPlayerEqual(expectPl, h2.Pl);
        Assert.Equal(3, h2.Pl.Health);                  // no p_reenter health reset (C loadgame has none)
        Assert.Equal(expect.Objects[0].X, h2.Board.Objects[0].X); // hero where it was saved
        Assert.Equal(expect.Objects[0].Y, h2.Board.Objects[0].Y);
        Assert.NotNull(h2.MapHero);
        Assert.False(h2.Session.InLevel);               // stashed map reset
    }

    [Fact]
    public void EnterLevelAfterLoadStashesLoadedMapAndReturns()
    {
        var pl = MapPl();
        var h = Harness(pl);
        h.Board.Cells[11, 11] = 0x1234;
        pl.Score = 500; pl.Fruit = 4;
        var parsed = BoardLoader.Parse(BoardWriter.Write(h.Board, pl));

        var pl2 = MapPl();
        var h2 = Harness(pl2);
        h2.LoadSaved(parsed, parsed.Player);
        pl2.Level = 1;
        h2.Enter("BOARD_01");
        Assert.True(h2.Session.InLevel);
        Assert.Equal(500u, h2.Pl.Score);
        Assert.Equal(4, h2.Pl.Fruit);
        h2.ExitToMap();
        Assert.Equal(0x1234, h2.Board.Cells[11, 11]);   // the loaded map came back, not the pristine MAP
        Assert.Equal(500u, h2.Pl.Score);
    }
}
