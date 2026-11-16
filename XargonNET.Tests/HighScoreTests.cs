using Xunit;
using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Ui;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>Task 5: printhi() qualification/insertion (xargon.c:740-783), display layout and rest() (xargon.c:516).</summary>
public sealed class HighScoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xargon-hstest-" + Guid.NewGuid().ToString("N"));
    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private static HighScoreTable Table(params uint[] scores)
    {
        var cfg = new ConfigFile();
        for (int i = 0; i < scores.Length; i++) { cfg.HiScores[i] = scores[i]; cfg.HiNames[i] = "N" + i; }
        return new HighScoreTable(cfg);
    }

    private static HighScoreTable Full() => Table(1000, 900, 800, 700, 600, 500, 400, 300, 200, 100);

    // ---- qualification ----

    [Fact]
    public void EqualToLastEntryDoesNotQualifyOneMoreDoes()
    {
        var t = Full();
        Assert.Equal(-1, t.Qualifies(100));
        Assert.Equal(9, t.Qualifies(101));
    }

    [Fact]
    public void BelowLastEntryDoesNotQualify() => Assert.Equal(-1, Full().Qualifies(50));

    [Fact]
    public void ZeroNeverQualifiesEvenOnEmptyTable()
    {
        Assert.Equal(-1, new HighScoreTable(new ConfigFile()).Qualifies(0));
        Assert.Equal(0, new HighScoreTable(new ConfigFile()).Qualifies(1));
    }

    [Fact]
    public void TiesRankBelowExistingEqualScores()
    {
        var t = Full();
        Assert.Equal(2, t.Qualifies(900));  // equal to entry 1: ranks below it
        Assert.Equal(1, t.Qualifies(901));
        Assert.Equal(1, t.Qualifies(1000)); // equal to the top: second
        Assert.Equal(0, t.Qualifies(1001));
    }

    [Fact]
    public void QualifyingPositionsTopMiddleBottom()
    {
        var t = Full();
        Assert.Equal(0, t.Qualifies(5000));
        Assert.Equal(5, t.Qualifies(550));
        Assert.Equal(9, t.Qualifies(150));
    }

    [Fact]
    public void PartiallyFilledTableInsertsAfterLastNonZero()
    {
        var t = Table(500, 300);
        Assert.Equal(2, t.Qualifies(300));
        Assert.Equal(1, t.Qualifies(400));
        Assert.Equal(2, t.Qualifies(1));
    }

    // ---- insertion ----

    [Fact]
    public void InsertShiftsDownAndDropsLast()
    {
        var t = Full();
        t.Insert(4, "NEW", 650);
        Assert.Equal(new uint[] { 1000, 900, 800, 700, 650, 600, 500, 400, 300, 200 },
            Enumerable.Range(0, 10).Select(t.Score).ToArray());
        Assert.Equal("N3", t.Name(3));
        Assert.Equal("NEW", t.Name(4));
        Assert.Equal("N4", t.Name(5));   // names move with their scores
        Assert.Equal("N8", t.Name(9));   // old 9th (N9) dropped
    }

    [Fact]
    public void InsertAtTopAndBottom()
    {
        var t = Full();
        t.Insert(0, "TOP", 5000);
        Assert.Equal(5000u, t.Score(0));
        Assert.Equal(1000u, t.Score(1));
        Assert.Equal(200u, t.Score(9));
        var b = Full();
        b.Insert(9, "LOW", 150);
        Assert.Equal(200u, b.Score(8));
        Assert.Equal(150u, b.Score(9));
        Assert.Equal("LOW", b.Name(9));
    }

    [Fact]
    public void InsertWithEmptyNameLeavesRowEmptyUntilNamed()
    {
        var t = Full();
        t.Insert(2, "", 850);
        Assert.Equal("", t.Name(2));
        Assert.Equal(850u, t.Score(2));
        t.SetName(2, "ZED");
        Assert.Equal("ZED", t.Name(2));
        t.ClearName(2);
        Assert.Equal("", t.Name(2));
        Assert.Equal(850u, t.Score(2));
    }

    [Fact]
    public void NamesAreTruncatedToEightCharacters()
    {
        var t = Full();
        t.Insert(0, "ABCDEFGHIJKL", 5000);
        Assert.Equal("ABCDEFGH", t.Name(0));
        t.SetName(1, "123456789");
        Assert.Equal("12345678", t.Name(1));
        t.SetName(1, "EXACT123");
        Assert.Equal("EXACT123", t.Name(1));
    }

    [Fact]
    public void InsertWritesThroughToTheConfigFile()
    {
        var cfg = new ConfigFile();
        new HighScoreTable(cfg).Insert(0, "ME", 77);
        Assert.Equal("ME", cfg.HiNames[0]);
        Assert.Equal(77u, cfg.HiScores[0]);
    }

    // ---- per-episode separation and persistence ----

    [Fact]
    public void EpisodesAreSeparateFiles()
    {
        var store = new HighScoreStore(new UserDataStore(_dir));
        var c1 = store.Load(1);
        new HighScoreTable(c1).Insert(0, "ONE", 111);
        Assert.True(store.Save(1, c1));
        Assert.Equal(0u, new HighScoreStore(new UserDataStore(_dir)).Load(2).HiScores[0]);
        var c2 = store.Load(2);
        new HighScoreTable(c2).Insert(0, "TWO", 222);
        Assert.True(store.Save(2, c2));
        var fresh = new HighScoreStore(new UserDataStore(_dir));
        Assert.Equal("ONE", fresh.Load(1).HiNames[0]);
        Assert.Equal(111u, fresh.Load(1).HiScores[0]);
        Assert.Equal("TWO", fresh.Load(2).HiNames[0]);
        Assert.Equal(0u, fresh.Load(3).HiScores[0]);
    }

    [Fact]
    public void RoundTripKeepsSaveNamesAndTail()
    {
        var us = new UserDataStore(_dir);
        var c = new ConfigFile();
        c.SaveNames[3] = "slot3";
        c.Tail[0] = 9;
        new HighScoreTable(c).Insert(0, "ABCDEFGH", 4_000_000_000u);
        Assert.True(c.Save(us, 1));
        var l = new HighScoreStore(us).Load(1);
        Assert.Equal("ABCDEFGH", l.HiNames[0]);
        Assert.Equal(4_000_000_000u, l.HiScores[0]);
        Assert.Equal("slot3", l.SaveNames[3]);
        Assert.Equal(9, l.Tail[0]);
    }

    [Fact]
    public void UnavailableStoreDoesNotThrowAndKeepsScoresForTheSession()
    {
        var unavailable = UserDataStore.FromFolders(_ => "");
        Assert.False(unavailable.IsAvailable);
        var store = new HighScoreStore(unavailable);
        var c = store.Load(1);
        new HighScoreTable(c).Insert(0, "KEEP", 321);
        Assert.False(store.Save(1, c));            // not persisted ...
        var again = store.Load(1);                  // ... but still shown for this session
        Assert.Equal("KEEP", again.HiNames[0]);
        Assert.Equal(321u, again.HiScores[0]);
        Assert.Equal(0u, new HighScoreStore(unavailable).Load(1).HiScores[0]); // a new process starts empty
    }

    [Fact]
    public void SuccessfulSaveClearsSessionOnlyCopy()
    {
        var store = new HighScoreStore(new UserDataStore(_dir));
        Assert.False(store.Save(7, new ConfigFile())); // invalid episode: failure, no throw
        var c = store.Load(1);
        c.HiScores[0] = 5;
        Assert.True(store.Save(1, c));
        File.Delete(Path.Combine(_dir, "config.xr1"));
        Assert.Equal(0u, store.Load(1).HiScores[0]); // disk is the truth after a good save
    }

    // ---- display ----

    private static (VgaScreen screen, ShapeManager shapes, TextRenderer text) Setup()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        screen.Clear(200);
        return (screen, shapes, new TextRenderer(shapes));
    }

    private static byte Px(VgaScreen s, int x, int y) => s.Pixels[y * VgaScreen.Width + x];

    private static bool Has(VgaScreen s, int x0, int y0, int w, int h, byte colour)
    {
        for (int y = y0; y < y0 + h; y++) for (int x = x0; x < x0 + w; x++) if (Px(s, x, y) == colour) return true;
        return false;
    }

    [Fact]
    public void FrameIsTheDefwinTextbox()
    {
        var w = HighScoreWindow.Frame();
        Assert.Equal((88, 24, 144, 112), (w.Border.X, w.Border.Y, w.Border.W, w.Border.H));
        Assert.Equal((96, 32, 128, 96), (w.Inside.X, w.Inside.Y, w.Inside.W, w.Inside.H));
    }

    [Fact]
    public void TitleAndUnderlineLayout()
    {
        var (screen, shapes, text) = Setup();
        HighScoreWindow.Render(screen, shapes, text, Table(), -1);
        const int ix = 96, iy = 32;
        // title: 15 chars x 6 px at (18,4) colour 7; nothing of colour 7 to the left of x=18 or right of x=18+90.
        Assert.True(Has(screen, ix + 18, iy + 4, 90, 6, 7));
        Assert.False(Has(screen, ix + 0, iy + 4, 18, 4, 7));
        Assert.False(Has(screen, ix + 108, iy + 4, 20, 4, 7));
        // underline: 16 underscores at (15,8) colour 4, spanning exactly x 15..110.
        Assert.True(Has(screen, ix + 15, iy + 8, 96, 6, 4));
        Assert.False(Has(screen, ix + 0, iy + 8, 15, 6, 4));
        Assert.False(Has(screen, ix + 111, iy + 8, 17, 6, 4));
        Assert.False(Has(screen, ix + 15, iy + 0, 96, 4, 4));
    }

    [Fact]
    public void NameRowsUseColour3AtEightWithSevenPixelPitch()
    {
        var (screen, shapes, text) = Setup();
        var cfg = new ConfigFile();
        cfg.HiNames[0] = "WWWW";
        cfg.HiNames[5] = "WWWW";
        HighScoreWindow.Render(screen, shapes, text, new HighScoreTable(cfg), -1);
        const int ix = 96, iy = 32;
        Assert.True(Has(screen, ix + 8, iy + 20, 24, 6, 3));
        Assert.True(Has(screen, ix + 8, iy + 20 + 5 * 7, 24, 6, 3));
        Assert.False(Has(screen, ix + 8, iy + 20 + 1 * 7, 24, 6, 3)); // empty row 1 draws no name pixels
        Assert.False(Has(screen, ix + 0, iy + 20, 8, 6, 3));          // nothing left of x=8
        Assert.False(Has(screen, ix + 32, iy + 20, 40, 6, 3));        // and only 4 chars
    }

    [Theory]
    [InlineData(7u, 1)]
    [InlineData(123u, 3)]
    [InlineData(123456u, 6)]
    [InlineData(4294967295u, 10)]
    public void ScoresAreRightAlignedEndingAtX120(uint score, int digits)
    {
        Assert.Equal(120 - 6 * digits, HighScoreTable.ScoreX(score));
        var (screen, shapes, text) = Setup();
        var cfg = new ConfigFile();
        cfg.HiScores[2] = score; // row 2, y = 20+14 = 34
        HighScoreWindow.Render(screen, shapes, text, new HighScoreTable(cfg), -1);
        const int ix = 96, iy = 32;
        int rowY = iy + 34, left = ix + 120 - 6 * digits;
        Assert.True(Has(screen, left, rowY, 6 * digits, 6, 2));
        if (left > ix + 60) Assert.False(Has(screen, ix + 60, rowY, left - (ix + 60), 6, 2)); // nothing left of the first digit
        Assert.False(Has(screen, ix + 120, rowY, 8, 6, 2));               // nothing right of x=120
        Assert.True(Has(screen, ix + 114, rowY, 6, 6, 2));                // the last digit occupies 114..119
    }

    [Fact]
    public void EmptyRowsShowZeroScore()
    {
        var (screen, shapes, text) = Setup();
        HighScoreWindow.Render(screen, shapes, text, Table(), -1);
        for (int i = 0; i < 10; i++) Assert.True(Has(screen, 96 + 114, 32 + 20 + 7 * i, 6, 6, 2), "row " + i);
    }

    [Fact]
    public void HighlightRowIsEightSpacesOnColour1()
    {
        var (screen, shapes, text) = Setup();
        var cfg = new ConfigFile();
        cfg.HiScores[3] = 999; cfg.HiNames[3] = "";
        HighScoreWindow.Render(screen, shapes, text, new HighScoreTable(cfg), 3);
        const int ix = 96, iy = 32;
        for (int y = 0; y < 6; y++)
            for (int x = 0; x < 48; x++)
                Assert.Equal((byte)1, Px(screen, ix + 8 + x, iy + 20 + 3 * 7 + y));
        // outside the 8 cells and in the neighbouring rows colour 1 is not painted
        Assert.NotEqual((byte)1, Px(screen, ix + 8 + 48, iy + 20 + 3 * 7));
        Assert.False(Has(screen, ix + 8, iy + 20 + 2 * 7, 48, 6, 1));
        Assert.False(Has(screen, ix + 8, iy + 20 + 4 * 7, 48, 6, 1));
        // the score of the new row is still drawn in colour 2
        Assert.True(Has(screen, ix + 102, iy + 20 + 3 * 7, 18, 6, 2));
    }

    [Fact]
    public void NoHighlightMeansNoColour1InTheNameColumn()
    {
        var (screen, shapes, text) = Setup();
        HighScoreWindow.Render(screen, shapes, text, Table(), -1);
        Assert.False(Has(screen, 96 + 8, 32 + 20, 48, 70, 1));
    }

    // ---- rest() ----

    [Theory]
    [InlineData(PopupInput.Escape, true)]
    [InlineData(PopupInput.Enter, true)]
    [InlineData(PopupInput.Space, true)]
    [InlineData(PopupInput.Fire1, true)]
    [InlineData(PopupInput.Fire2, true)]
    [InlineData(PopupInput.Up, true)]
    [InlineData(PopupInput.Down, true)]
    [InlineData(PopupInput.Other, false)]
    [InlineData(PopupInput.None, false)]
    public void RestDismissDecision(PopupInput k, bool dismiss) => Assert.Equal(dismiss, PopupWindows.RestDismisses(k));

    [Theory]
    [InlineData(PopupInput.Escape, true)]
    [InlineData(PopupInput.Enter, true)]
    [InlineData(PopupInput.Space, true)]
    [InlineData(PopupInput.Fire1, true)]
    [InlineData(PopupInput.Fire2, true)]
    [InlineData(PopupInput.Up, true)]
    [InlineData(PopupInput.Down, true)]
    [InlineData(PopupInput.None, false)]
    public void HeldKeysBlockTheInitialWait(PopupInput k, bool blocks) => Assert.Equal(blocks, PopupWindows.RestBlocks(k));
}
