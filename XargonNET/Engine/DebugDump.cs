using XargonNET.Assets;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Engine;

/// <summary>
/// Headless rendering to PNG for visual verification (no SDL window). Lets each phase's output
/// be inspected as an image file.
/// </summary>
public static class DebugDump
{
    /// <summary>--dump-board NAME [episode] [tileX] [tileY] [palTable]: render a board region.
    /// palTable: -1 = default vgapal, -2 = scan/apply embedded, &gt;=0 = apply that table's palette.</summary>
    public static int DumpBoard(string name, int episode, int scrollTileX, int scrollTileY, int palTable = -1, bool withObjects = false)
    {
        string dir = AssetSet.FindAssetsDir();
        var set = AssetSet.Mount(dir, episode);
        var screen = new VgaScreen(); // compiled-in default palette
        if (palTable == -2) set.LoadPalette(screen);
        else if (palTable >= 0)
        {
            var pt = set.Graphics.LoadTable(palTable);
            var ps = pt?.Shapes.FirstOrDefault(s => s.Width == 64 && s.Height == 12);
            if (ps != null) screen.LoadPalette6(ps.Pixels);
        }
        uint c = screen.Palette[248];
        Console.WriteLine($"palTable={palTable} bg[248]=#{(byte)(c >> 16):X2}{(byte)(c >> 8):X2}{(byte)c:X2}");

        var shapes = new ShapeManager(set.Graphics);
        var board = set.LoadBoard(name);
        if (palTable == -2) Objects.Effects.ApplyForBoard(screen, board); // per-level sky colour

        var vp = screen.Main; // full-screen viewport
        vp.OriginX = scrollTileX * 16;
        vp.OriginY = scrollTileY * 16;

        screen.Clear(248); // play-area background (clrvp mainvp,248)
        BoardRenderer.DrawBoard(screen, vp, board, set.Info, shapes);
        if (withObjects) // optional object layers (back, normal, front) incl. text objects
        {
            var objs = new Objects.ObjectManager(board, shapes, set.Info, screen, episode: episode) { Text = new Ui.TextRenderer(shapes) };
            _ = new Objects.Behaviors(objs);
            objs.DrawBack(screen, vp);
            objs.DrawFore(screen, vp);
        }

        string outPath = Path.Combine(Directory.GetCurrentDirectory(), "board.png");
        PngWriter.SaveScreen(outPath, screen);
        Console.WriteLine($"wrote {outPath}  ({name}.XR{episode} @ tile {scrollTileX},{scrollTileY}, objs={board.Objects.Count})");
        return 0;
    }

    /// <summary>--dump-fullboard NAME [episode] [missing]: render the whole board to fullboard.png.
    /// If <paramref name="highlightMissing"/>, cells whose shape does not resolve are painted magenta.</summary>
    public static int DumpFullBoard(string name, int episode, bool highlightMissing = false)
    {
        string dir = AssetSet.FindAssetsDir();
        var set = AssetSet.Mount(dir, episode);
        var palScreen = new VgaScreen();
        set.LoadPalette(palScreen); // gameplay palette
        var shapes = new ShapeManager(set.Graphics);
        var board = set.LoadBoard(name);
        Objects.Effects.ApplyForBoard(palScreen, board); // per-level sky colour

        int w = Board.W * 16, h = Board.H * 16;
        var idx = new byte[w * h];
        Array.Fill(idx, (byte)250); // play-area "air" fill (effects object sets 250 to the sky colour)
        var missing = new bool[w * h];
        uint bg = palScreen.Palette[248];
        Console.WriteLine($"palette[248] = #{(byte)(bg >> 16):X2}{(byte)(bg >> 8):X2}{(byte)bg:X2}");

        var hist = new Dictionary<int, int>();
        int missingCells = 0;
        var missingByCell = new Dictionary<int, int>();
        for (int tx = 0; tx < Board.W; tx++)
            for (int ty = 0; ty < Board.H; ty++)
            {
                int cell = board.CellIndex(tx, ty);
                hist[cell] = hist.GetValueOrDefault(cell) + 1;
                var e = set.Info.Entries[cell % InfoTable.Count];
                var sh = shapes.GetShape((e.Sh >> 8) & 0x3f, e.Sh & 0xff);
                if (sh is null)
                {
                    missingCells++;
                    missingByCell[cell] = missingByCell.GetValueOrDefault(cell) + 1;
                    if (highlightMissing)
                        for (int py = 0; py < 16; py++)
                            for (int px = 0; px < 16; px++)
                                missing[(ty * 16 + py) * w + (tx * 16 + px)] = true;
                    continue;
                }
                BlitBig(idx, w, h, shapes, e.Sh, tx * 16, ty * 16);
            }

        // Draw objects (back, then normal, then front) at their resting shapes.
        void DrawObjs(Func<Objects.KindDef, bool> filter)
        {
            foreach (var o in board.Objects)
            {
                var kd = Objects.KindDefs.Get(o.Kind);
                if (kd.HasSprite && filter(kd))
                    BlitBig(idx, w, h, shapes, kd.ShapeTable * 256 + kd.DrawOffset, o.X, o.Y);
            }
        }
        DrawObjs(kd => kd.IsBack);
        DrawObjs(kd => !kd.IsBack && !kd.IsFront);
        DrawObjs(kd => kd.IsFront);

        var rgb = new byte[w * h * 3];
        var pal = palScreen.Palette;
        for (int i = 0; i < idx.Length; i++)
        {
            if (missing[i]) { rgb[i * 3] = 255; rgb[i * 3 + 1] = 0; rgb[i * 3 + 2] = 255; continue; }
            uint argb = pal[idx[i]];
            rgb[i * 3] = (byte)(argb >> 16);
            rgb[i * 3 + 1] = (byte)(argb >> 8);
            rgb[i * 3 + 2] = (byte)argb;
        }
        string outPath = Path.Combine(Directory.GetCurrentDirectory(), "fullboard.png");
        PngWriter.SaveRgb(outPath, w, h, rgb);

        Console.WriteLine($"wrote {outPath}  ({name}.XR{episode}, objs={board.Objects.Count}, missingCells={missingCells})");
        Console.WriteLine("top cell values (cell -> count, info.sh table/shape, name):");
        foreach (var kv in hist.OrderByDescending(k => k.Value).Take(12))
        {
            var e = set.Info.Entries[kv.Key % InfoTable.Count];
            Console.WriteLine($"  cell {kv.Key,4} x{kv.Value,-5} -> sh=0x{e.Sh:X4} (tbl {(e.Sh >> 8) & 0x3f}, shp {e.Sh & 0xff}) '{e.Name}'");
        }
        if (missingByCell.Count > 0)
        {
            Console.WriteLine("MISSING-shape cells (cell -> count, info.sh table/shape, name):");
            foreach (var kv in missingByCell.OrderByDescending(k => k.Value).Take(12))
            {
                var e = set.Info.Entries[kv.Key % InfoTable.Count];
                Console.WriteLine($"  cell {kv.Key,4} x{kv.Value,-5} -> sh=0x{e.Sh:X4} (tbl {(e.Sh >> 8) & 0x3f}, shp {e.Sh & 0xff}) '{e.Name}'");
            }
        }
        return 0;
    }

