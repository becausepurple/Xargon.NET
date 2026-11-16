using SDL3;
using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Ui;
using XargonNET.Video;
using Xunit;

namespace XargonNET.Tests;

public class HotkeyTests
{
    private static SDL.Keycode K(char upper) => (SDL.Keycode)(int)upper; // upper-case code, as toupper(key) would see

    private static HotkeyAction Press(HotkeyState s, SDL.Keycode k, bool repeat = false) =>
        Hotkeys.Decide(s, new KeyPress(k, repeat));

    // ---- spec 1.2 table ---------------------------------------------------------------------

    [Theory]
    [InlineData(SDL.Keycode.F1, HotkeyAction.Help)]
    [InlineData(SDL.Keycode.S, HotkeyAction.Save)]
    [InlineData(SDL.Keycode.L, HotkeyAction.Load)]
    [InlineData(SDL.Keycode.P, HotkeyAction.Pause)]
    [InlineData(SDL.Keycode.B, HotkeyAction.Buy)]
    [InlineData(SDL.Keycode.Return, HotkeyAction.Inventory)]
    [InlineData(SDL.Keycode.KpEnter, HotkeyAction.Inventory)]
    [InlineData(SDL.Keycode.G, HotkeyAction.ToggleGranny)]
    [InlineData(SDL.Keycode.N, HotkeyAction.ToggleNoise)]
    [InlineData(SDL.Keycode.Escape, HotkeyAction.OpenGameMenu)]
    [InlineData(SDL.Keycode.Q, HotkeyAction.OpenGameMenu)]
    public void EveryHotkeyMapsToItsAction(SDL.Keycode key, HotkeyAction expected)
    {
        Assert.Equal(expected, Press(new HotkeyState(), key));
        // letters are case-insensitive: the upper-case code does the same
        if (key >= SDL.Keycode.A && key <= SDL.Keycode.Z)
            Assert.Equal(expected, Press(new HotkeyState(), K(char.ToUpperInvariant((char)(int)key))));
    }

    [Theory]
    [InlineData(SDL.Keycode.Space)]
    [InlineData(SDL.Keycode.A)]
    [InlineData(SDL.Keycode.X)]
    [InlineData(SDL.Keycode.F2)]
    [InlineData(SDL.Keycode.F7)] // the first F7 is just a streak start
    [InlineData(SDL.Keycode.Z)]
    [InlineData(SDL.Keycode.Alpha1)]
    public void OtherKeysDoNothing(SDL.Keycode key) => Assert.Equal(HotkeyAction.None, Press(new HotkeyState(), key));

    [Theory]
    [InlineData(SDL.Keycode.F1)]
    [InlineData(SDL.Keycode.S)]
    [InlineData(SDL.Keycode.L)]
    [InlineData(SDL.Keycode.P)]
    [InlineData(SDL.Keycode.B)]
    [InlineData(SDL.Keycode.Return)]
    [InlineData(SDL.Keycode.G)]
    [InlineData(SDL.Keycode.N)]
    [InlineData(SDL.Keycode.Escape)]
    [InlineData(SDL.Keycode.Q)]
    public void AutoRepeatNeverTriggers(SDL.Keycode key)
    {
        var s = new HotkeyState();
        for (int i = 0; i < 5; i++) Assert.Equal(HotkeyAction.None, Press(s, key, repeat: true));
        Assert.False(s.Granny);
        Assert.True(s.Noise);
        Assert.Equal(0, s.StreakCount);
    }

    [Theory]
    [InlineData(SDL.Keycode.Up)]
    [InlineData(SDL.Keycode.Down)]
    [InlineData(SDL.Keycode.Left)]
    [InlineData(SDL.Keycode.Right)]
    [InlineData(SDL.Keycode.LShift)]
    [InlineData(SDL.Keycode.RShift)]
    [InlineData(SDL.Keycode.LCtrl)]
    [InlineData(SDL.Keycode.RCtrl)]
    [InlineData(SDL.Keycode.LAlt)]
    [InlineData(SDL.Keycode.RAlt)]
    public void ArrowsAndModifiersNeverTriggerAndNeverBreakAStreak(SDL.Keycode key)
    {
        var s = new HotkeyState();
        Assert.Equal(HotkeyAction.None, Press(s, key));
        Assert.Equal(HotkeyAction.None, Press(s, SDL.Keycode.F7));
        Assert.Equal(HotkeyAction.None, Press(s, key));
        Assert.Equal(HotkeyAction.None, Press(s, SDL.Keycode.F7));
        Assert.Equal(HotkeyAction.None, Press(s, key));
        Assert.Equal(HotkeyAction.Cheat, Press(s, SDL.Keycode.F7)); // streak survived the neutral keys
    }

