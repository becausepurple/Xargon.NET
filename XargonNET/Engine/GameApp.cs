using SDL3;
using XargonNET.Assets;
using XargonNET.Objects;
using XargonNET.Ui;
using XargonNET.Video;

namespace XargonNET.Engine;

/// <summary>
/// Owns the SDL window/renderer and the software VGA screen, and runs the top-level game flow:
/// episode selector -> mount episode -> play, with palette fades between states. The DOS original
/// shipped one executable per episode; this unified port picks the episode up front and mounts the
/// matching .XR data set.
/// </summary>
public sealed class GameApp : IDisposable
{
    private const int Scale = 3;

    private IntPtr _window;
    private IntPtr _renderer;
    private VgaScreen _screen = null!;
    private SdlPresenter _presenter = null!;
    private bool _running;
    private bool _quit;

    private string _assetsDir = "";
    private int _episode;
    private AssetSet _assets = null!;
    private ShapeManager _shapes = null!;
    private TextRenderer _text = null!;

    private Board _board = null!;
    private ObjectManager _objects = null!;
    private Player? _player;
    private MapHero? _mapHero;
    private Assets.PlayerState _pl = null!; // persistent player state across the adventure
    private LevelSession _session = null!;  // death/respawn/level-transition state
    private readonly Controls _controls = new();
    private readonly Viewport _gameVp = new(0, 2, VgaScreen.Width, 158); // play area above the status bar
    private readonly HashSet<SDL.Keycode> _keys = new();
    private readonly TickClock _clock = new(); // 18.2 Hz BIOS-tick pacing, as in the original loop
    private PopupWindows _popups = null!;       // blocking text-popup host (dotextmsg)
    private HighScoreWindow _highScores = null!;   // printhi(0)/printhi(1) modals
    private HintFlags _hints = new();           // first-time hints, reset per episode mount

    public bool Init()
    {
        if (!SDL.Init(SDL.InitFlags.Video))
        {
            Console.WriteLine($"SDL_Init failed: {SDL.GetError()}");
            return false;
        }
        _window = SDL.CreateWindow("Xargon.NET", VgaScreen.Width * Scale, VgaScreen.Height * Scale, 0);
        if (_window == IntPtr.Zero) { Console.WriteLine($"CreateWindow: {SDL.GetError()}"); return false; }
        _renderer = SDL.CreateRenderer(_window, null);
        if (_renderer == IntPtr.Zero) { Console.WriteLine($"CreateRenderer: {SDL.GetError()}"); return false; }
        SDL.SetRenderLogicalPresentation(_renderer, VgaScreen.Width, VgaScreen.Height,
            SDL.RendererLogicalPresentation.Letterbox);

        _screen = new VgaScreen();
        _presenter = new SdlPresenter(_renderer);
        // The blocking text-popup host (dotextmsg). It reads the current episode's shapes/text/text bank
        // lazily so it stays correct across episode mounts, and composes over the live _screen.
        _popups = new PopupWindows(_screen, () => _shapes, () => _text, () => _assets.Text,
            _presenter, _clock, _controls, _keys, () => _quit = true, () => _quit);
        var nameInput = new TextInputBox(_screen, () => _text, _presenter, _clock, _controls, _keys,
            () => _quit = true, () => _quit, _window);
        _highScores = new HighScoreWindow(_screen, () => _shapes, () => _text, _presenter, _popups, nameInput,
            new HighScoreStore(UserDataStore.Default()), () => _episode);

        try
        {
            _assetsDir = AssetSet.FindAssetsDir();
            MountEpisode(1); // initial mount for the shell (fonts/palette)
        }
        catch (Exception ex) { Console.WriteLine($"Asset load failed: {ex.Message}"); return false; }
        return true;
    }

    private void MountEpisode(int episode)
    {
        _episode = episode;
        _assets = AssetSet.Mount(_assetsDir, episode);
        _assets.LoadPalette(_screen);
        _shapes = new ShapeManager(_assets.Graphics);
        _text = new TextRenderer(_shapes);
        _hints = new HintFlags(); // first_* reset only on an episode mount (open question 5 ruling)
    }

    public void Run()
    {
        _running = true;
        while (_running && !_quit)
        {
            int ep = RunEpisodeSelect();
            if (ep <= 0) break;
            if (ep != _episode) MountEpisode(ep);
            RunMainMenu();
        }
    }

    // ---- Main menu (mainmenu/domenu over the intro board backdrop) ------------------------