    /// <summary>--dump-palettes [episode]: list every 64x12 8-bit palette candidate and key colours.</summary>
    public static int DumpPalettes(int episode)
    {
        string dir = AssetSet.FindAssetsDir();
        var set = AssetSet.Mount(dir, episode);
        Console.WriteLine($"Episode {episode}: scanning for 64x12 palette shapes");
        foreach (int t in set.Graphics.PresentTables())
        {
            ShapeTable? table;
            try { table = set.Graphics.LoadTable(t); } catch { continue; }
            if (table is null || table.NumColorBits != 8) continue;
            for (int i = 0; i < table.Shapes.Count; i++)
            {
                var sh = table.Shapes[i];
                if (sh.Width == 64 && sh.Height == 12 && sh.Pixels.Length >= 768)
                {
                    string C(int idx) => $"{idx}=({sh.Pixels[idx * 3]},{sh.Pixels[idx * 3 + 1]},{sh.Pixels[idx * 3 + 2]})";
                    Console.WriteLine($"  table {t} shape {i}: sky {C(176)} {C(179)} {C(182)} | air {C(248)} {C(249)} {C(250)} {C(251)}");
                }
            }
        }
        return 0;
    }

    /// <summary>--sky [episode]: report the dominant colour index of the sky tiles (table 8,
    /// shapes 0..6, i.e. SK1..) and its RGB under the embedded (table 5) palette, plus what index
    /// the "air" tile (info[0].sh) contains. Helps determine the correct background colour.</summary>
    public static int DumpSky(int episode)
    {
        string dir = AssetSet.FindAssetsDir();
        var set = AssetSet.Mount(dir, episode);
        var screen = new VgaScreen();
        set.LoadPalette(screen); // embedded palette (scan)

        void Report(string label, Shape? sh)
        {
            if (sh is null) { Console.WriteLine($"  {label}: <none>"); return; }
            var counts = new Dictionary<byte, int>();
            foreach (var b in sh.Pixels) counts[b] = counts.GetValueOrDefault(b) + 1;
            var top = counts.OrderByDescending(k => k.Value).First();
            uint c = screen.Palette[top.Key];
            Console.WriteLine($"  {label}: {sh.Width}x{sh.Height} dominant idx={top.Key} " +
                $"({top.Value}px) rgb=#{(byte)(c >> 16):X2}{(byte)(c >> 8):X2}{(byte)c:X2}");
        }

        var shapes = new ShapeManager(set.Graphics);
        for (int i = 0; i < 7; i++) Report($"sky tbl8 shp{i}", shapes.GetShape(8, i));
        var air = set.Info.Entries[0];
        Report($"air info[0] sh=0x{air.Sh:X4}", shapes.GetShape((air.Sh >> 8) & 0x3f, air.Sh & 0xff));
        return 0;
    }