    // ---- F7 x3 cheat streak -----------------------------------------------------------------

    [Fact]
    public void ThirdF7IsTheCheatThenTheStreakRestarts()
    {
        var s = new HotkeyState();
        var seq = new[] { 1, 2, 3, 4, 5, 6 }.Select(_ => Press(s, SDL.Keycode.F7)).ToArray();
        Assert.Equal(new[]
        {
            HotkeyAction.None, HotkeyAction.None, HotkeyAction.Cheat,
            HotkeyAction.None, HotkeyAction.None, HotkeyAction.Cheat, // cheatchar=0 after a cheat: count restarts at 1
        }, seq);
    }

    [Theory]
    [InlineData(SDL.Keycode.A)]
    [InlineData(SDL.Keycode.Space)]
    [InlineData(SDL.Keycode.F1)]
    [InlineData(SDL.Keycode.Z)]
    [InlineData(SDL.Keycode.Return)]
    public void AnyOtherKeyResetsTheF7Streak(SDL.Keycode other)
    {
        var s = new HotkeyState();
        Press(s, SDL.Keycode.F7);
        Press(s, SDL.Keycode.F7);
        Press(s, other);
        Assert.Equal(HotkeyAction.None, Press(s, SDL.Keycode.F7)); // streak is 1 again, not 3
        Assert.Equal(HotkeyAction.None, Press(s, SDL.Keycode.F7));
        Assert.Equal(HotkeyAction.Cheat, Press(s, SDL.Keycode.F7));
    }

    [Fact]
    public void RepeatEventsDoNotAdvanceTheF7Streak()
    {
        var s = new HotkeyState();
        Press(s, SDL.Keycode.F7);
        for (int i = 0; i < 6; i++) Press(s, SDL.Keycode.F7, repeat: true);
        Assert.Equal(HotkeyAction.None, Press(s, SDL.Keycode.F7));
        Assert.Equal(HotkeyAction.Cheat, Press(s, SDL.Keycode.F7));
    }

    [Fact]
    public void RepeatEventsDoNotBreakTheStreakEither()
    {
        var s = new HotkeyState();
        Press(s, SDL.Keycode.F7);
        Press(s, SDL.Keycode.A, repeat: true); // a typematic repeat of some other held key
        Press(s, SDL.Keycode.F7);
        Assert.Equal(HotkeyAction.Cheat, Press(s, SDL.Keycode.F7));
    }

    // ---- Z x3 debug -------------------------------------------------------------------------

    [Fact]
    public void ThirdZTogglesDebugThenRestarts()
    {
        var s = new HotkeyState();
        Assert.False(s.Debug);
        Assert.Equal(HotkeyAction.None, Press(s, SDL.Keycode.Z));
        Assert.Equal(HotkeyAction.None, Press(s, K('Z'))); // case-insensitive
        Assert.Equal(HotkeyAction.ToggleDebug, Press(s, SDL.Keycode.Z));
        Assert.True(s.Debug);
        Assert.Equal(HotkeyAction.None, Press(s, SDL.Keycode.Z));
        Assert.Equal(HotkeyAction.None, Press(s, SDL.Keycode.Z));
        Assert.Equal(HotkeyAction.ToggleDebug, Press(s, SDL.Keycode.Z));
        Assert.False(s.Debug);
    }

    [Fact]
    public void ZStreakIsResetByAnyOtherKeyAndIgnoresRepeats()
    {
        var s = new HotkeyState();
        Press(s, SDL.Keycode.Z);
        Press(s, SDL.Keycode.Z);
        Press(s, SDL.Keycode.F7);
        Assert.Equal(HotkeyAction.None, Press(s, SDL.Keycode.Z));
        Press(s, SDL.Keycode.Z, repeat: true);
        Press(s, SDL.Keycode.Z, repeat: true);
        Assert.Equal(HotkeyAction.None, Press(s, SDL.Keycode.Z));
        Assert.Equal(HotkeyAction.ToggleDebug, Press(s, SDL.Keycode.Z));
        Assert.True(s.Debug);
    }

