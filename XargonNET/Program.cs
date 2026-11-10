using XargonNET.Engine;

Console.WriteLine("Xargon.NET — faithful port of the 1993 DOS game (all episodes, one executable).");

if (args.Contains("--verify"))
    return AssetVerifier.Run();

if (args.Length > 0 && args[0] == "--sample")
{
    var (w, h, rgb) = XargonNET.Video.PngReader.Load(args[1]);
    Console.WriteLine($"{args[1]}: {w}x{h}");
    for (int i = 2; i + 1 < args.Length; i += 2)
    {
        int sx = int.Parse(args[i]), sy = int.Parse(args[i + 1]);
        int o = (sy * w + sx) * 3;
        Console.WriteLine($"  ({sx},{sy}) = #{rgb[o]:X2}{rgb[o + 1]:X2}{rgb[o + 2]:X2}  rgb({rgb[o]},{rgb[o + 1]},{rgb[o + 2]})");
    }
    return 0;
}

if (args.Length > 0 && args[0] == "--dump-board")
{
    string name = args.Length > 1 ? args[1] : "MAP";
    int ep = args.Length > 2 ? int.Parse(args[2]) : 1;
    int tx = args.Length > 3 ? int.Parse(args[3]) : 0;
    int ty = args.Length > 4 ? int.Parse(args[4]) : 0;
    int palTable = args.Length > 5 ? int.Parse(args[5]) : -2; // default: gameplay palette
    return DebugDump.DumpBoard(name, ep, tx, ty, palTable, args.Contains("objs"));
}

if (args.Length > 0 && args[0] == "--dump-fullboard")
{
    string name = args.Length > 1 ? args[1] : "MAP";
    int ep = args.Length > 2 ? int.Parse(args[2]) : 1;
    bool missing = args.Contains("missing");
    return DebugDump.DumpFullBoard(name, ep, missing);
}

if (args.Length > 0 && args[0] == "--player")
    return DebugDump.DumpPlayer(args.Length > 1 ? int.Parse(args[1]) : 1,
        args.Length > 2 ? int.Parse(args[2]) : 60, args.Length > 3 ? int.Parse(args[3]) : 0);

if (args.Length > 0 && args[0] == "--mainmenu")
    return DebugDump.DumpMainMenu(args.Length > 1 ? int.Parse(args[1]) : 1, args.Length > 2 ? int.Parse(args[2]) : 0);

if (args.Length > 0 && args[0] == "--window")
    return DebugDump.DumpWindows(args.Length > 1 ? int.Parse(args[1]) : 1);

if (args.Length > 0 && args[0] == "--page")
    return DebugDump.DumpPage(int.Parse(args[1]), args[2], int.Parse(args[3]), args.Length > 4 ? args[4] : null);

if (args.Length > 0 && args[0] == "--popup")
    return DebugDump.DumpPopup(int.Parse(args[1]), int.Parse(args[2]),
        args.Length > 3 ? int.Parse(args[3]) : 0,
        args.Length > 4 ? args[4] : "plain",
        args.Length > 5 ? args[5] : null);

if (args.Length > 0 && args[0] == "--banner")
    return DebugDump.DumpBanner(int.Parse(args[1]), int.Parse(args[2]), args.Length > 3 ? args[3] : null);

if (args.Length > 0 && args[0] == "--menu")
    return DebugDump.DumpMenu(args.Length > 1 ? int.Parse(args[1]) : 1, args.Length > 2 ? int.Parse(args[2]) : 1);

if (args.Length > 0 && args[0] == "--text")
    return DebugDump.DumpText(args.Length > 1 ? int.Parse(args[1]) : 1);

if (args.Length > 0 && args[0] == "--map")
    return DebugDump.DumpMap(args.Length > 1 ? int.Parse(args[1]) : 1);

if (args.Length > 0 && args[0] == "--census")
    return DebugDump.Census(args.Length > 1 ? int.Parse(args[1]) : 1);

if (args.Length > 0 && args[0] == "--transport")
    return DebugDump.DumpTransport(args.Length > 1 ? args[1] : "BOARD_01", args.Length > 2 ? int.Parse(args[2]) : 1);

if (args.Length > 0 && args[0] == "--objlist")
    return DebugDump.ObjList(args[1], int.Parse(args[2]));

if (args.Length > 0 && args[0] == "--findobj")
    return DebugDump.FindObj(args[1], int.Parse(args[2]), int.Parse(args[3]));

if (args.Length > 0 && args[0] == "--cells")
    return DebugDump.DumpCells(args[1], int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]));

if (args.Length > 0 && args[0] == "--derive-palette")
    return DebugDump.DerivePalette(args[1], int.Parse(args[2]), args[3]);

if (args.Length > 0 && args[0] == "--sky")
    return DebugDump.DumpSky(args.Length > 1 ? int.Parse(args[1]) : 1);

if (args.Length > 0 && args[0] == "--dump-palettes")
{
    int ep = args.Length > 1 ? int.Parse(args[1]) : 1;
    return DebugDump.DumpPalettes(ep);
}

if (args.Length > 0 && args[0] == "--dump-table")
{
    int ep = args.Length > 1 ? int.Parse(args[1]) : 1;
    int table = args.Length > 2 ? int.Parse(args[2]) : 30;
    return DebugDump.DumpTable(ep, table);
}

using var app = new GameApp();
if (!app.Init())
{
    Console.WriteLine("Failed to initialize. Exiting.");
    return 1;
}

app.Run();
return 0;
