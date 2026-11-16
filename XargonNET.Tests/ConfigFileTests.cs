using Xunit;
using XargonNET.Assets;
using XargonNET.Engine;

namespace XargonNET.Tests;

public sealed class ConfigFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xargon-cfgtest-" + Guid.NewGuid().ToString("N"));
    private UserDataStore Store() => new(_dir);
    private string FileFor(int ep) => Path.Combine(_dir, $"config.xr{ep}");

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private static void AssertEmpty(ConfigFile c)
    {
        Assert.Equal(10, c.HiNames.Length);
        Assert.Equal(10, c.HiScores.Length);
        Assert.Equal(7, c.SaveNames.Length);
        Assert.Equal(22, c.Tail.Length);
        Assert.All(c.HiNames, n => Assert.Equal("", n));
        Assert.All(c.HiScores, s => Assert.Equal(0u, s));
        Assert.All(c.SaveNames, n => Assert.Equal("", n));
        Assert.All(c.Tail, b => Assert.Equal(0, b));
    }

    [Fact]
    public void RoundTripPreservesEveryField()
    {
        var c = new ConfigFile();
        for (int i = 0; i < 10; i++) { c.HiNames[i] = "HERO" + i; c.HiScores[i] = (uint)(4_000_000_000u - i * 1000); }
        for (int i = 0; i < 7; i++) c.SaveNames[i] = "slot " + i;
        for (int i = 0; i < 22; i++) c.Tail[i] = (byte)(i + 1);
        Assert.True(c.Save(Store(), 2));

        var l = ConfigFile.Load(Store(), 2);
        Assert.Equal(c.HiNames, l.HiNames);
        Assert.Equal(c.HiScores, l.HiScores);
        Assert.Equal(c.SaveNames, l.SaveNames);
        Assert.Equal(c.Tail, l.Tail);
    }

    [Fact]
    public void MissingFileGivesDefaults() => AssertEmpty(ConfigFile.Load(Store(), 1));

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(231)]
    [InlineData(233)]
    [InlineData(10000)]
    public void WrongLengthFileGivesDefaults(int len)
    {
        Directory.CreateDirectory(_dir);
        var bytes = new byte[len];
        Array.Fill(bytes, (byte)'A');
        File.WriteAllBytes(FileFor(1), bytes);
        AssertEmpty(ConfigFile.Load(Store(), 1));
    }

    [Fact]
    public void UnreadableFileGivesDefaults()
    {
        Directory.CreateDirectory(FileFor(1)); // a directory where the file should be
        AssertEmpty(ConfigFile.Load(Store(), 1));
    }

    [Fact]
    public void LockedFileGivesDefaultsWithoutThrowing()
    {
        if (!OperatingSystem.IsWindows()) return; // exclusive locks are advisory elsewhere
        Assert.True(new ConfigFile { HiScores = { [0] = 5 } }.Save(Store(), 1));
        using var hold = new FileStream(FileFor(1), FileMode.Open, FileAccess.Read, FileShare.None);
        AssertEmpty(ConfigFile.Load(Store(), 1));
    }

    [Fact]
    public void RelativeRootIsRejected() =>
        Assert.Throws<ArgumentException>(() => new UserDataStore("XargonNET"));

    private static readonly string Abs = Path.Combine(Path.GetTempPath(), "xargon-fake-home");

    [Fact]
    public void FromFoldersUsesLocalAppData() =>
        Assert.Equal(Path.Combine(Abs, "XargonNET", "x"),
            UserDataStore.FromFolders(f => f == Environment.SpecialFolder.LocalApplicationData ? Abs : "").PathFor("x"));

    [Fact]
    public void FromFoldersFallsBackToUserProfile() =>
        Assert.Equal(Path.Combine(Abs, ".xargonnet", "x"),
            UserDataStore.FromFolders(f => f == Environment.SpecialFolder.UserProfile ? Abs : "").PathFor("x"));

    [Fact]
    public void FromFoldersRejectsRelativeFolderValues() =>
        Assert.False(UserDataStore.FromFolders(_ => "relative").IsAvailable);

    [Fact]
    public void FromFoldersBothEmptyGivesUnavailableStore()
    {
        var s = UserDataStore.FromFolders(_ => "");
        Assert.False(s.IsAvailable);
        Assert.Null(s.Root);
        Assert.False(s.EnsureDirectory());
        Assert.False(new ConfigFile().Save(s, 1));
        AssertEmpty(ConfigFile.Load(s, 1));
        Assert.False(File.Exists("XargonNET")); // nothing relative was created
        Assert.False(Directory.Exists("XargonNET"));
        Assert.False(File.Exists("config.xr1"));
    }

    [Fact]
    public void FromFoldersNeverThrows() =>
        Assert.False(UserDataStore.FromFolders(_ => throw new InvalidOperationException()).IsAvailable);

    [Fact]
    public void GarbageOfCorrectLengthLoadsWithPrintableNames()
    {
        Directory.CreateDirectory(_dir);
        var rng = new Random(1234);
        for (int round = 0; round < 50; round++)
        {
            var bytes = new byte[232];
            rng.NextBytes(bytes);
            File.WriteAllBytes(FileFor(3), bytes);
            var c = ConfigFile.Load(Store(), 3);
            foreach (var n in c.HiNames.Concat(c.SaveNames))
            {
                Assert.True(n.Length <= 10);
                Assert.All(n, ch => Assert.InRange(ch, ' ', '~'));
            }
        }
    }

    [Fact]
    public void GarbageActuallyDecodesFields()
    {
        // Guards against a Load that returns defaults for everything: a valid-length file must be parsed.
        Directory.CreateDirectory(_dir);
        var bytes = new byte[232];
        bytes[0] = (byte)'Q';
        bytes[100] = 7;
        File.WriteAllBytes(FileFor(1), bytes);
        var c = ConfigFile.Load(Store(), 1);
        Assert.Equal("Q", c.HiNames[0]);
        Assert.Equal(7u, c.HiScores[0]);
    }

    [Fact]
    public void SavedFileMatchesOriginalLayout()
    {
        var c = new ConfigFile();
        c.HiNames[0] = "AB"; c.HiNames[9] = "ZED";
        c.HiScores[0] = 0x01020304; c.HiScores[9] = 0xAABBCCDD;
        c.SaveNames[0] = "first"; c.SaveNames[6] = "last";
        c.Tail[0] = 0x11; c.Tail[21] = 0x99;
        Assert.True(c.Save(Store(), 1));

        var b = File.ReadAllBytes(FileFor(1));
        Assert.Equal(232, b.Length);
        Assert.Equal((byte)'A', b[0]); Assert.Equal((byte)'B', b[1]); Assert.Equal(0, b[2]);
        Assert.Equal((byte)'Z', b[90]); Assert.Equal((byte)'D', b[92]); Assert.Equal(0, b[93]);
        Assert.Equal(new byte[] { 4, 3, 2, 1 }, b[100..104]);         // u32 little endian
        Assert.Equal(new byte[] { 0xDD, 0xCC, 0xBB, 0xAA }, b[136..140]);
        Assert.Equal("first", System.Text.Encoding.ASCII.GetString(b, 140, 5)); Assert.Equal(0, b[145]);
        Assert.Equal("last", System.Text.Encoding.ASCII.GetString(b, 200, 4)); Assert.Equal(0, b[204]);
        Assert.Equal(0x11, b[210]);
        Assert.Equal(0x99, b[231]);
    }

    [Fact]
    public void LongNamesTruncateToNineCharsKeepingNul()
    {
        var c = new ConfigFile();
        c.HiNames[0] = "ABCDEFGHIJKLMNOP";
        c.SaveNames[6] = "0123456789ABC";
        Assert.True(c.Save(Store(), 1));
        var b = File.ReadAllBytes(FileFor(1));
        Assert.Equal(0, b[9]);
        Assert.Equal(0, b[10]); // next name untouched
        Assert.Equal(0, b[200 + 9]);
        var l = ConfigFile.Load(Store(), 1);
        Assert.Equal("ABCDEFGHI", l.HiNames[0]);
        Assert.Equal("012345678", l.SaveNames[6]);
        Assert.Equal("", l.HiNames[1]);
    }

    [Fact]
    public void NonAsciiNamesSaveAsPrintableAscii()
    {
        var c = new ConfigFile();
        c.HiNames[0] = "aé\tb";
        Assert.True(c.Save(Store(), 1));
        var l = ConfigFile.Load(Store(), 1);
        Assert.Equal("ab", l.HiNames[0]);
    }

    [Fact]
    public void EpisodesUseSeparateFiles()
    {
        var a = new ConfigFile(); a.HiScores[0] = 1; a.Save(Store(), 1);
        var b = new ConfigFile(); b.HiScores[0] = 2; b.Save(Store(), 2);
        Assert.True(File.Exists(FileFor(1)));
        Assert.True(File.Exists(FileFor(2)));
        Assert.False(File.Exists(FileFor(3)));
        Assert.Equal(1u, ConfigFile.Load(Store(), 1).HiScores[0]);
        Assert.Equal(2u, ConfigFile.Load(Store(), 2).HiScores[0]);
        AssertEmpty(ConfigFile.Load(Store(), 3));
    }

    [Fact]
    public void InvalidEpisodeLoadsDefaultsAndSaveFails()
    {
        AssertEmpty(ConfigFile.Load(Store(), 0));
        Assert.False(new ConfigFile().Save(Store(), 4));
    }

    [Fact]
    public void FailedWriteReturnsFalse()
    {
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "x");
        var bad = new UserDataStore(Path.Combine(blocker, "sub")); // parent is a file
        Assert.False(new ConfigFile().Save(bad, 1));
        AssertEmpty(ConfigFile.Load(bad, 1));
    }

    [Fact]
    public void FailedReplaceReturnsFalseAndLeavesNoTemp()
    {
        Directory.CreateDirectory(FileFor(1)); // destination is a directory: replace must fail
        Assert.False(new ConfigFile().Save(Store(), 1));
        Assert.Equal(new[] { FileFor(1) }, Directory.GetFileSystemEntries(_dir));
    }

    [Fact]
    public void SuccessfulSaveLeavesNoTempFile()
    {
        var c = new ConfigFile();
        Assert.True(c.Save(Store(), 1));
        Assert.True(c.Save(Store(), 1)); // overwrite path
        Assert.Equal(new[] { FileFor(1) }, Directory.GetFileSystemEntries(_dir));
    }

    [Fact]
    public void DirectoryCreatedLazily()
    {
        var s = Store();
        Assert.False(Directory.Exists(_dir));
        ConfigFile.Load(s, 1);
        Assert.False(Directory.Exists(_dir));
        Assert.True(new ConfigFile().Save(s, 1));
        Assert.True(Directory.Exists(_dir));
    }

    [Fact]
    public void DefaultStoreIsUnderLocalAppData()
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XargonNET");
        Assert.Equal(Path.Combine(expected, "x"), UserDataStore.Default().PathFor("x"));
    }

    [Fact]
    public void ShippedConfigXr3ParsesAsEmptyTable()
    {
        string? assets = null;
        try { assets = AssetSet.FindAssetsDir(); } catch (DirectoryNotFoundException) { }
        var path = assets is null ? null : Path.Combine(assets, "CONFIG.XR3");
        if (path is null || !File.Exists(path)) return; // shipped data absent: nothing to verify

        var raw = File.ReadAllBytes(path); // read-only; parsed in memory, never copied
        Assert.Equal(232, raw.Length);
        var c = ConfigFile.Parse(raw);
        Assert.All(c.HiNames, n => Assert.Equal("", n));
        Assert.All(c.HiScores, s => Assert.Equal(0u, s));
        Assert.All(c.SaveNames, n => Assert.Equal("", n));
        Assert.Equal(1, c.Tail[18]);
        Assert.Equal(1, c.Tail[20]);
    }
}
