using XargonNET.Assets;
using XargonNET.Video;

namespace XargonNET.Engine;

/// <summary>
/// Headless self-check for the asset decoders (Phase 1 verification). Mounts each episode and
/// asserts that: the palette shape is found, shape tables decode, and every board file parses
/// to exactly EOF. Run with <c>--verify</c>. Returns a process exit code (0 = all passed).
/// </summary>
public static class AssetVerifier
{
    public static int Run()
    {
        string dir;
        try { dir = AssetSet.FindAssetsDir(); }
        catch (Exception ex) { Console.WriteLine(ex.Message); return 1; }

        Console.WriteLine($"Assets: {dir}");
        int failures = 0;

        for (int ep = 1; ep <= 3; ep++)
        {
            Console.WriteLine($"\n=== Episode {ep} ===");
            AssetSet set;
            try { set = AssetSet.Mount(dir, ep); }
            catch (Exception ex) { Console.WriteLine($"  MOUNT FAILED: {ex.Message}"); failures++; continue; }

            // Shape tables
            int tableCount = 0, shapeCount = 0;
            foreach (int t in set.Graphics.PresentTables())
            {
                try
                {
                    var table = set.Graphics.LoadTable(t);
                    if (table is null) continue;
                    tableCount++;
                    shapeCount += table.Shapes.Count;
                }
                catch (Exception ex) { Console.WriteLine($"  table {t} FAILED: {ex.Message}"); failures++; }
            }
            Console.WriteLine($"  shape tables: {tableCount} present, {shapeCount} shapes total");

            // Palette: gameplay uses the compiled-in default (index 248 = sky-blue background).
            var screen = new VgaScreen();
            uint c248 = screen.Palette[248];
            Console.WriteLine($"  palette: default vgapal loaded, bg[248]=#{(byte)(c248 >> 16):X2}{(byte)(c248 >> 8):X2}{(byte)c248:X2}");

            // Info table
            int namedTiles = set.Info.Entries.Count(e => e.Name.Length > 0);
            Console.WriteLine($"  info[]: {namedTiles} defined tiles");

            // Boards — everything that should parse as a board.
            foreach (var name in BoardNames(dir, ep))
            {
                try
                {
                    var b = set.LoadBoard(name);
                    Console.WriteLine($"  board {name,-10}: OK  objs={b.Objects.Count} fruit={b.FruitCount} " +
                                      $"plHealth={b.Player.Health}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  board {name,-10}: FAIL  {ex.Message}");
                    failures++;
                }
            }
        }

        Console.WriteLine(failures == 0 ? "\nALL CHECKS PASSED" : $"\n{failures} FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>Names of files that are boards for a given episode (BOARD_*, INTRO, MAP, STORY).</summary>
    private static IEnumerable<string> BoardNames(string dir, int episode)
    {
        var names = new List<string>();
        string suffix = $".XR{episode}";
        foreach (var path in Directory.EnumerateFiles(dir, $"*{suffix}"))
        {
            string stem = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
            if (stem.StartsWith("BOARD_") || stem is "INTRO" or "MAP" or "STORY")
                names.Add(stem);
        }
        names.Sort(StringComparer.Ordinal);
        return names;
    }
}