    // The original's "ORDERING INFO" entry (30-year-old ordering details) is intentionally dropped.
    internal const string MainMenuText =
        "7\r3PLAY NEW GAME\r3LOAD GAME\r3INSTRUCTIONS\r3THE STORY\r3DEMO-PREVIEW\r3HIGH SCORES\r3CREDITS\r7EXIT\r\r";
    internal const string MainMenuKeys = "PLISDHCE";

    private void RunMainMenu()
    {
        var menu = new Menu(MainMenuText, MainMenuKeys, 1, 8, 24, 11, 8, 24, 1);
        LoadBackdrop();
        int sel = 0;
        DrawMenuFrame(menu, sel, 0);
        FadeIn();

        while (_running && !_quit)
        {
            char chosen = RunMenu(menu, 'E', ref sel,
                draw: (s, f) => DrawMenuFrame(menu, s, f),
                tick: () =>
                {
                    _objects.Gamecount++;
                    _objects.UpdateObjects(_gameVp, touch: false); // animate backdrop (torches, water)
                });
            if (chosen == '\0') return; // window closed (quit latched)

            if (!HandleMenuChoice(chosen)) return; // EXIT -> back to episode selector
            LoadBackdrop();
            DrawMenuFrame(menu, sel, 0);
            FadeIn();
        }
    }

    /// <summary>
    /// The shared domenu loop (main menu now, game/buy menu later). Per tick: drain events (arrows
    /// move the clamped selection, Return/Space/letters/Esc/F1 resolve via <see cref="Menu.ResolveKey"/>),
    /// then <paramref name="tick"/> (backdrop animation) and <paramref name="draw"/>, present, wait.
    /// Returns the chosen key, or '\0' when the window was closed (sets the quit flag).
    /// </summary>
    private char RunMenu(Menu menu, char escKey, ref int sel, Action<int, int> draw, Action? tick = null)
    {
        int frame = 0;
        while (_running && !_quit)
        {
            while (SDL.PollEvent(out SDL.Event e))
            {
                if ((SDL.EventType)e.Type == SDL.EventType.Quit) { _quit = true; return '\0'; }
                if ((SDL.EventType)e.Type != SDL.EventType.KeyDown) continue;
                char key = e.Key.Key switch
                {
                    SDL.Keycode.Up => '\0',
                    SDL.Keycode.Down => '\0',
                    SDL.Keycode.Return => '\r',
                    SDL.Keycode.Space => ' ',
                    SDL.Keycode.Escape => Menu.KeyEsc,
                    SDL.Keycode.F1 => Menu.KeyF1,
                    _ => LetterKey(e.Key.Key),
                };
                if (e.Key.Key == SDL.Keycode.Up) sel = Menu.MoveSelection(sel, -1, menu.ItemCount);
                else if (e.Key.Key == SDL.Keycode.Down) sel = Menu.MoveSelection(sel, +1, menu.ItemCount);
                char chosen = Menu.ResolveKey(key, menu.Keytab, sel, escKey);
                if (chosen != '\0') return chosen;
            }

            frame++;
            tick?.Invoke();
            draw(sel, frame);
            _presenter.Present(_screen);
            _clock.Wait();
        }
        return '\0';
    }

    /// <summary>Returns false only for EXIT (leave the menu back to the episode selector).</summary>
    private bool HandleMenuChoice(char key)
    {
        switch (key)
        {
            case 'P': // PLAY NEW GAME -> the overworld map, then levels reached from it
                FadeOut();
                RunAdventure();
                return true;
            case 'I': // INSTRUCTIONS -> dotextmsg(1) (XARGON HELP, scroll form) over the menu
                _popups.TextMessage(1, PopupStyle.Plain);
                return true;
            case 'S': // THE STORY -> page viewer over the STORY board, from page 0
                RunPageView("STORY", 0);
                return true;
            case 'C': // CREDITS -> page viewer over the INTRO board, from page 20
                RunPageView("INTRO", 20);
                return true;
            case 'E': // EXIT
                FadeOut();
                return false;
            case 'H': // HIGH SCORES -> printhi(0): the table over the menu, then rest()
                _highScores.Show();
                return true;
            default: // LOAD/DEMO — not implemented yet
                return true;
        }
    }

    private static char LetterKey(SDL.Keycode k)
    {
        int c = (int)k;
        if (c >= 'a' && c <= 'z') return (char)(c - 32);
        return '\0';
    }

    private void LoadBackdrop()
    {
        _board = _assets.LoadBoard("INTRO");
        Effects.ApplyForBoard(_screen, _board);
        _objects = new ObjectManager(_board, _shapes, _assets.Info, _screen, episode: _episode)
        { Text = _text, Popups = _popups, Hints = _hints };
        _ = new Behaviors(_objects);
        _player = null; // menu backdrop has no controllable player
        _mapHero = null;
        _gameVp.OriginX = 0;
        _gameVp.OriginY = 0;
    }

