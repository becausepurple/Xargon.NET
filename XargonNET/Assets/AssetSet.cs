using XargonNET.Text;
using XargonNET.Video;

namespace XargonNET.Assets;

/// <summary>
/// One episode's mounted data: the graphics container and the info/tile table, plus helpers
/// to resolve <c>NAME.XR{episode}</c> paths and load boards. Unifying all three episodes
/// behind this is what lets a single executable serve them via the selector screen (the DOS
/// original shipped one .exe per episode).
/// </summary>
public sealed class AssetSet
{
    public int Episode { get; }
    public string Dir { get; }
    public ShapeFile Graphics { get; }
    public InfoTable Info { get; }
    public TextBank Text { get; }

    private AssetSet(int episode, string dir, ShapeFile graphics, InfoTable info, TextBank text)
    {
        Episode = episode;
        Dir = dir;
        Graphics = graphics;
        Info = info;
        Text = text;
    }

    public string PathFor(string baseName) => Path.Combine(Dir, $"{baseName}.XR{Episode}");

    public static AssetSet Mount(string dir, int episode)
    {
        var gfx = ShapeFile.Load(Path.Combine(dir, $"GRAPHICS.XR{episode}"));
        var info = InfoTable.Load(Path.Combine(dir, $"TILES.XR{episode}"));
        var text = TextBank.Load(Path.Combine(dir, $"AUDIO.XR{episode}"));
        return new AssetSet(episode, dir, gfx, info, text);
    }

    public Board LoadBoard(string name) => BoardLoader.Load(PathFor(name));

    /// <summary>
    /// Apply the game's gameplay palette. A 64x12 shape in an 8-bit table carries the VGA palette
    /// (SHAPEMAN.C:111). The primary shape table (loaded first) holds the correct gameplay palette
    /// — blue sky, tan stone — so we apply the first one found. (A later table carries a different
    /// palette used by other screens; applying that one turns the sky white.) After applying, we
    /// force colours 250/251 to black exactly as init_colors() does — this is what makes the "air"
    /// tiles and play-area background a deliberate black void. Returns the table used, or -1.
    /// </summary>
    public int LoadPalette(VgaScreen screen)
    {
        int applied = -1;
        foreach (int t in Graphics.PresentTables())
        {
            ShapeTable? table;
            try { table = Graphics.LoadTable(t); }
            catch { continue; }
            if (table is null || table.NumColorBits != 8) continue;
            var pal = table.Shapes.FirstOrDefault(s => s.Width == 64 && s.Height == 12 && s.Pixels.Length >= 768);
            if (pal is null) continue;
            screen.LoadPalette6(pal.Pixels);
            applied = t;
            break; // first (primary) palette table is the gameplay palette
        }

        // NOTE: init_colors() blacks out colours 250/251, but that runs on the menu/map
        // screens, not during level play. In a level those indices keep their palette values
        // (the "air" tile is a solid fill of index 250 = sky blue), so we do NOT black them out.
        return applied;
    }

    /// <summary>
    /// Locate the Assets directory, checking the build output first (Content is copied there),
    /// then the working directory, then walking up to the repo-level Assets folder.
    /// </summary>
    public static string FindAssetsDir()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "Assets"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets"),
        };

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
            candidates.Add(Path.Combine(dir.FullName, "Assets"));

        foreach (var c in candidates)
            if (File.Exists(Path.Combine(c, "GRAPHICS.XR1")))
                return c;

        throw new DirectoryNotFoundException(
            "Could not locate the Assets directory (looked for GRAPHICS.XR1).");
    }
}
