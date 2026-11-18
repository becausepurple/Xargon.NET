using XargonNET.Assets;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Engine;

/// <summary>
/// <c>--makesave EP SLOT NAME LEVEL</c>: write a map save with the map hero standing next to the entry of level
/// LEVEL (the map checkpoint whose counter is LEVEL), for testing that mission without playing up to it. The save is
/// an ordinary map save (what the in-game SAVE writes), so the normal LOAD GAME accepts it. A spot is only used if
/// walking toward the entry from it really enters the level (checked on a copy of the map).
/// </summary>
public static class DebugSave
{
    private const int Distance = 32;

    public static int MakeSave(int episode, int slot, string name, int level)
    {
        var store = UserDataStore.Default();
        if (!store.IsAvailable) { Console.WriteLine("No per-user data folder is available."); return 1; }
        if ((uint)slot >= SaveGames.Slots) { Console.WriteLine($"Slot must be 0..{SaveGames.Slots - 1}."); return 1; }
        string clean = SaveLoadFlow.Sanitise(name);
        if (clean.Length == 0) { Console.WriteLine("The slot name must have at least one printable character."); return 1; }

        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var shapes = new ShapeManager(set.Graphics);
        var board = set.LoadBoard("MAP");

        int cpIndex = board.Objects.FindIndex(o => o.Kind == ObjectKind.Checkpt && o.Counter == level);
        if (cpIndex < 0) { Console.WriteLine($"The map has no entry for level {level}."); return 1; }
        var cp = board.Objects[cpIndex];
        Console.WriteLine($"level {level} entry at ({cp.X},{cp.Y}) -> {cp.Inside}");

        (int dx, int dy)[] dirs = { (-1, 0), (1, 0), (0, -1), (0, 1) };
        int hx = 0, hy = 0;
        bool found = false;
        foreach (int dist in new[] { Distance, 16, 48 })
        {
            foreach (var (dx, dy) in dirs)
            {
                int x = cp.X + dx * dist, y = cp.Y + dy * dist;
                if (x < 0 || y < 0 || x >= Board.W * 16 || y >= Board.H * 16) continue;
                if (!WalkingEnters(set, shapes, episode, board, x, y, -dx, -dy, cp.Inside)) continue;
                hx = x; hy = y; found = true;
                break;
            }
            if (found) break;
        }
        if (!found) { Console.WriteLine("No spot next to that entry reaches it by walking straight toward it."); return 1; }

        var hero = board.Objects[0];
        hero.X = hx; hero.Y = hy;
        var pl = PlayerState.NewGame();
        pl.Level = LevelSession.MapLevel;
        pl.Inv[1] = Inv.Fire;   // laser plus fireballs
        pl.NumInv = 2;
        Console.WriteLine($"hero placed at ({hx},{hy})");

        if (!SaveGames.TryWrite(store, episode, slot, board, pl)) { Console.WriteLine("Could not write the save file."); return 1; }
        if (SaveGames.TryRead(store, episode, slot) is null) { Console.WriteLine("The written save was rejected by the loader."); return 1; }
        var cfgStore = new HighScoreStore(store);
        var cfg = cfgStore.Load(episode);
        cfg.SaveNames[slot] = clean;
        if (!cfgStore.Save(episode, cfg)) { Console.WriteLine("Saved the game but could not store the slot name."); return 1; }
        Console.WriteLine($"Wrote {store.PathFor(SaveGames.FileNameFor(slot, episode))} as slot {slot + 1} \"{clean}\".");
        return 0;
    }

    /// <summary>Hold the given direction on a COPY of the map, loaded the way a real LOAD GAME builds its world, until the
    /// hero enters the level (or 60 frames pass).</summary>
    private static bool WalkingEnters(AssetSet set, ShapeManager shapes, int episode, Board board, int x, int y,
        int dirX, int dirY, string? inside)
    {
        var copy = board.Clone();
        copy.Objects[0].X = x; copy.Objects[0].Y = y;
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var vp = new Viewport(0, 2, VgaScreen.Width, 158);
        var pl = PlayerState.NewGame();
        pl.Level = LevelSession.MapLevel;
        var session = new LevelSession(n => set.LoadBoard(n.ToUpperInvariant()), shapes, set.Info, screen, vp, pl,
            new Ui.TextRenderer(shapes), episode);
        var world = session.LoadSaved(copy, pl);
        var om = world.Objects;
        var mapHero = new MapHero(om, vp);
        var ctl = new Controls { Dx1 = dirX, Dy1 = dirY };
        for (int t = 0; t < 60; t++)
        {
            om.Gamecount++;
            mapHero.Update(ctl);
            om.UpdateObjects(vp);
            if (om.NewLevel is not null) return om.NewLevel == inside;
        }
        return false;
    }
}