    // ---- Adventure: overworld map <-> platformer levels -----------------------------------

    // TASK7: replace with the full game menu (RQSLIABGN, 9 items). Same window style as the real one, two items.
    internal const string SkeletonGameMenuText =
        "7    - GAME OPTIONS -    \r3RETURN TO GAME\r2QUIT GAME\r\r";
    internal const string SkeletonGameMenuKeys = "RQ";

    /// <summary>
    /// Plays the adventure until the game ends. Contract with the main menu: returns normally (after the
    /// win pages / high scores for Quit and Won) so the menu redraws; when the window is closed the quit
    /// flag is latched and it returns immediately WITHOUT the high-score prompt (the app is exiting, so
    /// nothing is shown or saved).
    /// </summary>
    private void RunAdventure()
    {
        _pl = Assets.PlayerState.NewGame();
        _pl.Level = LevelSession.MapLevel;
        _session = new LevelSession(name => _assets.LoadBoard(name.ToUpperInvariant()),
            _shapes, _assets.Info, _screen, _gameVp, _pl, _text, _episode)
        { Popups = _popups, Hints = _hints };
        AdoptWorld(_session.Load("MAP"));
        FadeIn();

        var debug = GameFlow.ParseDebug(Environment.GetEnvironmentVariable(GameFlow.DebugEnvVar));
        var over = GameOver.None;
        int ticks = 0;

        while (_running && !_quit && over == GameOver.None)
        {
            while (SDL.PollEvent(out SDL.Event e))
            {
                switch ((SDL.EventType)e.Type)
                {
                    case SDL.EventType.Quit: _quit = true; return;
                    case SDL.EventType.KeyDown:
                        if (e.Key.Key is SDL.Keycode.Escape or SDL.Keycode.Q) // escape / 'Q' -> gamemenu()
                        {
                            if (RunGameMenu()) over = GameOver.Quit;
                            if (_quit) return;
                            break;
                        }
                        if (e.Key.Key == SDL.Keycode.G) _clock.TicksPerFrame = 3 - _clock.TicksPerFrame; // granny mode
                        _keys.Add(e.Key.Key);
                        break;
                    case SDL.EventType.KeyUp: _keys.Remove(e.Key.Key); break;
                }
                if (over != GameOver.None) break;
            }
            if (over != GameOver.None) break;

            _objects.Gamecount++;
            _controls.Poll(_keys);
            _objects.InputDx = _controls.Dx1;
            _objects.InputDy = _controls.Dy1;
            _objects.CurControls = _controls;
            if (_mapHero is not null) _mapHero.Update(_controls);
            else _player?.Update(_controls);

            if (_player?.Died == true)
            {
                AdoptWorld(_session.Die()); // death -> reload level + restore snapshot (p_reenter(1))
            }
            else
            {
                _objects.UpdateObjects(_gameVp);
                _objects.UpdBotMsg(); // upd_botmsg(): after upd_objs(1), before refresh
                if (_objects.NewLevel is not null) HandleTransition();
            }
            over = GameFlow.PollGameWon(_objects); // checkpoint state 4: gameover=2

            DrawLevel();
            _presenter.Present(_screen);
            _clock.Wait();

            ticks++;
            if (over == GameOver.None && debug is { } d && ticks == d.AfterTicks) over = d.Reason; // env-var only
        }

        if (over != GameOver.None && !_quit) RunGameEnd(over);
    }

    /// <summary>The temporary game menu (gamemenu): RETURN resumes play (keys cleared, clock reset); QUIT
    /// GAME returns true (gameover=1, no confirmation). Closing the window latches the quit flag.</summary>
    private bool RunGameMenu()
    {
        var menu = new Menu(SkeletonGameMenuText, SkeletonGameMenuKeys, 1, 2, 24, 9, 10, 24, 0);
        int sel = 0;
        char chosen = RunMenu(menu, 'R', ref sel, draw: (s, f) => DrawMenuFrame(menu, s, f));
        _keys.Clear();
        _clock.Reset();
        return chosen == 'Q';
    }

