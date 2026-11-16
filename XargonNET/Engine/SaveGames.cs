using XargonNET.Assets;
using XargonNET.Objects;

namespace XargonNET.Engine;

/// <summary>
/// Save-slot files: <c>save_{slot}.xr{episode}</c> in the per-user store, in the original board-file layout
/// (<see cref="BoardWriter"/>/<see cref="BoardLoader"/>). Reading never trusts the file: the size is bounded before
/// anything is read, parsing failures of every kind are swallowed, and a parsed board must pass
/// <see cref="IsSane"/> before it can replace the running game. Nothing here throws.
/// </summary>
public static class SaveGames
{
    public const int Slots = ConfigFile.NumSaves;

    /// <summary>A save is about 17 KB; anything past a generous 1 MiB is not one and is never read.</summary>
    public const long MaxFileBytes = 1 << 20;

    /// <summary>cells (16384) + numobjs (2) + one object (31) + pl (96) + cnt_fruit (1): the smallest a save can be.</summary>
    public const int MinFileBytes = Board.W * Board.H * 2 + 2 + 31 + 96 + 1;

    public static string FileNameFor(int slot, int episode) => $"save_{slot}.xr{episode}";

    private static bool ValidSlot(int slot) => (uint)slot < Slots;
    private static bool ValidEpisode(int episode) => episode is >= 1 and <= 3;

    /// <summary>saveboard(): the live board and the live player state (incl. fruit), written atomically. Returns
    /// false (the previous file, if any, is untouched) when the store is unavailable, the slot/episode is invalid,
    /// the board cannot be serialised, or the write fails.</summary>
    public static bool TryWrite(UserDataStore store, int episode, int slot, Board board, PlayerState pl)
    {
        if (!store.IsAvailable || !ValidSlot(slot) || !ValidEpisode(episode)) return false;
        try
        {
            byte[] bytes = BoardWriter.Write(board, pl);
            return store.EnsureDirectory() && AtomicFile.TryWrite(store.PathFor(FileNameFor(slot, episode)), bytes);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>loadboard() of a save: the parsed, validated board, or null for a missing, unreadable, wrongly sized,
    /// truncated, corrupt or implausible file.</summary>
    public static Board? TryRead(UserDataStore store, int episode, int slot)
    {
        if (!store.IsAvailable || !ValidSlot(slot) || !ValidEpisode(episode)) return null;
        try
        {
            using var fs = new FileStream(store.PathFor(FileNameFor(slot, episode)), FileMode.Open, FileAccess.Read, FileShare.Read);
            long len = fs.Length;
            if (len < MinFileBytes || len > MaxFileBytes) return null; // checked BEFORE reading anything
            var data = new byte[len];
            fs.ReadExactly(data);
            return TryParse(data);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException
                                      or ArgumentException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>Parse and validate a save image; null when it is not a loadable save. The loader reads counts and
    /// lengths straight from the file, so every way it can fail on hostile bytes is caught here.</summary>
    public static Board? TryParse(byte[] data)
    {
        if (data.Length < MinFileBytes || data.Length > MaxFileBytes) return null;
        try
        {
            var b = BoardLoader.Parse(data, "save");
            return IsSane(b) ? b : null;
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException
                                      or OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether a parsed board can safely become the running game. Saves only ever come from the map, so: 1..256
    /// objects, object 0 is the map hero (<c>obj_tiny</c>) at a position on the board, every kind is a known kind,
    /// the map checkpoint reads state 5 (see <see cref="GameFlow.IsMapBoard"/>) and the inventory count fits the
    /// 30-slot array. This also rejects a level board or another episode's garbage renamed to a save.
    /// </summary>
    public static bool IsSane(Board b)
    {
        int n = b.Objects.Count;
        if (n < 1 || n > BoardWriter.MaxObjects) return false;
        var hero = b.Objects[0];
        if (hero.Kind != ObjectKind.Tiny) return false;
        if (hero.X < 0 || hero.X >= Board.W * 16 || hero.Y < 0 || hero.Y >= Board.H * 16) return false;
        foreach (var o in b.Objects)
            if (o.Kind >= KindDefs.Count) return false;
        if (b.Player.NumInv < 0 || b.Player.NumInv > b.Player.Inv.Length) return false;
        return GameFlow.IsMapBoard(b.Objects);
    }
}