    [Fact]
    public void MixedF7AndZNeverTriggers()
    {
        var s = new HotkeyState();
        foreach (var k in new[] { SDL.Keycode.F7, SDL.Keycode.Z, SDL.Keycode.F7, SDL.Keycode.Z, SDL.Keycode.F7, SDL.Keycode.Z })
            Assert.Equal(HotkeyAction.None, Press(s, k));
        Assert.False(s.Debug);
    }

    [Fact]
    public void HotkeysThemselvesCountTowardTheStreakLikeAnyKey()
    {
        // A hotkey press is a keystroke like any other: it breaks an F7 streak.
        var s = new HotkeyState();
        Press(s, SDL.Keycode.F7);
        Press(s, SDL.Keycode.F7);
        Assert.Equal(HotkeyAction.Save, Press(s, SDL.Keycode.S));
        Assert.Equal(HotkeyAction.None, Press(s, SDL.Keycode.F7));
    }

    // ---- granny / noise / debug state -------------------------------------------------------

    [Fact]
    public void GrannyAndNoiseTogglesFlipStoredState()
    {
        var s = new HotkeyState();
        Assert.False(s.Granny);
        Assert.True(s.Noise); // soundf=1 at start
        Assert.Equal(1, s.TicksPerFrame);
        Press(s, SDL.Keycode.G);
        Assert.True(s.Granny);
        Assert.Equal(2, s.TicksPerFrame); // pace becomes granny+1 ticks
        Press(s, K('G'));
        Assert.False(s.Granny);
        Assert.Equal(1, s.TicksPerFrame);
        Press(s, SDL.Keycode.N);
        Assert.False(s.Noise);
        Press(s, SDL.Keycode.N);
        Assert.True(s.Noise);
        Assert.False(s.Granny);
    }

    [Fact]
    public void GrannyKeyHeldDoesNotFlickerBetweenStates()
    {
        var s = new HotkeyState();
        Press(s, SDL.Keycode.G);
        for (int i = 0; i < 10; i++) Press(s, SDL.Keycode.G, repeat: true);
        Assert.True(s.Granny);
    }

    // ---- cheat effects (spec 9) -------------------------------------------------------------