    /// <summary>play() tail: [pageview(200) if won] then printhi(1), per <see cref="GameFlow.Plan"/>.</summary>
    private void RunGameEnd(GameOver over)
    {
        var steps = GameFlow.Plan(over, demo: false);
        if (steps.ShowWinPages && GameFlow.WinAnchor(_board) is not null)
        {
            FadeOut();
            RunPageView(_board, _objects, GameFlow.WinPage); // live board; ends faded back in on a cleared screen
        }
        else if (steps.ShowWinPages) Console.WriteLine("Win pages skipped: no page-200 checkpoint on the live board.");
        else
        {
            DrawLevel(); // drop the game-menu window before the table is drawn over the live frame
            _presenter.Present(_screen);
        }
        if (_quit) return;
        if (steps.ShowHighScores) _highScores.ShowAfterGame(_pl.Score); // the live score; no rollback
    }

    /// <summary>Bind the rendering/controller state to a freshly built world (the camera is already
    /// framed by <see cref="LevelSession"/>). The hero's kind picks the top-down map or platformer controller.</summary>
    private void AdoptWorld(LevelSession.World w)
    {
        _board = w.Board;
        _objects = w.Objects;
        _keys.Clear();

        int heroKind = _board.Objects.Count > 0 ? _board.Objects[0].Kind : 0;
        if (heroKind == ObjectKind.Tiny) { _mapHero = new MapHero(_objects, _gameVp); _player = null; } // obj_tiny
        else { _player = new Player(_objects, _gameVp); _mapHero = null; }
    }

    private void HandleTransition()
    {
        string nl = _objects.NewLevel!;
        _objects.NewLevel = null;
        char c0 = nl.Length > 0 ? nl[0] : '\0';
        if (nl == "!")
        {
            // putlevelmsg(0); loadboard(map); p_reenter; donelevelmsg(): no fades (the banner covers the swap).
            long t0 = _popups.ShowLevelBanner(_episode, 0);
            var w = _session.ExitToMap(deferPalette: true);
            _popups.WaitLevelBanner(t0);
            _session.ApplyPendingPalette();
            AdoptWorld(w);
        }
        else if (c0 is '#' or '*' or '&') { /* level song — Phase 7 audio */ }
        else
        {
            // putlevelmsg(pl.level) (set by the map checkpoint); saveboard/loadboard; donelevelmsg().
            long t0 = _popups.ShowLevelBanner(_episode, _pl.Level);
            var w = _session.EnterLevel(nl, deferPalette: true);
            _popups.WaitLevelBanner(t0);
            _session.ApplyPendingPalette();
            AdoptWorld(w);
        }
    }

    private void DrawMenuFrame(Menu menu, int sel, int frame)
    {
        DrawLevel();
        menu.Draw(_screen, _text, _shapes, sel, frame / 2);
    }

    // ---- Episode selector -----------------------------------------------------------------

    private int RunEpisodeSelect()
    {
        int sel = _episode >= 1 && _episode <= 3 ? _episode : 1;
        MountEpisode(sel == _episode ? _episode : sel); // ensure fonts/palette loaded
        DrawEpisodeSelect(sel);
        FadeIn();

        while (_running && !_quit)
        {
            bool redraw = false;
            while (SDL.PollEvent(out SDL.Event e))
            {
                if ((SDL.EventType)e.Type == SDL.EventType.Quit) { _quit = true; return 0; }
                if ((SDL.EventType)e.Type != SDL.EventType.KeyDown) continue;
                switch (e.Key.Key)
                {
                    case SDL.Keycode.Escape: _quit = true; return 0;
                    case SDL.Keycode.Up: sel = sel > 1 ? sel - 1 : 3; redraw = true; break;
                    case SDL.Keycode.Down: sel = sel < 3 ? sel + 1 : 1; redraw = true; break;
                    case SDL.Keycode.Alpha1: sel = 1; redraw = true; break;
                    case SDL.Keycode.Alpha2: sel = 2; redraw = true; break;
                    case SDL.Keycode.Alpha3: sel = 3; redraw = true; break;
                    case SDL.Keycode.Return:
                    case SDL.Keycode.Space:
                        FadeOut();
                        return sel;
                }
            }
            if (redraw) DrawEpisodeSelect(sel);
            _presenter.Present(_screen);
            SDL.Delay(14);
        }
        return 0;
    }

    private void DrawEpisodeSelect(int sel) => EpisodeSelectScreen.Draw(_screen, _text, sel);

    // ---- Story / credits page viewer (pageview) ---------------------------------------------

    /// <summary>pageview(): show full-screen pages of <paramref name="boardName"/>, each a 320x200 window whose
    /// origin is the page's checkpoint. Up = previous, Down/Space/Enter/fire keys = next, Esc = leave.</summary>
    private void RunPageView(string boardName, int startPage)
    {
        FadeOut();
        var board = _assets.LoadBoard(boardName);
        Effects.ApplyForBoard(_screen, board);
        var objects = new ObjectManager(board, _shapes, _assets.Info, _screen, episode: _episode)
        { Text = _text, Popups = _popups, Hints = _hints };
        _ = new Behaviors(objects);
        RunPageView(board, objects, startPage);
    }

