using Xunit;
using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>Final fix wave: hostile checkpoint "inside" names, atomic level entry, the high-score read-failure guard.</summary>
public sealed class FinalFixTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xargon-finalfix-" + Guid.NewGuid().ToString("N"));
    private readonly AssetSet _set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);

    public FinalFixTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private static Board MapWithCheckpointInside(string? inside)
    {
        var b = new Board();
        b.Objects.Add(new Obj { Kind = ObjectKind.Tiny, X = 100, Y = 100 });
        b.Objects.Add(new Obj { Kind = ObjectKind.Checkpt, Counter = 0, State = 5, X = 16, Y = 16 });
        b.Objects.Add(new Obj { Kind = ObjectKind.Checkpt, Counter = 1, State = 0, X = 64, Y = 64, Inside = inside, HadInside = inside is not null });
        b.Player = PlayerState.NewGame();
        return b;
    }

    private static byte[] ImageWith(string inside) =>
        BoardWriter.Write(MapWithCheckpointInside(inside), PlayerState.NewGame());

    public static IEnumerable<object[]> HostileNames() => new[]
    {
        new object[] { @"..\..\x" }, new object[] { @"C:\x" }, new object[] { "/etc/x" },
        new object[] { new string('a', 5000) },
    };

    [Theory, MemberData(nameof(HostileNames))]
    public void HostileCheckpointInsideIsRejectedByTheValidatorAndTheFileLoad(string inside)
    {
        var bytes = ImageWith(inside);
        Assert.Null(SaveGames.TryParse(bytes));

        var store = new UserDataStore(_dir);
        Assert.True(store.EnsureDirectory());
        File.WriteAllBytes(store.PathFor(SaveGames.FileNameFor(2, 1)), bytes);
        Assert.Null(SaveGames.TryRead(store, 1, 2));
    }

    [Fact]
    public void EmbeddedNulIsNeverAccepted()
    {
        Assert.False(BoardNames.IsCheckpointInside("board_01\0x"));
        Assert.False(BoardNames.IsCheckpointInside("\0"));
        // The board format stores C strings, so a NUL ends the text on load: what survives is the plain prefix.
        var loaded = SaveGames.TryParse(ImageWith("board_01\0..\\x"));
        Assert.True(loaded is null || loaded.Objects[2].Inside == "board_01");
    }

    [Theory]
    [InlineData("board_01"), InlineData("board_99"), InlineData("!"), InlineData("#song_0.xr1"), InlineData("*song_10.XR3"),
     InlineData("&end_mac.xr1"), InlineData("")]
    public void ShippedShapesAndPlainNamesPassTheValidator(string inside) =>
        Assert.NotNull(SaveGames.TryParse(ImageWith(inside)));

    [Fact]
    public void NullInsideOnACheckpointPasses() => Assert.NotNull(SaveGames.TryParse(
        BoardWriter.Write(MapWithCheckpointInside(null), PlayerState.NewGame())));

    [Fact]
    public void EveryCheckpointInsideOfEveryShippedBoardPassesTheValidator()
    {
        int boards = 0, checkpoints = 0;
        var seen = new SortedSet<string>();
        foreach (var f in Directory.GetFiles(AssetSet.FindAssetsDir(), "*.XR?"))
        {
            string n = Path.GetFileNameWithoutExtension(f);
            if (!(n.StartsWith("BOARD_") || n is "MAP" or "INTRO" or "STORY")) continue;
            var b = BoardLoader.Load(f); // a shipped board that fails to load fails the test
            boards++;
            foreach (var o in b.Objects.Where(o => o.Kind == ObjectKind.Checkpt))
            {
                checkpoints++;
                seen.Add(o.Inside ?? "");
                Assert.True(BoardNames.IsCheckpointInside(o.Inside), $"{Path.GetFileName(f)}: '{o.Inside}'");
            }
        }
        Assert.True(boards > 0);
        Assert.True(checkpoints > 0);
        Assert.Contains("!", seen);
        Assert.Contains("board_01", seen);
    }

    [Theory]
    [InlineData("BOARD_01"), InlineData("MAP"), InlineData("INTRO"), InlineData("STORY")]
    public void PathForAcceptsPlainNames(string name) => Assert.StartsWith(_set.Dir, _set.PathFor(name));

    [Theory]
    [InlineData(@"..\..\x"), InlineData(@"C:\x"), InlineData("/etc/x"), InlineData("a/b"), InlineData(""), InlineData("a.b"), InlineData("x\0")]
    public void PathForRefusesNamesThatCouldLeaveTheDataFolder(string name) =>
        Assert.Throws<ArgumentException>(() => _set.PathFor(name));

    // ---- atomic level entry -------------------------------------------------------------------

    private LevelHarness Harness(Func<string, Board>? loader = null)
    {
        var screen = new VgaScreen();
        _set.LoadPalette(screen);
        var pl = PlayerState.NewGame();
        pl.Level = LevelSession.MapLevel;
        return new LevelHarness(loader ?? (n => _set.LoadBoard(n.ToUpperInvariant())), new ShapeManager(_set.Graphics), _set.Info, screen, pl, "MAP");
    }

    [Theory]
    [InlineData("board_99"), InlineData(@"..\..\x"), InlineData(@"C:\x"), InlineData("/etc/x")]
    public void FailedLevelEntryLeavesEverythingUntouchedAndShowsAMessage(string name)
    {
        int loaderCalls = 0;
        var h = Harness(n => { loaderCalls++; return _set.LoadBoard(n.ToUpperInvariant()); });
        int callsAfterMap = loaderCalls;
        h.Pl.Level = 1;
        var boardBefore = h.Board;
        var objectsBefore = h.Objects;
        byte[] plBefore = BoardWriter.Write(h.Board, h.Pl);

        h.Objects.NewLevel = name; // what a map checkpoint does
        var ex = Record.Exception(() => h.Step());

        Assert.Null(ex);
        Assert.Null(h.Objects.NewLevel);                       // consumed: it cannot repeat every frame
        Assert.Same(boardBefore, h.Board);                     // the current world is still the map
        Assert.Same(objectsBefore, h.Objects);
        Assert.False(h.Session.InLevel);                       // nothing stashed
        Assert.Equal(GameFlow.LevelFailedText, h.Objects.Status.Message);
        Assert.Equal(1, h.Pl.Level);
        Assert.NotNull(h.MapHero);
        Assert.Equal(plBefore.Length, BoardWriter.Write(h.Board, h.Pl).Length);
        if (name != "board_99") Assert.Equal(callsAfterMap, loaderCalls); // an invalid name never reaches the loader

        h.Step();                                              // and the next frame is calm
        Assert.False(h.Session.InLevel);
    }

    [Fact]
    public void FailedEntryThenAValidOneStillWorks()
    {
        var h = Harness();
        h.Pl.Level = 1;
        Assert.False(h.TryEnter("board_99"));
        Assert.True(h.TryEnter("BOARD_01"));
        Assert.True(h.Session.InLevel);
        Assert.Null(h.MapHero);
    }

    [Fact]
    public void TryEnterLevelReportsFailureWithoutThrowing()
    {
        var h = Harness();
        h.Pl.Level = 1;
        Assert.False(h.Session.TryEnterLevel("board_99", out _));
        Assert.Throws<InvalidDataException>(() => h.Session.EnterLevel("board_99"));
        Assert.False(h.Session.InLevel);
    }

    [Fact]
    public void BoardNamePlainnessRules()
    {
        Assert.True(BoardNames.IsPlain("board_01"));
        Assert.False(BoardNames.IsPlain(new string('a', 33)));
        Assert.False(BoardNames.IsPlain(null));
        Assert.False(BoardNames.IsSongMarker("#"));
        Assert.False(BoardNames.IsSongMarker("#..\\x"));
        Assert.False(BoardNames.IsSongMarker("#a.b.c"));
        Assert.False(BoardNames.IsCheckpointInside("!x"));
    }

    // ---- high scores: a failed read must not be overwritten -----------------------------------

    private static void WriteRealScores(UserDataStore store)
    {
        var c = new ConfigFile();
        c.HiScores[0] = 4242; c.HiNames[0] = "REAL";
        Assert.True(c.Save(store, 1));
    }

    [Fact]
    public void ReadFailureIsReportedDistinctlyFromMissingAndCorrupt()
    {
        var store = new UserDataStore(_dir);
        Assert.Equal(ConfigFile.LoadStatus.Missing, ConfigFile.TryLoad(store, 1, out _));
        File.WriteAllBytes(store.PathFor("config.xr1"), new byte[5]);
        Assert.Equal(ConfigFile.LoadStatus.Corrupt, ConfigFile.TryLoad(store, 1, out _));
        WriteRealScores(store);
        Assert.Equal(ConfigFile.LoadStatus.Ok, ConfigFile.TryLoad(store, 1, out var c));
        Assert.Equal(4242u, c.HiScores[0]);
        Assert.Equal(ConfigFile.LoadStatus.ReadFailed,
            ConfigFile.TryLoad(store, 1, out _, _ => throw new IOException("locked")));
    }

    [Fact]
    public void AFailedReadNeverLetsTheNextSaveOverwriteTheRealScores_Seam()
    {
        var store = new UserDataStore(_dir);
        WriteRealScores(store);
        bool fail = true;
        var hs = new HighScoreStore(store, p => fail ? throw new IOException("antivirus") : File.ReadAllBytes(p));
        var shown = hs.Load(1);
        Assert.Equal(0u, shown.HiScores[0]);                   // defaults shown after the failed read
        new HighScoreTable(shown).Insert(0, "NEW", 5);
        Assert.False(hs.Save(1, shown));                       // refused: session-only
        Assert.Equal(5u, hs.Load(1).HiScores[0]);              // still visible this session
        fail = false;
        Assert.Equal(4242u, ConfigFile.Load(store, 1).HiScores[0]); // the real file is intact
    }

    [Fact]
    public void ALockedConfigFileIsNotOverwrittenOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;
        var store = new UserDataStore(_dir);
        WriteRealScores(store);
        var hs = new HighScoreStore(store);
        ConfigFile shown;
        using (new FileStream(store.PathFor("config.xr1"), FileMode.Open, FileAccess.Read, FileShare.None))
            shown = hs.Load(1);
        new HighScoreTable(shown).Insert(0, "NEW", 5);
        Assert.False(hs.Save(1, shown));
        Assert.Equal(4242u, ConfigFile.Load(store, 1).HiScores[0]);
    }

    [Fact]
    public void MissingOrCorruptConfigStillSavesNormally()
    {
        var store = new UserDataStore(_dir);
        var hs = new HighScoreStore(store);
        var c = hs.Load(1);                                    // missing: fine
        c.HiScores[0] = 9;
        Assert.True(hs.Save(1, c));
        File.WriteAllBytes(store.PathFor("config.xr2"), new byte[3]);
        var c2 = hs.Load(2);                                   // wrong length: corrupt, overwriting is intended
        c2.HiScores[0] = 8;
        Assert.True(hs.Save(2, c2));
        Assert.Equal(8u, ConfigFile.Load(store, 2).HiScores[0]);
    }

    [Fact]
    public void ConfigSaveStillWritesExactly232BytesAndLeavesNoTemp()
    {
        var store = new UserDataStore(_dir);
        Assert.True(new ConfigFile().Save(store, 3));
        Assert.Equal(232, new FileInfo(store.PathFor("config.xr3")).Length);
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void LastLoadFailedIsTrueOnlyForANamedSlotThatCannotBeRead()
    {
        var store = new UserDataStore(_dir);
        var hs = new HighScoreStore(store);
        int slot = 1;
        var flow = new SaveLoadFlow(store, hs, () => 1, NullPopupHost.Instance, (_, _) => slot, (_, _) => "X");
        Assert.Null(flow.Load());                      // empty slot: silent
        Assert.False(flow.LastLoadFailed);

        var cfg = hs.Load(1);
        cfg.SaveNames[1] = "NAMED";
        Assert.True(hs.Save(1, cfg));
        Assert.Null(flow.Load());                      // named but no file: failed
        Assert.True(flow.LastLoadFailed);

        slot = -1;                                     // Esc
        Assert.Null(flow.Load());
        Assert.False(flow.LastLoadFailed);
    }
}
