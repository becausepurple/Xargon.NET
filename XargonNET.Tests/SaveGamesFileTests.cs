using Xunit;
using XargonNET.Assets;
using XargonNET.Engine;

namespace XargonNET.Tests;

/// <summary>Task 10: the atomic write helper and the save-file layer (SaveGames) in isolation.</summary>
public sealed class SaveGamesFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xargon-atomictest-" + Guid.NewGuid().ToString("N"));

    public SaveGamesFileTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private string P(string n) => Path.Combine(_dir, n);

    [Fact]
    public void WritesNewFileAndReplacesAnExistingOne()
    {
        Assert.True(AtomicFile.TryWrite(P("a.bin"), new byte[] { 1, 2, 3 }));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(P("a.bin")));
        Assert.True(AtomicFile.TryWrite(P("a.bin"), new byte[] { 9 }));
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(P("a.bin")));
        Assert.Single(Directory.GetFiles(_dir)); // no temp left behind
    }

    [Fact]
    public void FailedMoveKeepsTheOldFileAndRemovesTheTemp()
    {
        File.WriteAllBytes(P("a.bin"), new byte[] { 1, 2, 3 });
        bool ok = AtomicFile.TryWrite(P("a.bin"), new byte[] { 7, 7, 7, 7 },
            move: (_, _) => throw new IOException("simulated sharing violation"));
        Assert.False(ok);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(P("a.bin")));
        Assert.Equal(new[] { "a.bin" }, Directory.GetFiles(_dir).Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void FailedMoveReportsFalseForUnauthorizedToo()
    {
        Assert.False(AtomicFile.TryWrite(P("b.bin"), new byte[] { 1 }, (_, _) => throw new UnauthorizedAccessException()));
        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public void TargetThatIsADirectoryFailsWithoutDamage()
    {
        Directory.CreateDirectory(P("d.bin"));
        File.WriteAllBytes(Path.Combine(P("d.bin"), "inner"), new byte[] { 5 });
        Assert.False(AtomicFile.TryWrite(P("d.bin"), new byte[] { 1 }));
        Assert.True(File.Exists(Path.Combine(P("d.bin"), "inner")));
        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public void MissingParentDirectoryFailsWithoutThrowing() =>
        Assert.False(AtomicFile.TryWrite(Path.Combine(_dir, "nope", "x.bin"), new byte[] { 1 }));

    [Fact]
    public void LockedExistingFileIsNotDamagedOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return; // POSIX rename ignores open handles: covered by the seam test above
        File.WriteAllBytes(P("l.bin"), new byte[] { 1, 2, 3 });
        using (new FileStream(P("l.bin"), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.False(AtomicFile.TryWrite(P("l.bin"), new byte[] { 9, 9 }));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(P("l.bin")));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void SaveGamesRejectsUnavailableStoreAndBadSlots()
    {
        var b = new Board();
        b.Objects.Add(new Obj { Kind = 5 });
        Assert.False(SaveGames.TryWrite(UserDataStore.FromFolders(_ => ""), 1, 0, b, new PlayerState()));
        var store = new UserDataStore(_dir);
        Assert.False(SaveGames.TryWrite(store, 1, 7, b, new PlayerState()));
        Assert.False(SaveGames.TryWrite(store, 1, -1, b, new PlayerState()));
        Assert.False(SaveGames.TryWrite(store, 4, 0, b, new PlayerState()));
        Assert.Null(SaveGames.TryRead(UserDataStore.FromFolders(_ => ""), 1, 0));
        Assert.Null(SaveGames.TryRead(store, 1, 9));
        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public void BoardWithTooManyObjectsFailsToWriteButDoesNotThrow()
    {
        var b = new Board();
        for (int i = 0; i < 300; i++) b.Objects.Add(new Obj { Kind = 5 });
        Assert.False(SaveGames.TryWrite(new UserDataStore(_dir), 1, 0, b, new PlayerState()));
        Assert.Empty(Directory.GetFiles(_dir));
    }
}