    /// <summary>pageview(startPage) over an already-loaded (live) board and its objects, as the C does for the
    /// win pages (page 200 onward over whatever board is loaded at game over). The screen must already be
    /// faded out. A missing page skips gracefully.</summary>
    private void RunPageView(Board board, ObjectManager objects, int startPage)
    {
        int page = startPage;
        bool leave = false;
        while (!leave && !_quit)
        {
            var anchor = PageView.FindAnchor(board, page);
            if (anchor is null) break; // no such page: leave gracefully (the C would spin)

            PageView.Render(_screen, objects, board, _assets.Info, _shapes, anchor);
            FadeIn();
            if (_quit) return;

            PageNav nav = PageNav.Stay;
            while (nav.Kind == PageNavKind.Stay && !_quit)
            {
                while (SDL.PollEvent(out SDL.Event e))
                {
                    if ((SDL.EventType)e.Type == SDL.EventType.Quit) { _quit = true; return; }
                    if ((SDL.EventType)e.Type != SDL.EventType.KeyDown || e.Key.Repeat) continue;
                    PageKey? key = e.Key.Key switch
                    {
                        SDL.Keycode.Up => PageKey.Back,
                        SDL.Keycode.Escape => PageKey.Escape,
                        SDL.Keycode.Down or SDL.Keycode.Space or SDL.Keycode.Return or SDL.Keycode.KpEnter
                            or SDL.Keycode.LShift or SDL.Keycode.RShift or SDL.Keycode.LCtrl or SDL.Keycode.RCtrl
                            or SDL.Keycode.LAlt or SDL.Keycode.RAlt => PageKey.Forward,
                        _ => null,
                    };
                    if (key is null) continue;
                    nav = PageView.Navigate(anchor, page, key.Value);
                    if (nav.Kind != PageNavKind.Stay) break;
                }
                if (nav.Kind != PageNavKind.Stay) break;
                _presenter.Present(_screen);
                SDL.Delay(14);
            }

            if (_quit) return;
            if (nav.Kind == PageNavKind.GoTo) { page = nav.Page; FadeOut(); }
            else leave = true;
        }

        // Exit: fade out, clear the main viewport to 248, fade back in (as the C does).
        FadeOut();
        _screen.Clear(248);
        FadeIn();
    }

    // ---- Drawing --------------------------------------------------------------------------

    private void DrawLevel()
    {
        bool inGame = _player is not null || _mapHero is not null;
        _screen.Clear(248);
        // drawstats() head: restore the hero palette at the end of a hurt/heal flash. Hero-kind
        // independent and before any object draw, so it also ticks for a bird/fish/map hero.
        if (inGame) _objects.HeroColorFlashPre(_screen);
        _objects.DrawBack(_screen, _gameVp);
        BoardRenderer.DrawBoard(_screen, _gameVp, _board, _assets.Info, _shapes);
        // refresh(): the hero (object 0) is the last of the reverse normal pass, before front objects.
        _objects.DrawFore(_screen, _gameVp, () =>
        {
            _player?.Draw(_screen, _shapes);
            _mapHero?.Draw(_screen, _shapes);
        });
        if (inGame)
        {
            _objects.UpdateFruitHeart(); // drawstats(): 16 fruit -> +1 heart, just before the hearts are drawn
            Hud.Draw(_screen, _shapes, _text, _objects.Pl, _objects.Status, _objects.Gamecount); // status bar in the map/levels
            _objects.HeroColorFlashPost(_screen); // refresh() tail: step/flash the hurt-flash counter
        }
    }

    // ---- Fades ----------------------------------------------------------------------------

    private void FadeIn() => Fade(0, 256);
    private void FadeOut() => Fade(256, 0);

    private void Fade(int from, int to)
    {
        int steps = 16;
        for (int i = 0; i <= steps; i++)
        {
            // Drain events so the OS sees the window as responsive during the fade.
            while (SDL.PollEvent(out SDL.Event e))
                if ((SDL.EventType)e.Type == SDL.EventType.Quit) _quit = true;
            int b = from + (to - from) * i / steps;
            _presenter.Present(_screen, b);
            SDL.Delay(14);
        }
        _clock.Reset();
    }

    public void Dispose()
    {
        _presenter?.Dispose();
        if (_renderer != IntPtr.Zero) SDL.DestroyRenderer(_renderer);
        if (_window != IntPtr.Zero) SDL.DestroyWindow(_window);
        SDL.Quit();
    }
}
