using System.Buffers.Binary;
using Xunit;
using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Ui;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>Task 10: savegame()/loadgame() (xargon.c:412-450) through real files in a temp UserDataStore.</summary>
public sealed class SaveLoadFlowTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xargon-savetest-" + Guid.NewGuid().ToString("N"));
    private readonly AssetSet _set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private sealed class RecordingPopups : IPopupHost
    {
        public readonly List<(int N, PopupStyle Style)> Messages = new();
        public void TextMessage(int n, PopupStyle style) => Messages.Add((n, style));
        public void DialogMessage(int n, bool eagle) { }
    }

    private UserDataStore Store() => new(_dir);
    private string SlotFile(int slot) => Path.Combine(_dir, $"save_{slot}.xr1");

    private LevelHarness Harness()
    {
        var screen = new VgaScreen();
        _set.LoadPalette(screen);
        var pl = PlayerState.NewGame();
        pl.Level = LevelSession.MapLevel;
        return new LevelHarness(n => _set.LoadBoard(n.ToUpperInvariant()), new ShapeManager(_set.Graphics), _set.Info,
            screen, pl, "MAP");
    }

    private sealed class Rig
    {
        public readonly RecordingPopups Popups = new();
        public readonly HighScoreStore Hs;
        public readonly UserDataStore Store;
        public readonly List<(string Title, string Blank)> Picks = new();
        public int Slot = 3;
        public string? Name = "MY SAVE";
        public readonly List<(int Slot, string Existing)> NameAsks = new();
        public SaveLoadFlow Flow;
        public readonly List<string> Notes = new();

        public Rig(UserDataStore store, int episode = 1)
        {
            Store = store;
            Hs = new HighScoreStore(store);
            Flow = new SaveLoadFlow(store, Hs, () => episode, Popups,
                pick: (t, b) => { Picks.Add((t, b)); return Slot; },
                askName: (s, e) => { NameAsks.Add((s, e)); return Name; },
                notify: Notes.Add);
        }
    }

    // ---- helpers ------------------------------------------------------------------------------

    /// <summary>Mutate a live map world: score, health, inventory, fruit, a consumed pickup, an opened door.</summary>
    private static void Modify(LevelHarness h)
    {
        h.Pl.Score = 123456; h.Pl.Health = 3; h.Pl.Emeralds = 77; h.Pl.Fruit = 9; h.Pl.Ouched = 2;
        h.Objects.AddInv(Inv.Rock); h.Objects.AddInv(Inv.Rock); h.Objects.AddInv(Inv.Key2);
        int victim = Enumerable.Range(1, h.Board.Objects.Count - 1)
            .First(i => h.Board.Objects[i].Kind != ObjectKind.Checkpt);
        h.Objects.KillObj(victim);                       // a consumed pickup / killed object
        h.Objects.SetBoard(20, 20, 0x4000 | 321);        // an opened door: new tile plus a runtime mod flag
        h.Objects.SetBoard(127, 63, 5);
    }

    private static void AssertWorldsEqual(LevelHarness a, LevelHarness b)
    {
        for (int x = 0; x < Board.W; x++)
            for (int y = 0; y < Board.H; y++)
                Assert.True(a.Board.Cells[x, y] == b.Board.Cells[x, y], $"cell {x},{y}");
        Assert.Equal(a.Board.Objects.Count, b.Board.Objects.Count);
        for (int i = 0; i < a.Board.Objects.Count; i++)
        {
            // ObjectManager's ctor re-sizes killed (kind Killme) objects to 0x0 on adopt (Task 3 note): never drawn or collided.
            var x = a.Board.Objects[i].Clone(); var y = b.Board.Objects[i].Clone();
            if (x.Kind == ObjectKind.Killme) x.Xl = x.Yl = 0;
            if (y.Kind == ObjectKind.Killme) y.Xl = y.Yl = 0;
            BoardWriterTests.AssertObjEqual(x, y, $"obj {i}");
        }
        BoardWriterTests.AssertPlayerEqual(a.Pl, b.Pl);
    }

    private static byte[] Snapshot(LevelHarness h) => BoardWriter.Write(h.Board, h.Pl);

    // ---- save-allowed predicate (map only) ----------------------------------------------------

    [Fact]
    public void SaveOnTheMapIsAllowed()
    {
        var h = Harness();
        Assert.True(GameFlow.IsOnMap(h.Objects));
        var rig = new Rig(Store());
        Assert.True(rig.Flow.Save(h.Objects, h.Board, h.Pl));
        Assert.Empty(rig.Popups.Messages);
        Assert.True(File.Exists(SlotFile(3)));
    }

    [Fact]
    public void SaveInsideALevelShowsMessage4AndWritesNothing()
    {
        var h = Harness();
        h.Pl.Level = 1;
        h.Enter("BOARD_01");
        Assert.False(GameFlow.IsOnMap(h.Objects));
        var rig = new Rig(Store());
        Assert.False(rig.Flow.Save(h.Objects, h.Board, h.Pl));
        Assert.Equal(new[] { (4, PopupStyle.Plain) }, rig.Popups.Messages.ToArray());
        Assert.Empty(rig.Picks);       // the slot window never opens
        Assert.Empty(rig.NameAsks);
        Assert.False(Directory.Exists(_dir)); // nothing created at all
    }

    [Fact]
    public void MapBoardPredicateMatchesTheObjectManagerOne()
    {
        var h = Harness();
        Assert.True(GameFlow.IsMapBoard(h.Board.Objects));
        h.Pl.Level = 1;
        h.Enter("BOARD_01");
        Assert.False(GameFlow.IsMapBoard(h.Board.Objects));
        Assert.False(GameFlow.IsMapBoard(new List<Obj>()));
    }

    // ---- round trip (Review Focus 3) ----------------------------------------------------------

    [Fact]
    public void SaveToSlot3ThenLoadIntoAFreshWorldRestoresEverythingExactly()
    {
        var h = Harness();
        Modify(h);
        var rig = new Rig(Store());
        Assert.True(rig.Flow.Save(h.Objects, h.Board, h.Pl));
        Assert.Equal(new[] { ("SAVE GAME", "") }, rig.Picks.ToArray());

        var h2 = Harness(); // brand-new world and player
        Assert.NotEqual(h.Pl.Score, h2.Pl.Score);
        var rig2 = new Rig(Store()) { Slot = 3 };
        var loaded = rig2.Flow.Load();
        Assert.NotNull(loaded);
        Assert.Equal(new[] { ("LOAD GAME", "<empty>") }, rig2.Picks.ToArray());
        h2.LoadSaved(loaded!, loaded!.Player);

        AssertWorldsEqual(h, h2);
        Assert.Equal(9, h2.Pl.Fruit);
        Assert.Equal(123456u, h2.Pl.Score);
        Assert.Equal(2, h2.Objects.InvCount(Inv.Rock));
        Assert.Equal(1, h2.Objects.InvCount(Inv.Key2));
    }

    [Fact]
    public void SlotNamePersistsInConfigAndHighScoresSurvive()
    {
        var store = Store();
        var seed = new ConfigFile();
        new HighScoreTable(seed).Insert(0, "TOPDOG", 999999);
        new HighScoreTable(seed).Insert(1, "RUNNER", 5000);
        Assert.True(seed.Save(store, 1));

        var h = Harness();
        var rig = new Rig(store);
        Assert.True(rig.Flow.Save(h.Objects, h.Board, h.Pl));

        var cfg = ConfigFile.Load(Store(), 1); // a second, independent load
        Assert.Equal("MY SAVE", cfg.SaveNames[3]);
        Assert.Equal("TOPDOG", cfg.HiNames[0]);
        Assert.Equal(999999u, cfg.HiScores[0]);
        Assert.Equal("RUNNER", cfg.HiNames[1]);
        Assert.Equal(5000u, cfg.HiScores[1]);
        for (int i = 0; i < 7; i++) if (i != 3) Assert.Equal("", cfg.SaveNames[i]);
    }

    [Fact]
    public void SaveFileIsLowercaseAndPerEpisode()
    {
        var h = Harness();
        var rig = new Rig(Store(), episode: 2);
        Assert.True(rig.Flow.Save(h.Objects, h.Board, h.Pl));
        Assert.Equal(new[] { "config.xr2", "save_3.xr2" }, Directory.GetFiles(_dir).Select(Path.GetFileName).OrderBy(n => n).ToArray());
        Assert.Equal("save_0.xr1", SaveGames.FileNameFor(0, 1));
        Assert.Equal("save_6.xr3", SaveGames.FileNameFor(6, 3));
    }

    [Fact]
    public void ExistingNameIsPrefilledAndMaxLengthIsNine()
    {
        var store = Store();
        var cfg = new ConfigFile(); cfg.SaveNames[2] = "OLD";
        Assert.True(cfg.Save(store, 1));
        var h = Harness();
        var rig = new Rig(store) { Slot = 2, Name = "ABCDEFGHIJKLMN" };
        Assert.True(rig.Flow.Save(h.Objects, h.Board, h.Pl));
        Assert.Equal(new[] { (2, "OLD") }, rig.NameAsks.ToArray());
        Assert.Equal("ABCDEFGHI", ConfigFile.Load(Store(), 1).SaveNames[2]); // the 10-byte field holds 9 characters
        Assert.Equal(9, SaveLoadFlow.NameMaxLen);
    }

    // ---- name entry decisions -----------------------------------------------------------------

    [Fact]
    public void EscDuringNameEntryCancelsPerRuling2()
    {
        Assert.False(SaveLoadFlow.NameEscCommits);
        var s = new TextInputState("OLD", SaveLoadFlow.NameMaxLen, SaveLoadFlow.NameEscCommits);
        s.Press('X');
        Assert.Equal(TextInputOutcome.Cancelled, s.Escape());
    }

    [Fact]
    public void CancelledOrBlankNameSavesNothing()
    {
        foreach (string? name in new string?[] { null, "" })
        {
            var h = Harness();
            var rig = new Rig(Store()) { Name = name };
            Assert.False(rig.Flow.Save(h.Objects, h.Board, h.Pl));
            Assert.False(File.Exists(SlotFile(3)));
            Assert.Equal("", ConfigFile.Load(Store(), 1).SaveNames[3]);
        }
    }

    [Fact]
    public void CancelledSlotPickSavesNothing()
    {
        var h = Harness();
        var rig = new Rig(Store()) { Slot = -1 };
        Assert.False(rig.Flow.Save(h.Objects, h.Board, h.Pl));
        Assert.Empty(rig.NameAsks);
        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public void OutOfRangeSlotFromThePickerIsIgnored()
    {
        var h = Harness();
        foreach (int slot in new[] { 7, 99, -5 })
        {
            var rig = new Rig(Store()) { Slot = slot };
            Assert.False(rig.Flow.Save(h.Objects, h.Board, h.Pl));
            Assert.Null(rig.Flow.Load());
        }
        Assert.False(Directory.Exists(_dir));
    }

    // ---- failure behaviour --------------------------------------------------------------------

    [Fact]
    public void UnavailableStoreSaveDoesNothingAndDoesNotThrow()
    {
        var h = Harness();
        var unavailable = UserDataStore.FromFolders(_ => "");
        var rig = new Rig(unavailable);
        Assert.False(rig.Flow.Save(h.Objects, h.Board, h.Pl));
        Assert.Null(rig.Flow.Load());
    }

    [Fact]
    public void FailedWriteToAnUnusedSlotStoresNoNameAndLeavesOtherSlotsIntact()
    {
        var h = Harness();
        var rig = new Rig(Store());
        Assert.True(rig.Flow.Save(h.Objects, h.Board, h.Pl));
        byte[] before = File.ReadAllBytes(SlotFile(3));

        // Make the second write to slot 3 un-replaceable by putting a directory where the move must land, via slot 4.
        Directory.CreateDirectory(SlotFile(4));
        var rig4 = new Rig(Store()) { Slot = 4, Name = "NEVER" };
        Assert.False(rig4.Flow.Save(h.Objects, h.Board, h.Pl));
        Assert.True(Directory.Exists(SlotFile(4)));
        Assert.Equal("", ConfigFile.Load(Store(), 1).SaveNames[4]);   // no name for a save that did not happen
        Assert.Equal("MY SAVE", ConfigFile.Load(Store(), 1).SaveNames[3]);
        Assert.Equal(before, File.ReadAllBytes(SlotFile(3)));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));              // no temp litter
        Assert.Equal(new[] { SaveLoadFlow.SaveFailedText }, rig4.Notes.ToArray());
    }

    [Fact]
    public void FailedOverwriteOfAnExistingSlotKeepsItsFileAndOldNameAndTellsThePlayer()
    {
        if (!OperatingSystem.IsWindows()) return; // needs a lockable target; the move-failure seam is covered in SaveGamesFileTests
        var h = Harness();
        var rig = new Rig(Store()) { Name = "FIRST" };
        Assert.True(rig.Flow.Save(h.Objects, h.Board, h.Pl));
        byte[] before = File.ReadAllBytes(SlotFile(3));
        h.Pl.Score = 999;
        var rig2 = new Rig(Store()) { Name = "SECOND" };
        using (new FileStream(SlotFile(3), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.False(rig2.Flow.Save(h.Objects, h.Board, h.Pl));
        Assert.Equal(before, File.ReadAllBytes(SlotFile(3)));
        Assert.Equal("FIRST", ConfigFile.Load(Store(), 1).SaveNames[3]);
        Assert.Equal(new[] { SaveLoadFlow.SaveFailedText }, rig2.Notes.ToArray());
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void SaveFailureWithUnavailableStoreTellsThePlayer()
    {
        var h = Harness();
        var rig = new Rig(UserDataStore.FromFolders(_ => ""));
        Assert.False(rig.Flow.Save(h.Objects, h.Board, h.Pl));
        Assert.Equal(new[] { SaveLoadFlow.SaveFailedText }, rig.Notes.ToArray());
    }

    [Fact]
    public void ConfigWriteFailureAfterAGoodFileIsReportedAndKeptInMemory()
    {
        var h = Harness();
        var rig = new Rig(Store());
        // a directory where config.xr1 must go: the save file can be written, the name cannot be persisted
        Directory.CreateDirectory(Path.Combine(_dir, "config.xr1"));
        Assert.True(rig.Flow.Save(h.Objects, h.Board, h.Pl));
        Assert.True(File.Exists(SlotFile(3)));
        Assert.Equal(new[] { SaveLoadFlow.NameNotStoredText }, rig.Notes.ToArray());
        Assert.Equal("MY SAVE", rig.Hs.Load(1).SaveNames[3]); // session-only copy, still listed this session
    }

    [Fact]
    public void LoadFailureOfANamedSlotTellsThePlayerButAnEmptySlotAndEscStaySilent()
    {
        PlantSlot3(new byte[500]);
        var rig = new Rig(Store());
        Assert.Null(rig.Flow.Load());
        Assert.Equal(new[] { SaveLoadFlow.LoadFailedText }, rig.Notes.ToArray());

        var silent = new Rig(Store()) { Slot = 5 }; // empty name
        Assert.Null(silent.Flow.Load());
        silent.Slot = -1;
        Assert.Null(silent.Flow.Load());
        Assert.Empty(silent.Notes);
    }

    [Fact]
    public void IsSaneRejectsHeroOffBoardAndNegativeNumInv()
    {
        var map = _set.LoadBoard("MAP");
        Assert.True(SaveGames.IsSane(map));
        foreach (var (x, y) in new[] { (-1, 10), (10, -1), (Board.W * 16, 10), (10, Board.H * 16) })
        {
            var b = map.Clone(); b.Objects[0].X = x; b.Objects[0].Y = y;
            Assert.False(SaveGames.IsSane(b), $"hero at {x},{y}");
        }
        var neg = map.Clone(); neg.Player.NumInv = -1;
        Assert.False(SaveGames.IsSane(neg));
        var big = map.Clone(); big.Player.NumInv = 31;
        Assert.False(SaveGames.IsSane(big));
    }

    // ---- loading: rejects hostile files without throwing (Review Focus 1) --------------------

    private byte[] ValidMapSave()
    {
        var h = Harness();
        Modify(h);
        return BoardWriter.Write(h.Board, h.Pl);
    }

    private void PlantSlot3(byte[] bytes, string name = "PLANTED")
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(SlotFile(3), bytes);
        var cfg = new ConfigFile(); cfg.SaveNames[3] = name;
        Assert.True(cfg.Save(Store(), 1));
    }

    public static IEnumerable<object[]> BadFiles()
    {
        yield return new object[] { "zero-length", 0 };
        yield return new object[] { "one byte", 1 };
        yield return new object[] { "short 100", 100 };
        yield return new object[] { "exactly the cells only", Board.W * Board.H * 2 };
        yield return new object[] { "truncated by 1", -1 };
        yield return new object[] { "truncated half", -2 };
        yield return new object[] { "trailing junk", -3 };
        yield return new object[] { "garbage", -4 };
        yield return new object[] { "oversized 1MiB+1", -5 };
        yield return new object[] { "numobjs 300", -6 };
        yield return new object[] { "numobjs 0", -7 };
        yield return new object[] { "negative numobjs", -8 };
        yield return new object[] { "hero is not the map hero", -9 };
        yield return new object[] { "level board, not a map", -10 };
        yield return new object[] { "unknown object kind", -11 };
        yield return new object[] { "numinv 500", -12 };
        yield return new object[] { "negative inside length", -13 };
    }

    private byte[] MakeBad(int code, byte[] good)
    {
        if (code >= 0) return new byte[code];
        const int objsAt = Board.W * Board.H * 2 + 2;
        switch (code)
        {
            case -1: return good[..^1];
            case -2: return good[..(good.Length / 2)];
            case -3: return good.Concat(new byte[] { 1, 2, 3 }).ToArray();
            case -4: { var g = new byte[20000]; new Random(1).NextBytes(g); return g; }
            case -5: return new byte[SaveGames.MaxFileBytes + 1];
            case -6: // 300 objects: structurally valid but over the engine's 256-object limit
            {
                var b = new byte[Board.W * Board.H * 2 + 2 + 300 * 31 + 97];
                BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(Board.W * Board.H * 2), 300);
                for (int i = 0; i < 300; i++) b[objsAt + i * 31] = (byte)ObjectKind.Tiny;
                return b;
            }
            case -7:
            {
                var b = new byte[Board.W * Board.H * 2 + 2 + 97];
                return b;
            }
            case -8:
            {
                var b = new byte[Board.W * Board.H * 2 + 2 + 97];
                BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(Board.W * Board.H * 2), -5);
                return b;
            }
            case -9: { var g = (byte[])good.Clone(); g[objsAt] = (byte)ObjectKind.Grunt; return g; }
            case -10:
            {
                var lvl = _set.LoadBoard("BOARD_01");
                return BoardWriter.Write(lvl, lvl.Player);
            }
            case -11: { var g = (byte[])good.Clone(); g[objsAt + 31] = 250; return g; }
            case -12:
            {
                var g = (byte[])good.Clone();
                int plAt = objsAt + (BinaryPrimitives.ReadInt16LittleEndian(good.AsSpan(Board.W * Board.H * 2)) * 31);
                BinaryPrimitives.WriteInt16LittleEndian(g.AsSpan(plAt + 6), 500);
                return g;
            }
            case -13:
            {
                // flip the first inside length to a negative number
                var lvl = _set.LoadBoard("MAP");
                var bytes = BoardWriter.Write(lvl, lvl.Player);
                int at = objsAt + lvl.Objects.Count * 31 + 97;
                Assert.True(at + 2 <= bytes.Length, "MAP has inside text");
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at), -7);
                return bytes;
            }
        }
        throw new InvalidOperationException();
    }

    [Theory]
    [MemberData(nameof(BadFiles))]
    public void CorruptSaveIsRejectedWithoutThrowingAndGameIsUntouched(string label, int code)
    {
        var h = Harness();
        Modify(h);
        byte[] runningBefore = Snapshot(h);
        PlantSlot3(MakeBad(code, ValidMapSave()));
        var rig = new Rig(Store());
        Board? b = null;
        var ex = Record.Exception(() => b = rig.Flow.Load());
        Assert.Null(ex);
        Assert.True(b is null, $"{label} must not load");
        Assert.Equal(runningBefore, Snapshot(h)); // the running game was not touched
        Assert.Equal(new[] { ("LOAD GAME", "<empty>") }, rig.Picks.ToArray());
    }

    [Fact]
    public void SaveWithAnObjectCarryingInsideTextIsAcceptedBackByTheValidator()
    {
        // The shipped MAP carries inside texts (level names); the sanity check must accept what the game writes.
        var h = Harness();
        Assert.Contains(h.Board.Objects, o => o.HadInside);
        Assert.NotNull(SaveGames.TryParse(BoardWriter.Write(h.Board, h.Pl)));
    }

    [Fact]
    public void MissingFileAndDirectoryInPlaceOfFileReturnNull()
    {
        var cfg = new ConfigFile(); cfg.SaveNames[3] = "GHOST";
        Assert.True(cfg.Save(Store(), 1));
        var rig = new Rig(Store());
        Assert.Null(rig.Flow.Load());                  // name present, file missing (the C crashes here)
        Directory.CreateDirectory(SlotFile(3));
        Assert.Null(rig.Flow.Load());                  // directory where the file should be
    }

    [Fact]
    public void EmptySlotNameLoadsNothingEvenIfAFileExists()
    {
        PlantSlot3(ValidMapSave(), name: "");
        var rig = new Rig(Store());
        Assert.Null(rig.Flow.Load());
        rig.Slot = -1;
        Assert.Null(rig.Flow.Load());                  // Esc
    }

    [Fact]
    public void WrongEpisodeContentIsNotLoadedAsAnotherEpisodesSave()
    {
        var h = Harness();
        var rig = new Rig(Store(), episode: 1);
        Assert.True(rig.Flow.Save(h.Objects, h.Board, h.Pl));
        // episode 2 reads save_3.xr2, which does not exist, and its config has no names
        var rig2 = new Rig(Store(), episode: 2);
        Assert.Null(rig2.Flow.Load());
    }

    // ---- loading from inside a level discards it (Review Focus 3) -----------------------------

    [Fact]
    public void LoadInsideALevelDiscardsTheLevelAndTheStashedMap()
    {
        var saved = Harness();
        Modify(saved);
        var rig = new Rig(Store());
        Assert.True(rig.Flow.Save(saved.Objects, saved.Board, saved.Pl));

        var h = Harness();
        h.Pl.Level = 1;
        h.Enter("BOARD_01");
        Assert.True(h.Session.InLevel);
        h.Pl.Score = 7;

        var loaded = new Rig(Store()).Flow.Load();
        Assert.NotNull(loaded);
        h.LoadSaved(loaded!, loaded!.Player);

        Assert.False(h.Session.InLevel);
        Assert.True(GameFlow.IsOnMap(h.Objects));
        Assert.Equal(123456u, h.Pl.Score);
        AssertWorldsEqual(saved, h);

        // ExitToMap must not resurrect the old (pre-level) map: the stash is gone, so it re-adopts the loaded one.
        h.ExitToMap();
        for (int x = 0; x < Board.W; x++)
            for (int y = 0; y < Board.H; y++)
                Assert.True(saved.Board.Cells[x, y] == h.Board.Cells[x, y], $"cell {x},{y}");
        Assert.Equal(0x4000 | 321, h.Board.Cells[20, 20]);
    }

    [Fact]
    public void LoadedMapKeepsLoadedPositionAndHealthWithoutReenter()
    {
        var saved = Harness();
        Modify(saved);
        saved.Objects.Obj(0).X = 400; saved.Objects.Obj(0).Y = 300;
        var rig = new Rig(Store());
        Assert.True(rig.Flow.Save(saved.Objects, saved.Board, saved.Pl));

        var h = Harness();
        var loaded = new Rig(Store()).Flow.Load()!;
        h.LoadSaved(loaded, loaded.Player);
        Assert.Equal((400, 300), (h.Objects.Obj(0).X, h.Objects.Obj(0).Y));
        Assert.Equal(3, h.Pl.Health); // p_reenter would restore 5
    }
}