    /// <summary>--text EP: render sample text in both fonts to text.png (verifies glyph rendering).</summary>
    public static int DumpText(int episode)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        var text = new Ui.TextRenderer(shapes);

        screen.Clear(0);
        text.Draw(screen, screen.Main, 8, 8, Ui.TextRenderer.Font8, 15, -1, "XARGON.NET");
        text.Draw(screen, screen.Main, 8, 24, Ui.TextRenderer.Font8, 7, -1, "ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        text.Draw(screen, screen.Main, 8, 36, Ui.TextRenderer.Font8, 6, -1, "0123456789 !?.,-");
        text.Draw(screen, screen.Main, 8, 56, Ui.TextRenderer.Font6, 15, -1, "SMALL 6x6 FONT: PLAY NEW GAME");
        text.Draw(screen, screen.Main, 8, 72, Ui.TextRenderer.Font6, 3, -1, "SELECT AN EPISODE (1-3)");

        string outPath = Path.Combine(Directory.GetCurrentDirectory(), "text.png");
        PngWriter.SaveScreen(outPath, screen);
        Console.WriteLine($"wrote {outPath}");
        return 0;
    }

    /// <summary>--player EP TICKS: run the player for N ticks (optional held-right after settle)
    /// on BOARD_01 and render it centred, to player.png. Verifies the player sprite + physics.</summary>
    public static int DumpPlayer(int episode, int ticks, int heldDir = 0)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        var board = set.LoadBoard("BOARD_01");
        Objects.Effects.ApplyForBoard(screen, board);
        var vp = new Viewport(0, 2, VgaScreen.Width, 158); // gameplay viewport (status bar below)
        var textR = new Ui.TextRenderer(shapes);
        var objs = new Objects.ObjectManager(board, shapes, set.Info, screen, episode: episode) { Text = new Ui.TextRenderer(shapes) };
        _ = new Objects.Behaviors(objs);
        objs.ReenterPlayer(1);
        var player = new Objects.Player(objs, vp);
        var controls = new Objects.Controls();
        // XARGON_DEBUG_FRUIT="count[,icon]" presets the fruit counter/icon so the HUD fruit slot can be inspected.
        if (Environment.GetEnvironmentVariable("XARGON_DEBUG_FRUIT") is { Length: > 0 } fr)
        {
            var f = fr.Split(',');
            objs.Pl.Fruit = int.Parse(f[0]);
            if (f.Length > 1) objs.Pl.FruitIcon = int.Parse(f[1]);
        }

        if (heldDir == 8) // water test: drop the player into the first water tile, expect drowning
        {
            for (int tx = 0; tx < Board.W && objs.Objects[0].State != 5; tx++)
                for (int ty = 0; ty < Board.H; ty++)
                {
                    int cell = board.CellIndex(tx, ty);
                    if (cell >= Objects.Tiles.WaterT1 && cell <= Objects.Tiles.WaterT8)
                    {
                        objs.Objects[0].X = tx * 16; objs.Objects[0].Y = ty * 16;
                        objs.Objects[0].State = 0;
                        vp.OriginX = tx * 16 - 160; vp.OriginY = ty * 16 - 100;
                        for (int t = 0; t < 40; t++) { objs.Gamecount++; player.Update(controls); objs.UpdateObjects(vp); }
                        tx = Board.W; break;
                    }
                }
            Console.WriteLine($"water test -> health={objs.Pl.Health} state={objs.Objects[0].State}");
            return 0;
        }

        if (heldDir >= 5) // trigger tests: 5=token(28), 6=checkpoint(17), 7=pad(12)
        {
            int kind = heldDir switch { 5 => 28, 6 => 17, _ => 12 };
            for (int i = 0; i < objs.Objects.Count; i++)
                if (objs.Objects[i].Kind == kind)
                {
                    objs.Objects[0].X = objs.Objects[i].X;
                    objs.Objects[0].Y = objs.Objects[i].Y;
                    objs.Objects[0].State = 0;
                    vp.OriginX = objs.Objects[i].X - 160;
                    vp.OriginY = objs.Objects[i].Y - 100;
                    break;
                }
            int before = objs.Objects.Count;
            for (int t = 0; t < 5; t++) { objs.Gamecount++; objs.UpdateObjects(vp); }
            Console.WriteLine($"trigger test (kind {kind}) -> level={objs.Pl.Level} numInv={objs.Pl.NumInv} objs {before}->{objs.Objects.Count}");
            return 0;
        }

        if (heldDir == 4) // combat test: place the player on the first grunt, expect damage
        {
            for (int i = 0; i < objs.Objects.Count; i++)
                if (objs.Objects[i].Kind == ObjectKind.Grunt)
                {
                    objs.Objects[0].X = objs.Objects[i].X;
                    objs.Objects[0].Y = objs.Objects[i].Y;
                    objs.Objects[0].State = 0;
                    vp.OriginX = objs.Objects[i].X - 160;
                    vp.OriginY = objs.Objects[i].Y - 100;
                    break;
                }
            for (int t = 0; t < 10; t++) { objs.Gamecount++; objs.UpdateObjects(vp); }
            Console.WriteLine($"combat test -> player health={objs.Pl.Health} state={objs.Objects[0].State}");
            return 0;
        }