    private static ObjectManager NewObjects(PlayerState pl)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        return new LevelSession(n => set.LoadBoard(n.ToUpperInvariant()), new ShapeManager(set.Graphics), set.Info,
            screen, new Viewport(0, 2, VgaScreen.Width, 158), pl).Load("MAP").Objects;
    }

    [Fact]
    public void CheatGivesFullHealthAllFourKeysAndInvincibility()
    {
        var pl = PlayerState.NewGame();
        var o = NewObjects(pl);
        pl.Health = 1;
        Hotkeys.ApplyCheat(o);
        Assert.Equal(5, pl.Health);
        for (int k = 0; k < 4; k++) Assert.Equal(1, o.InvCount(Inv.Key(k)));
        Assert.Equal(1, o.InvCount(Inv.Invin));
    }

    [Fact]
    public void CheatOnlyAddsMissingItemsAndLeavesTheRestUntouched()
    {
        var pl = PlayerState.NewGame();
        var o = NewObjects(pl);
        pl.Health = 2;
        pl.Score = 1234;
        pl.Fruit = 7;
        o.AddInv(Inv.Key1);          // already holds key 1 -> must not be duplicated
        int lasers = o.InvCount(Inv.Laser); // NewGame already carries the starting laser
        o.AddInv(Inv.Fire);
        o.AddInv(Inv.Fire);
        o.AddInv(Inv.Invin);         // already invincible -> not added again
        int emeralds = pl.Emeralds;
        Hotkeys.ApplyCheat(o);
        Assert.Equal(5, pl.Health);
        Assert.Equal(1, o.InvCount(Inv.Key0));
        Assert.Equal(1, o.InvCount(Inv.Key1));
        Assert.Equal(1, o.InvCount(Inv.Key2));
        Assert.Equal(1, o.InvCount(Inv.Key3));
        Assert.Equal(1, o.InvCount(Inv.Invin));
        Assert.Equal(lasers, o.InvCount(Inv.Laser));
        Assert.Equal(2, o.InvCount(Inv.Fire));
        Assert.Equal(1234u, pl.Score);
        Assert.Equal(7, pl.Fruit);
        Assert.Equal(emeralds, pl.Emeralds);
    }

    [Fact]
    public void CheatTwiceIsIdempotent()
    {
        var pl = PlayerState.NewGame();
        var o = NewObjects(pl);
        Hotkeys.ApplyCheat(o);
        int n = pl.NumInv;
        Hotkeys.ApplyCheat(o);
        Assert.Equal(n, pl.NumInv);
    }

    // ---- game menu key map and shared constants ---------------------------------------------

    [Fact]
    public void GameMenuKeyMapMatchesTheC()
    {
        Assert.Equal("RQSLIABGN", GameMenuDefs.GameKeys);
        Assert.Equal(GameMenuAction.Return, GameMenuDefs.Map('R'));
        Assert.Equal(GameMenuAction.Quit, GameMenuDefs.Map('Q'));
        Assert.Equal(GameMenuAction.Save, GameMenuDefs.Map('S'));
        Assert.Equal(GameMenuAction.Load, GameMenuDefs.Map('L'));
        Assert.Equal(GameMenuAction.Instructions, GameMenuDefs.Map('I'));
        Assert.Equal(GameMenuAction.Inventory, GameMenuDefs.Map('A'));
        Assert.Equal(GameMenuAction.Buy, GameMenuDefs.Map('B'));
        Assert.Equal(GameMenuAction.Granny, GameMenuDefs.Map('G'));
        Assert.Equal(GameMenuAction.Noise, GameMenuDefs.Map('N'));
        Assert.Equal(GameMenuAction.None, GameMenuDefs.Map('X'));
        Assert.Equal(GameMenuAction.None, GameMenuDefs.Map('\0'));
        // every key in the keytab is mapped (so every menu item does something)
        foreach (char c in GameMenuDefs.GameKeys) Assert.NotEqual(GameMenuAction.None, GameMenuDefs.Map(c));
    }

    [Fact]
    public void SharedMenuConstantsAreTheCStrings()
    {
        Assert.Equal(
            "7    - GAME OPTIONS -    \r3RETURN TO GAME\r2QUIT GAME\r2SAVE GAME\r2LOAD GAME\r2INSTRUCTIONS\r2ADDITIONAL INVENTORY\r2BUY EXTRA ITEMS\r2GRANNY MODE is    \r2NOISE is    \r\r",
            GameMenuDefs.GameText);
        Assert.Equal(
            "7  What would you like to buy?\r3RETURN TO GAME\r2A. HEALTH UNIT 15 EMERALDS\r2B. EXTRA LASER 10 EMERALDS\r2C. RAPID FIRE  25 EMERALDS\r2D. 1 FIREBALL   5 EMERALDS\r2E. 5 FIREBALLS 20 EMERALDS\r2F. INVINCIBLE  30 EMERALDS\r\r",
            GameMenuDefs.BuyText);
        Assert.Equal("RABCDEF", GameMenuDefs.BuyKeys);
    }

    [Fact]
    public void GameMenuGeometryIsTheCs()
    {
        var m = GameMenuDefs.CreateGameMenu();
        Assert.Equal(9, m.ItemCount);
        Assert.Equal((72, 24, 176, 112), (m.Border.X, m.Border.Y, m.Border.W, m.Border.H));
        var b = GameMenuDefs.CreateBuyMenu();
        Assert.Equal(7, b.ItemCount);
        Assert.Equal((56, 32, 208, 96), (b.Border.X, b.Border.Y, b.Border.W, b.Border.H));
    }

    [Fact]
    public void EscResolvesToReturnInTheGameMenuAndF1ToInstructions()
    {
        Assert.Equal('R', Menu.ResolveKey(Menu.KeyEsc, GameMenuDefs.GameKeys, 4, 'R'));
        Assert.Equal('I', Menu.ResolveKey(Menu.KeyF1, GameMenuDefs.GameKeys, 0, 'R'));
        Assert.Equal('Q', Menu.ResolveKey('\r', GameMenuDefs.GameKeys, 1, 'R')); // highlighted QUIT
    }

    [Fact]
    public void F1IsIgnoredWhenTheKeytabHasNoI()
    {
        // The buy menu's keytab is RABCDEF: F1 -> 'I' is not in it, so nothing is selected.
        Assert.Equal('\0', Menu.ResolveKey(Menu.KeyF1, GameMenuDefs.BuyKeys, 0, 'R'));
    }

    // ---- askquit ----------------------------------------------------------------------------

    [Fact]
    public void AskQuitYesOnlyOnY()
    {
        Assert.Equal(AskQuitAnswer.Yes, AskQuit.Answer(SDL.Keycode.Y));
        Assert.Equal(AskQuitAnswer.Yes, AskQuit.Answer(K('Y')));
    }

    [Theory]
    [InlineData(SDL.Keycode.N)]
    [InlineData(SDL.Keycode.Escape)]
    [InlineData(SDL.Keycode.Return)]
    [InlineData(SDL.Keycode.Space)]
    [InlineData(SDL.Keycode.A)]
    [InlineData(SDL.Keycode.Up)]
    [InlineData(SDL.Keycode.LShift)]
    [InlineData(SDL.Keycode.LCtrl)]
    public void AskQuitAnyOtherKeyFireOrDirectionMeansNo(SDL.Keycode k) =>
        Assert.Equal(AskQuitAnswer.No, AskQuit.Answer(k));

    [Theory]
    [InlineData(SDL.Keycode.Capslock)]
    [InlineData(SDL.Keycode.NumLockClear)]
    [InlineData(SDL.Keycode.Unknown)]
    public void AskQuitIgnoresKeysThatProduceNoKeystroke(SDL.Keycode k) =>
        Assert.Equal(AskQuitAnswer.Pending, AskQuit.Answer(k));

    [Fact]
    public void AskQuitFrameIsTheCsWindow()
    {
        var w = AskQuit.Frame();
        Assert.Equal((80, 64, 160, 32), (w.Border.X, w.Border.Y, w.Border.W, w.Border.H));
        Assert.Equal((88, 72, 144, 16), (w.Inside.X, w.Inside.Y, w.Inside.W, w.Inside.H));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AskQuitShapeExistsInEveryEpisode(int ep)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), ep);
        var shapes = new ShapeManager(set.Graphics);
        Assert.NotNull(shapes.GetShape(0x21, 0x0b));
    }

    [Fact]
    public void AskQuitRenderDrawsTheWindowAndShape()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        screen.Clear(0);
        var shapes = new ShapeManager(set.Graphics);
        var text = new TextRenderer(shapes);
        AskQuit.Render(screen, shapes, text);
        int lit = 0;
        for (int y = 72; y < 88; y++)
            for (int x = 88; x < 232; x++) if (screen.Pixels[y * VgaScreen.Width + x] != 0) lit++;
        Assert.True(lit > 200);
        Assert.Equal(0, screen.Pixels[10 * VgaScreen.Width + 10]); // nothing outside the window
    }

    // ---- menu input filtering (repeat, Left/Right) ------------------------------------------

    [Theory]
    [InlineData(SDL.Keycode.Q)]
    [InlineData(SDL.Keycode.R)]
    [InlineData(SDL.Keycode.Return)]
    [InlineData(SDL.Keycode.Space)]
    [InlineData(SDL.Keycode.Escape)]
    [InlineData(SDL.Keycode.F1)]
    public void MenuSelectionKeysIgnoreRepeats(SDL.Keycode k)
    {
        Assert.NotEqual('\0', Menu.Translate(k, repeat: false).Key);
        Assert.Equal(new Menu.MenuKey(0, '\0'), Menu.Translate(k, repeat: true));
    }

    [Fact]
    public void MenuTranslateMapsKeys()
    {
        Assert.Equal('Q', Menu.Translate(SDL.Keycode.Q, false).Key);
        Assert.Equal('Q', Menu.Translate(K('Q'), false).Key);
        Assert.Equal('\r', Menu.Translate(SDL.Keycode.Return, false).Key);
        Assert.Equal(' ', Menu.Translate(SDL.Keycode.Space, false).Key);
        Assert.Equal(Menu.KeyEsc, Menu.Translate(SDL.Keycode.Escape, false).Key);
        Assert.Equal(Menu.KeyF1, Menu.Translate(SDL.Keycode.F1, false).Key);
        Assert.Equal('\0', Menu.Translate(SDL.Keycode.Alpha1, false).Key);
    }

    [Fact]
    public void MenuArrowsMoveIncludingLeftRightAndWhileHeld()
    {
        // domenu: cur += dx1+dy1 -- Left/Up = -1, Right/Down = +1; a held arrow keeps moving (repeat counts).
        foreach (bool rep in new[] { false, true })
        {
            Assert.Equal(new Menu.MenuKey(-1, '\0'), Menu.Translate(SDL.Keycode.Up, rep));
            Assert.Equal(new Menu.MenuKey(-1, '\0'), Menu.Translate(SDL.Keycode.Left, rep));
            Assert.Equal(new Menu.MenuKey(+1, '\0'), Menu.Translate(SDL.Keycode.Down, rep));
            Assert.Equal(new Menu.MenuKey(+1, '\0'), Menu.Translate(SDL.Keycode.Right, rep));
        }
    }

    // ---- cursor animation -------------------------------------------------------------------

    [Fact]
    public void CursorGlyphAdvancesEveryTwoTicksOverSixGlyphs()
    {
        // C: count wraps at 12, shape 0x0201 + count/2 -> glyphs 1..6, each held for two ticks.
        var seen = Enumerable.Range(0, 24).Select(Menu.CursorGlyph).ToArray();
        Assert.Equal(new[] { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6 }, seen);
    }

    [Fact]
    public void MenuDrawUsesTheTwoTickCursorCycle()
    {
        byte[] Frame(int tick)
        {
            var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
            var screen = new VgaScreen();
            set.LoadPalette(screen);
            var shapes = new ShapeManager(set.Graphics);
            GameMenuDefs.CreateGameMenu().Draw(screen, new TextRenderer(shapes), shapes, 0, tick);
            return (byte[])screen.Pixels.Clone();
        }
        Assert.Equal(Frame(0), Frame(1));      // same glyph for ticks 0 and 1
        Assert.NotEqual(Frame(1), Frame(2));   // next glyph at tick 2
        Assert.Equal(Frame(0), Frame(12));     // period 12 ticks
    }

    // ---- GameWon is polled before the world is swapped --------------------------------------

    private static ObjectManager Objs() => NewObjects(PlayerState.NewGame());

    [Fact]
    public void GameWonIsPolledBeforeATransitionSwapsTheWorld()
    {
        var o = Objs();
        o.GameWon = true;
        o.NewLevel = "BOARD_1";
        int transitions = 0;
        var over = GameFlow.PostUpdate(o, died: false, die: () => Assert.Fail("no death"), transition: () => transitions++);
        Assert.Equal(GameOver.Won, over);
        Assert.Equal(0, transitions); // gameover ends the loop before newlevel is processed
    }

    [Fact]
    public void ATransitionRunsWhenNothingEndedTheGame()
    {
        var o = Objs();
        o.NewLevel = "BOARD_1";
        int transitions = 0;
        Assert.Equal(GameOver.None, GameFlow.PostUpdate(o, false, () => Assert.Fail("no death"), () => transitions++));
        Assert.Equal(1, transitions);
        // no pending level: nothing happens
        Assert.Equal(GameOver.None, GameFlow.PostUpdate(Objs(), false, () => Assert.Fail("no death"), () => Assert.Fail("none")));
    }

    [Fact]
    public void DeathIsHandledAfterThePollToo()
    {
        var o = Objs();
        int deaths = 0;
        Assert.Equal(GameOver.None, GameFlow.PostUpdate(o, died: true, die: () => deaths++, transition: () => Assert.Fail("x")));
        Assert.Equal(1, deaths);
        var won = Objs();
        won.GameWon = true;
        Assert.Equal(GameOver.Won, GameFlow.PostUpdate(won, died: true, die: () => Assert.Fail("x"), transition: () => Assert.Fail("x")));
    }
}