        if (heldDir == 2) // collection test: place the player on the first emerald
        {
            for (int i = 0; i < objs.Objects.Count; i++)
                if (objs.Objects[i].Kind == ObjectKind.Emerald)
                {
                    objs.Objects[0].X = objs.Objects[i].X;
                    objs.Objects[0].Y = objs.Objects[i].Y;
                    objs.Objects[0].State = 0; // st_stand
                    vp.OriginX = objs.Objects[i].X - 160; // camera on player so it culls in
                    vp.OriginY = objs.Objects[i].Y - 100;
                    break;
                }
            for (int t = 0; t < 3; t++) { objs.Gamecount++; objs.UpdateObjects(vp); }
            Console.WriteLine($"collection test -> emeralds={objs.Pl.Emeralds} score={objs.Pl.Score} objs={objs.Objects.Count}");
            return 0;
        }

        for (int t = 0; t < ticks; t++)
        {
            controls.Dx1 = t > 40 ? heldDir : 0; // let st_begin settle first, then hold a direction
            controls.Fire1 = (heldDir == 3 && t > 40) ? 1 : 0; // heldDir 3 = fire test
            if (controls.Fire1 == 0) controls.Fire1Off = 0;
            objs.Gamecount++;
            player.Update(controls);
            objs.UpdateObjects(vp);
        }
        if (heldDir == 3)
            Console.WriteLine($"fire test -> lasers on screen={objs.CountKind(36)} objs={objs.Objects.Count}");
        Console.WriteLine($"pl: health={objs.Pl.Health} emeralds={objs.Pl.Emeralds} score={objs.Pl.Score} objs={objs.Objects.Count}");

        screen.Clear(248);
        objs.DrawBack(screen, vp);
        BoardRenderer.DrawBoard(screen, vp, board, set.Info, shapes);
        objs.DrawFore(screen, vp);
        player.Draw(screen, shapes);
        Ui.Hud.Draw(screen, shapes, textR, objs.Pl, objs.Status, objs.Gamecount);

        var p = objs.Objects[0];
        string outPath = Path.Combine(Directory.GetCurrentDirectory(), "player.png");
        PngWriter.SaveScreen(outPath, screen);
        Console.WriteLine($"wrote {outPath}  player at ({p.X},{p.Y}) state={p.State} sub={p.SubState} after {ticks} ticks");
        return 0;
    }

    /// <summary>--mainmenu EP SEL: render the main menu over the intro backdrop to mainmenu.png.</summary>
    public static int DumpMainMenu(int episode, int sel)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        var textR = new Ui.TextRenderer(shapes);
        var board = set.LoadBoard("INTRO");
        Objects.Effects.ApplyForBoard(screen, board);
        var objs = new Objects.ObjectManager(board, shapes, set.Info, screen, episode: episode) { Text = new Ui.TextRenderer(shapes) };
        var vp = screen.Main;

        screen.Clear(248);
        objs.DrawBack(screen, vp);
        BoardRenderer.DrawBoard(screen, vp, board, set.Info, shapes);
        objs.DrawFore(screen, vp);

        var menu = new Ui.Menu(GameApp.MainMenuText, GameApp.MainMenuKeys, 1, 8, 24, 11, 8, 24, 1);
        menu.Draw(screen, textR, shapes, sel, 0);

        string outPath = Path.Combine(Directory.GetCurrentDirectory(), "mainmenu.png");
        PngWriter.SaveScreen(outPath, screen);
        Console.WriteLine($"wrote {outPath}");
        return 0;
    }

    /// <summary>--window EP: render a plain window and a dialog window with portrait divider (window.png).</summary>
    public static int DumpWindows(int episode)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        var textR = new Ui.TextRenderer(shapes);
        screen.Clear(0);

        var plain = Ui.Window.Def(1, 8, 8, 4, 0, 0, Ui.WindowStyle.Plain);
        plain.Draw(screen, shapes);
        plain.ClearInside(screen, 129);
        plain.TitleWin(screen, textR, "PLAIN", 15, false);

        var dlg = Ui.Window.Def(20, 8, 8, 4, 2, 0, Ui.WindowStyle.Dialog);
        dlg.Draw(screen, shapes);
        dlg.ClearInside(screen, 121);
        dlg.TitleWin(screen, textR, "DIALOG", 15, true);

        string outPath = Path.Combine(Directory.GetCurrentDirectory(), "window.png");
        PngWriter.SaveScreen(outPath, screen);
        Console.WriteLine($"wrote {outPath}");
        return 0;
    }

    /// <summary>--page EP BOARD PAGE [png]: render a story/credits page exactly as the page viewer would (no fades).</summary>
    public static int DumpPage(int episode, string boardName, int page, string? outFile = null)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        var board = set.LoadBoard(boardName.ToUpperInvariant());
        Objects.Effects.ApplyForBoard(screen, board);
        var objs = new Objects.ObjectManager(board, shapes, set.Info, screen, episode: episode) { Text = new Ui.TextRenderer(shapes) };
        _ = new Objects.Behaviors(objs);

        var anchor = PageView.FindAnchor(board, page);
        if (anchor is null) { Console.WriteLine($"{boardName}.XR{episode}: no page {page}"); return 1; }
        PageView.Render(screen, objs, board, set.Info, shapes, anchor);

        string outPath = outFile ?? Path.Combine(Directory.GetCurrentDirectory(), "page.png");
        PngWriter.SaveScreen(outPath, screen);
        Console.WriteLine($"wrote {outPath}  ({boardName}.XR{episode} page {page} @ ({anchor.X},{anchor.Y}) xd={anchor.Xd} yd={anchor.Yd})");
        return 0;
    }

    /// <summary>--popup EP MSG [OFFSET] [STYLE] [png]: render a level frame, then compose popup message
    /// MSG over it exactly as the modal loop's first frame would (no event loop). STYLE: plain|dialog.</summary>
    public static int DumpPopup(int episode, int msg, int offset, string style, string? outFile)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        var text = new Ui.TextRenderer(shapes);
        var board = set.LoadBoard("BOARD_01");
        Objects.Effects.ApplyForBoard(screen, board);
        var vp = new Viewport(0, 2, VgaScreen.Width, 158);
        var objs = new Objects.ObjectManager(board, shapes, set.Info, screen, episode: episode) { Text = text };
        _ = new Objects.Behaviors(objs);
        objs.ReenterPlayer(1);
        var player = new Objects.Player(objs, vp);
        for (int t = 0; t < 30; t++) { objs.Gamecount++; player.Update(new Objects.Controls()); objs.UpdateObjects(vp); }

        // Draw the level frame (what the visible page holds when dotextmsg is called).
        screen.Clear(248);
        objs.DrawBack(screen, vp);
        BoardRenderer.DrawBoard(screen, vp, board, set.Info, shapes);
        objs.DrawFore(screen, vp);
        player.Draw(screen, shapes);
        Ui.Hud.Draw(screen, shapes, text, objs.Pl, objs.Status, objs.Gamecount);

        var m = set.Text.Get(msg);
        if (m is null) { Console.WriteLine($"AUDIO.XR{episode}: message {msg} is absent"); return 1; }
        var ps = style.Equals("dialog", StringComparison.OrdinalIgnoreCase)
            ? Objects.PopupStyle.Dialog : Objects.PopupStyle.Plain;
        if (style.Equals("hero", StringComparison.OrdinalIgnoreCase) || style.Equals("eagle", StringComparison.OrdinalIgnoreCase))
        {
            // --popup EP MSG 0 hero|eagle [png]: the dialogmsg portrait window.
            PopupWindows.RenderDialogInto(screen, shapes, text, m, style.Equals("eagle", StringComparison.OrdinalIgnoreCase));
            string dPath = outFile ?? Path.Combine(Directory.GetCurrentDirectory(), "dialog.png");
            PngWriter.SaveScreen(dPath, screen);
            Console.WriteLine($"wrote {dPath}  (ep{episode} msg {msg} lines={m.NumLines} dialog={style})");
            return 0;
        }
        PopupWindows.RenderInto(screen, shapes, text, m, ps, offset);

        string outPath = outFile ?? Path.Combine(Directory.GetCurrentDirectory(), "popup.png");
        PngWriter.SaveScreen(outPath, screen);
        Console.WriteLine($"wrote {outPath}  (ep{episode} msg {msg} lines={m.NumLines} style={ps} offset={offset})");
        return 0;
    }

    /// <summary>--banner EP LEVEL [png]: render a level frame, then the putlevelmsg banner over it.</summary>
    public static int DumpBanner(int episode, int level, string? outFile)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        var text = new Ui.TextRenderer(shapes);
        var board = set.LoadBoard("BOARD_01");
        Objects.Effects.ApplyForBoard(screen, board);
        var vp = new Viewport(0, 2, VgaScreen.Width, 158);
        var objs = new Objects.ObjectManager(board, shapes, set.Info, screen, episode: episode) { Text = text };
        _ = new Objects.Behaviors(objs);
        objs.ReenterPlayer(1);
        var player = new Objects.Player(objs, vp);
        for (int t = 0; t < 30; t++) { objs.Gamecount++; player.Update(new Objects.Controls()); objs.UpdateObjects(vp); }
        screen.Clear(248);
        objs.DrawBack(screen, vp);
        BoardRenderer.DrawBoard(screen, vp, board, set.Info, shapes);
        objs.DrawFore(screen, vp);
        player.Draw(screen, shapes);
        Ui.Hud.Draw(screen, shapes, text, objs.Pl, objs.Status, objs.Gamecount);

        string? lt = Text.GameStrings.LevelText(episode, level);
        PopupWindows.RenderBannerInto(screen, shapes, text, lt);
        string outPath = outFile ?? Path.Combine(Directory.GetCurrentDirectory(), "banner.png");
        PngWriter.SaveScreen(outPath, screen);
        Console.WriteLine($"wrote {outPath}  (ep{episode} level {level} lines={PopupWindows.BannerLayout(lt).Count})");
        return 0;
    }

    /// <summary>--menu EP SEL: render the episode-selection screen to menu.png.</summary>
    public static int DumpMenu(int episode, int sel)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var text = new Ui.TextRenderer(new ShapeManager(set.Graphics));
        Ui.EpisodeSelectScreen.Draw(screen, text, sel);
        string outPath = Path.Combine(Directory.GetCurrentDirectory(), "menu.png");
        PngWriter.SaveScreen(outPath, screen);
        Console.WriteLine($"wrote {outPath}");
        return 0;
    }

    /// <summary>--map EP: render the overworld map with the hero, and verify stepping on a level
    /// checkpoint sets NewLevel (the map->level transition).</summary>
    public static int DumpMap(int episode)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        var board = set.LoadBoard("MAP");
        Objects.Effects.ApplyForBoard(screen, board);
        var pl = new PlayerState { Health = 5, Level = 127 };
        var objs = new Objects.ObjectManager(board, shapes, set.Info, screen, pl, episode) { Text = new Ui.TextRenderer(shapes) };
        _ = new Objects.Behaviors(objs);
        objs.ReenterPlayer(127); // no counter-127 checkpoint -> tiny stays at its start
        var vp = new Viewport(0, 2, VgaScreen.Width, 158);
        var hero = new Objects.MapHero(objs, vp);
        var text = new Ui.TextRenderer(shapes);
        Objects.Camera.Center(vp, objs.Objects[0]);

        screen.Clear(248);
        objs.DrawBack(screen, vp);
        BoardRenderer.DrawBoard(screen, vp, board, set.Info, shapes);
        objs.DrawFore(screen, vp);
        hero.Draw(screen, shapes);
        Ui.Hud.Draw(screen, shapes, text, pl, objs.Status, objs.Gamecount);
        PngWriter.SaveScreen(Path.Combine(Directory.GetCurrentDirectory(), "map.png"), screen);
        Console.WriteLine($"wrote map.png  hero(tiny) at ({objs.Objects[0].X},{objs.Objects[0].Y})");

        // Teleport onto board_01's entrance checkpoint (counter=1) and confirm the transition fires.
        for (int i = 0; i < board.Objects.Count; i++)
            if (board.Objects[i].Kind == ObjectKind.Checkpt && board.Objects[i].Counter == 1)
            {
                objs.Objects[0].X = board.Objects[i].X;
                objs.Objects[0].Y = board.Objects[i].Y;
                var ctl = new Objects.Controls();
                objs.Gamecount++;
                objs.UpdateObjects(vp);
                Console.WriteLine($"stepped on board_01 checkpoint -> NewLevel={objs.NewLevel ?? "(none)"} pl.Level={pl.Level}");
                break;
            }
        return 0;
    }

    /// <summary>--census EP: count every object kind used across all of the episode's boards and flag
    /// which kinds still have no behaviour handler. Prioritises what's left to port.</summary>
    public static int Census(int episode)
    {
        string[] boards =
        {
            "MAP", "INTRO", "STORY",
            "BOARD_01", "BOARD_02", "BOARD_03", "BOARD_04", "BOARD_05",
            "BOARD_06", "BOARD_07", "BOARD_08", "BOARD_09", "BOARD_10",
            "BOARD_32", "BOARD_33",
        };
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);

        var total = new int[KindDefs.Count];
        var boardsWith = new int[KindDefs.Count];
        bool[] handled = new bool[KindDefs.Count];
        foreach (var name in boards)
        {
            Board board;
            try { board = set.LoadBoard(name); } catch { continue; }
            var objs = new Objects.ObjectManager(board, shapes, set.Info, screen, episode: episode) { Text = new Ui.TextRenderer(shapes) };
            _ = new Objects.Behaviors(objs);
            for (int k = 0; k < KindDefs.Count; k++) handled[k] = objs.HasBehavior(k);
            var seen = new bool[KindDefs.Count];
            foreach (var o in board.Objects)
                if (o.Kind < KindDefs.Count) { total[o.Kind]++; if (!seen[o.Kind]) { seen[o.Kind] = true; boardsWith[o.Kind]++; } }
        }

        // Player (0) and tiny (5) are handled by Player/MapHero, not a registered behaviour.
        handled[0] = handled[5] = true;
        Console.WriteLine($"Episode {episode} object-kind census (count / boards / handled):");
        Console.WriteLine("  UNHANDLED kinds that appear in boards (priority to port):");
        for (int k = 0; k < KindDefs.Count; k++)
            if (total[k] > 0 && !handled[k])
                Console.WriteLine($"    [{k,2}] {KindDefs.Get(k).Name,-12} x{total[k],-4} in {boardsWith[k]} boards");
        Console.WriteLine("  handled kinds present: " +
            string.Join(", ", Enumerable.Range(0, KindDefs.Count).Where(k => total[k] > 0 && handled[k]).Select(k => KindDefs.Get(k).Name)));
        return 0;
    }

    /// <summary>--transport NAME EP: drop the player onto the first transpad, hold Up, and report the
    /// st_transport hand-off and final (teleported) position. Verifies the door/teleport flow.</summary>
    public static int DumpTransport(string name, int episode)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        var board = set.LoadBoard(name.ToUpperInvariant());
        Objects.Effects.ApplyForBoard(screen, board);
        var pl = new PlayerState { Health = 5, Level = 1 };
        var objs = new Objects.ObjectManager(board, shapes, set.Info, screen, pl, episode) { Text = new Ui.TextRenderer(shapes) };
        _ = new Objects.Behaviors(objs);
        var vp = new Viewport(0, 2, VgaScreen.Width, 158);
        var player = new Objects.Player(objs, vp);

        // Find the first transpad and its paired transport (same counter).
        int pad = -1, dest = -1;
        for (int i = 0; i < board.Objects.Count; i++) if (board.Objects[i].Kind == ObjectKind.TransPad) { pad = i; break; }
        if (pad < 0) { Console.WriteLine($"{name}: no transpad"); return 1; }
        int cnt = board.Objects[pad].Counter;
        for (int i = 0; i < board.Objects.Count; i++)
            if (board.Objects[i].Kind == ObjectKind.Transport && board.Objects[i].Counter == cnt) { dest = i; break; }

        var p = objs.Objects[0];
        p.X = board.Objects[pad].X; p.Y = board.Objects[pad].Y; p.State = 0; p.StateCount = 60;
        Objects.Camera.Center(vp, p);
        Console.WriteLine($"pad[{pad}] cnt={cnt} at ({board.Objects[pad].X},{board.Objects[pad].Y}); " +
            $"dest transport[{dest}] at ({(dest >= 0 ? board.Objects[dest].X : -1)},{(dest >= 0 ? board.Objects[dest].Y : -1)})");
        Console.WriteLine($"player start ({p.X},{p.Y}) state={p.State}");

        var ctl = new Objects.Controls();
        int enteredAt = -1;
        for (int f = 0; f < 40; f++)
        {
            int dy = enteredAt < 0 ? -1 : 0; // tap Up to enter, then release (as a human would)
            ctl.Dy1 = dy;
            objs.InputDx = 0; objs.InputDy = dy;
            objs.Gamecount++;
            player.Update(ctl);
            objs.UpdateObjects(vp);
            if (p.State == 6 && enteredAt < 0) { enteredAt = f; Console.WriteLine($"  f{f}: entered st_transport at ({p.X},{p.Y}) substate={p.SubState}"); }
            if (enteredAt >= 0 && p.State == 0) { Console.WriteLine($"  f{f}: teleported -> ({p.X},{p.Y}) state=stand"); break; }
        }
        Console.WriteLine($"final player ({p.X},{p.Y}) state={p.State}");
        return 0;
    }

    /// <summary>--objlist NAME EP: print the first ~24 objects (index, kind+name, pos, state, inside).</summary>
    public static int ObjList(string name, int episode)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var board = set.LoadBoard(name);
        Console.WriteLine($"{name}.XR{episode}: {board.Objects.Count} objects");
        for (int i = 0; i < board.Objects.Count && i < 80; i++)
        {
            var o = board.Objects[i];
            Console.WriteLine($"  [{i,2}] kind={o.Kind,2} {Objects.KindDefs.Get(o.Kind).Name,-10} " +
                $"({o.X,4},{o.Y,4}) st={o.State} cnt={o.Counter} inside={(o.Inside is null ? "-" : "\"" + o.Inside + "\"")}");
        }
        return 0;
    }

    /// <summary>--findobj NAME EP KIND: print objects of a given kind with their state fields.</summary>
    public static int FindObj(string name, int episode, int kind)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var board = set.LoadBoard(name);
        int count = 0;
        foreach (var o in board.Objects)
            if (o.Kind == kind)
            {
                count++;
                Console.WriteLine($"  kind={o.Kind} at ({o.X},{o.Y}) xd={o.Xd} yd={o.Yd} state={o.State} sub={o.SubState} counter={o.Counter}");
            }
        Console.WriteLine($"{name}.XR{episode}: {count} object(s) of kind {kind}");
        return 0;
    }

    /// <summary>--cells NAME EP X Y0 Y1: print the info entries for a vertical column of cells.</summary>
    public static int DumpCells(string name, int episode, int x, int y0, int y1)
    {
        string dir = AssetSet.FindAssetsDir();
        var set = AssetSet.Mount(dir, episode);
        var board = set.LoadBoard(name);
        Console.WriteLine($"{name}.XR{episode} column x={x}, y={y0}..{y1}:");
        for (int y = y0; y <= y1 && y < Board.H; y++)
        {
            int cell = board.CellIndex(x, y);
            var e = set.Info.Entries[cell % InfoTable.Count];
            Console.WriteLine($"  y={y,2} cell={cell,4} sh=0x{e.Sh:X4} '{e.Name}'");
        }
        return 0;
    }

    /// <summary>--derive-palette NAME EP REF.png: reconstruct the true 6-bit palette by rendering
    /// the board to an index buffer and reading each index's colour from the reference image.</summary>
    public static int DerivePalette(string name, int episode, string refPath)
    {
        string dir = AssetSet.FindAssetsDir();
        var set = AssetSet.Mount(dir, episode);
        var shapes = new ShapeManager(set.Graphics);
        var board = set.LoadBoard(name);

        int w = Board.W * 16, h = Board.H * 16;
        var idx = new byte[w * h];
        var covered = new bool[w * h];
        // Draw every cell's shape, recording which index each pixel got (opaque pixels only).
        for (int tx = 0; tx < Board.W; tx++)
            for (int ty = 0; ty < Board.H; ty++)
            {
                var e = set.Info.Entries[board.CellIndex(tx, ty) % InfoTable.Count];
                var sh = shapes.GetShape((e.Sh >> 8) & 0x3f, e.Sh & 0xff);
                if (sh is null) continue;
                for (int sy = 0; sy < sh.Height; sy++)
                    for (int sx = 0; sx < sh.Width; sx++)
                    {
                        byte c = sh.Pixels[sy * sh.Width + sx];
                        if (c == 0) continue; // transparent
                        int px = tx * 16 + sx, py = ty * 16 + sy;
                        if (px >= w || py >= h) continue;
                        idx[py * w + px] = c;
                        covered[py * w + px] = true;
                    }
            }

        var (rw, rh, rgb) = PngReader.Load(refPath);
        if (rw != w || rh != h) { Console.WriteLine($"size mismatch: board {w}x{h} vs ref {rw}x{rh}"); return 1; }

        // For each index, find the most common reference colour among its pixels.
        var votes = new Dictionary<int, int>[256];
        for (int i = 0; i < 256; i++) votes[i] = new();
        for (int i = 0; i < idx.Length; i++)
        {
            if (!covered[i]) continue;
            int rgbKey = (rgb[i * 3] << 16) | (rgb[i * 3 + 1] << 8) | rgb[i * 3 + 2];
            votes[idx[i]][rgbKey] = votes[idx[i]].GetValueOrDefault(rgbKey) + 1;
        }

        var dac = new byte[768];
        for (int i = 0; i < 256; i++)
        {
            if (votes[i].Count == 0) continue;
            int best = votes[i].OrderByDescending(k => k.Value).First().Key;
            dac[i * 3] = (byte)Math.Round(((best >> 16) & 0xff) * 63.0 / 255);
            dac[i * 3 + 1] = (byte)Math.Round(((best >> 8) & 0xff) * 63.0 / 255);
            dac[i * 3 + 2] = (byte)Math.Round((best & 0xff) * 63.0 / 255);
        }

        string outPath = Path.Combine(Directory.GetCurrentDirectory(), "derived_palette.bin");
        File.WriteAllBytes(outPath, dac);
        Console.WriteLine($"wrote {outPath} (768 bytes, 6-bit DAC). Coverage: {votes.Count(v => v.Count > 0)}/256 indices.");
        foreach (int i in new[] { 176, 179, 182, 248, 249, 250, 251 })
            Console.WriteLine($"  idx {i}: 6-bit=({dac[i * 3]},{dac[i * 3 + 1]},{dac[i * 3 + 2]})");
        return 0;
    }

    private static void BlitBig(byte[] buf, int w, int h, ShapeManager shapes, int shapeId, int dx, int dy)
    {
        var sh = shapes.GetShape((shapeId >> 8) & 0x3f, shapeId & 0xff);
        if (sh is null) return;
        for (int sy = 0; sy < sh.Height; sy++)
        {
            int py = dy + sy;
            if ((uint)py >= (uint)h) continue;
            for (int sx = 0; sx < sh.Width; sx++)
            {
                int px = dx + sx;
                if ((uint)px >= (uint)w) continue;
                byte c = sh.Pixels[sy * sh.Width + sx];
                if (c != 0) buf[py * w + px] = c;
            }
        }
    }

    /// <summary>--dump-table EPISODE TABLE: render every shape in a table as a montage to table.png.</summary>
    public static int DumpTable(int episode, int table)
    {
        string dir = AssetSet.FindAssetsDir();
        var set = AssetSet.Mount(dir, episode);
        var screen = new VgaScreen(); // compiled-in default palette

        var shapes = new ShapeManager(set.Graphics);
        var t = shapes.Table(table);
        if (t is null) { Console.WriteLine($"table {table} not present"); return 1; }

        screen.Clear(0);
        var vp = screen.Main;

        int cell = 20; // grid cell size
        int cols = VgaScreen.Width / cell;
        for (int i = 0; i < t.Shapes.Count; i++)
        {
            int gx = (i % cols) * cell + 1;
            int gy = (i / cols) * cell + 1;
            if (gy >= VgaScreen.Height) break;
            ShapeManager.Blit(screen, vp, t.Shapes[i], gx, gy);
        }

        string outPath = Path.Combine(Directory.GetCurrentDirectory(), "table.png");
        PngWriter.SaveScreen(outPath, screen);
        Console.WriteLine($"wrote {outPath}  (episode {episode} table {table}: {t.Shapes.Count} shapes, bits={t.NumColorBits}, flags=0x{t.Flags:X})");
        return 0;
    }
}
