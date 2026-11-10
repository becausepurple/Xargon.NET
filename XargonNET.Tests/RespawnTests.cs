using Xunit;
using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>
/// Task 9: death, respawn and level-transition state (LevelSession / p_reenter / stats / saveboard).
/// Synthetic boards keep the scenarios deterministic; a real-asset integration test drives the whole
/// MAP -> level -> die -> respawn -> exit path.
/// </summary>
public class RespawnTests
{
    private const int StStand = 0, StBegin = 4, StDie = 5, StPlatform = 7;

    // ---- Rig / synthetic boards -----------------------------------------------------------

    private static (ShapeManager shapes, InfoTable info, VgaScreen screen) Rig()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        return (new ShapeManager(set.Graphics), set.Info, screen);
    }

    private static Board SynthMap()
    {
        var b = new Board();
        b.Objects.Add(new Obj { Kind = (byte)ObjectKind.Tiny, X = 320, Y = 320 });      // map hero (obj 0)
        b.Objects.Add(new Obj { Kind = (byte)ObjectKind.Checkpt, Counter = 1, State = 0, X = 240, Y = 160, Inside = "level" });
        b.Player = new PlayerState { Health = 5, Level = LevelSession.MapLevel };
        return b;
    }

    private static Board SynthLevel()
    {
        var b = new Board();
        b.Objects.Add(new Obj { Kind = (byte)ObjectKind.Player, X = 100, Y = 100 });     // hero (obj 0)
        b.Objects.Add(new Obj { Kind = (byte)ObjectKind.Checkpt, Counter = 1, State = 1, X = 64, Y = 160, Inside = "*song" });
        b.Objects.Add(new Obj { Kind = (byte)ObjectKind.Platform, X = 200, Y = 200 });
        b.Objects.Add(new Obj { Kind = (byte)ObjectKind.Grunt, X = 300, Y = 100 });
        b.Player = new PlayerState { Health = 5, Level = 1 };
        return b;
    }

    private static Board Loader(string name) => name == "MAP" ? SynthMap() : SynthLevel();

    private static LevelHarness Harness(PlayerState pl, string start = "MAP")
    {
        var (shapes, info, screen) = Rig();
        return new LevelHarness(Loader, shapes, info, screen, pl, start);
    }

    [Fact]
    public void ExitToMapResetsSkyAndHeroColours()
    {
        var pl = new PlayerState { Health = 5, Level = LevelSession.MapLevel };
        var h = Harness(pl, "MAP");
        pl.Level = 1;
        h.Enter("level");
        var scr = h.Objects.Screen;
        scr.SetColor6(250, 12, 23, 63);            // the level's blue sky
        scr.SetColor6(251, 12, 23, 63);
        scr.SetColor6(207, 63, 0, 0);               // leftover hero flash
        scr.SetColor6(165, 1, 2, 3);                // laser colour
        h.ExitToMap();
        var expect = new VgaScreen();
        expect.SetColor6(0, 0, 0, 0);
        Assert.Equal(expect.Palette[0], scr.Palette[250]);
        Assert.Equal(expect.Palette[0], scr.Palette[251]);
        foreach (int c in new[] { 207, 220, 233, 162, 165, 167 })
        {
            expect.SetColor6(0, scr.Dac[c * 3], scr.Dac[c * 3 + 1], scr.Dac[c * 3 + 2]);
            Assert.Equal(expect.Palette[0], scr.Palette[c]);
        }
    }

    [Fact]
    public void DeferredPaletteKeepsOldColoursUntilApplied()
    {
        var (shapes, info, screen) = Rig();
        Board Skyed(string n)
        {
            var b = n == "MAP" ? SynthMap() : SynthLevel();
            // level: dk. blue sky (0,0,32); map: no effects object
            if (n != "MAP") b.Objects.Add(new Obj { Kind = (byte)ObjectKind.Effects, State = 2, X = 8, Y = 8 });
            return b;
        }
        var pl = new PlayerState { Health = 5, Level = LevelSession.MapLevel };
        var session = new LevelSession(Skyed, shapes, info, screen, new Viewport(0, 2, VgaScreen.Width, 158), pl);
        session.Load("MAP");
        var before250 = screen.Palette[250];
        var before251 = screen.Palette[251];
        pl.Level = 1;

        session.EnterLevel("level", deferPalette: true);
        Assert.Equal(before250, screen.Palette[250]);
        Assert.Equal(before251, screen.Palette[251]);
        session.ApplyPendingPalette();
        var expect = new VgaScreen();
        expect.SetColor6(0, 0, 0, 32);
        Assert.Equal(expect.Palette[0], screen.Palette[250]);
        Assert.Equal(expect.Palette[0], screen.Palette[251]);

        uint orig207 = screen.Palette[207];
        screen.SetColor6(207, 63, 0, 0);
        uint dirty207 = screen.Palette[207];
        Assert.NotEqual(orig207, dirty207);
        session.ExitToMap(deferPalette: true);
        Assert.Equal(expect.Palette[0], screen.Palette[250]); // level sky still showing behind the banner
        Assert.Equal(dirty207, screen.Palette[207]);          // hero colour not reset until applied
        session.ApplyPendingPalette();
        Assert.Equal(orig207, screen.Palette[207]);           // reset on apply
        expect.SetColor6(0, 0, 0, 0);
        Assert.Equal(expect.Palette[0], screen.Palette[250]);
        Assert.Equal(expect.Palette[0], screen.Palette[251]);
    }

    [Fact]
    public void NonDeferredBuildDropsStalePendingPalette()
    {
        var (shapes, info, screen) = Rig();
        Board Skyed(string n)
        {
            var b = n == "MAP" ? SynthMap() : SynthLevel();
            if (n != "MAP") b.Objects.Add(new Obj { Kind = (byte)ObjectKind.Effects, State = 2, X = 8, Y = 8 });
            return b;
        }
        var pl = new PlayerState { Health = 5, Level = LevelSession.MapLevel };
        var session = new LevelSession(Skyed, shapes, info, screen, new Viewport(0, 2, VgaScreen.Width, 158), pl);
        session.Load("MAP");
        pl.Level = 1;
        session.EnterLevel("level", deferPalette: true); // queues the level's dk. blue sky
        session.ExitToMap();                              // non-deferred build: map palette (black sky) applied now
        var after250 = screen.Palette[250];
        var after251 = screen.Palette[251];
        session.ApplyPendingPalette();                    // must not apply the stale level sky
        Assert.Equal(after250, screen.Palette[250]);
        Assert.Equal(after251, screen.Palette[251]);
    }

    // ---- Review Focus 1: dying off a platform releases the platform and ends in st_stand -----

    [Fact]
    public void DyingOnPlatformRespawnsStandingWithPlatformReleased()
    {
        var pl = new PlayerState { Health = 5, Level = LevelSession.MapLevel };
        var h = Harness(pl, "MAP");
        pl.Level = 1;
        h.Enter("level"); // reach the level from the map, so death reloads it (p_reenter(1))

        // Mount the hero on a platform: hero in st_platform, platform carrying (substate 7).
        int plat = IndexOfKind(h.Objects, ObjectKind.Platform);
        h.Objects.Obj(plat).SubState = StPlatform;
        h.Objects.Obj(0).State = StPlatform;

        h.Objects.POuch(10, 0); // lethal hit while riding
        Assert.Equal(StDie, h.HeroState);
        Assert.Equal(18, h.Objects.Obj(0).StateCount); // platform death shortens the throe

        int reached = h.StepUntilState(StStand);
        Assert.True(reached >= 0, "hero returns to st_stand after respawn");
        Assert.Equal(StStand, h.HeroState);

        // The board reloaded on death, so no live object is still flagged as carrying the hero.
        foreach (var o in h.Objects.Objects)
            Assert.NotEqual(StPlatform, o.SubState);
    }

    // ---- Review Focus 2: exit to map persists map edits + retires the level's checkpoint -----

    [Fact]
    public void ExitToMapRestoresMapKillsCompletedCheckpointAndKeepsOpenGate()
    {
        var pl = new PlayerState { Health = 5, Level = LevelSession.MapLevel };
        var h = Harness(pl, "MAP");

        // Open a map gate cell before entering the level, then enter level 1.
        const int gx = 5, gy = 7, openVal = 1234;
        h.Board.SetCell(gx, gy, openVal);
        pl.Level = 1; // the map checkpoint would have set this
        h.Enter("level");
        Assert.Equal(ObjectKind.Player, (int)h.Board.Objects[0].Kind);

        h.ExitToMap();

        Assert.Equal(ObjectKind.Tiny, (int)h.Board.Objects[0].Kind);
        Assert.Equal(openVal, h.Board.CellIndex(gx, gy)); // the opened gate stayed open

        int cp = IndexOfKind(h.Objects, ObjectKind.Checkpt, counter: 1, includeKilled: true);
        Assert.True(cp >= 0);
        Assert.Equal(ObjectKind.Killme, (int)h.Objects.Obj(cp).Kind); // completed level retired
    }

    [Fact]
    public void DeathRestoresEntrySnapshotStripsKeysAndRespawnsEnemies()
    {
        var pl = new PlayerState { Health = 5, Level = LevelSession.MapLevel };
        var h = Harness(pl, "MAP");

        pl.Score = 1000;
        pl.Level = 1;
        h.Enter("level"); // snapshot taken here: Score 1000, no keys

        // Progress inside the level: gain score and keys that a death should roll back / strip.
        pl.Score = 5000;
        h.Objects.AddInv(Inv.Key0);
        h.Objects.AddInv(Inv.Key2);
        Assert.Equal(1, h.Objects.InvCount(Inv.Key0));

        // Kill an enemy so we can confirm the reload respawns it.
        int grunt = IndexOfKind(h.Objects, ObjectKind.Grunt);
        h.Objects.KillObj(grunt);
        Assert.Equal(0, h.Objects.CountKind(ObjectKind.Grunt));

        // Lethal hit (leave the invincible st_begin first), then run to the respawn.
        h.Objects.Obj(0).State = StStand;
        h.Objects.POuch(10, 0);
        for (int i = 0; i < 30 && h.HeroState == StDie; i++) h.Step();

        Assert.Equal(StBegin, h.HeroState);        // reentered at the checkpoint
        Assert.Equal(5, h.Pl.Health);              // full health
        Assert.Equal(1000u, h.Pl.Score);           // entry snapshot score
        Assert.Equal(0, h.Objects.InvCount(Inv.Key0)); // keys stripped
        Assert.Equal(0, h.Objects.InvCount(Inv.Key2));
        Assert.Equal(1, h.Objects.CountKind(ObjectKind.Grunt)); // enemies respawned (board reloaded)
    }

    // ---- stats(0) item strip in isolation -------------------------------------------------

    [Fact]
    public void DeathStripsEpicExtraLasersRocksJumpInvinButKeepsOneLaser()
    {
        var pl = new PlayerState { Health = 5, Level = LevelSession.MapLevel };
        var h = Harness(pl, "MAP");

        // Hold the extras BEFORE entering, so the level-entry snapshot (o_pl) captures them; stats(0)
        // on death restores that snapshot and then strips them (keeping exactly one laser).
        h.Objects.AddInv(Inv.Laser); // two lasers held
        h.Objects.AddInv(Inv.Laser);
        h.Objects.AddInv(Inv.Epic);
        h.Objects.AddInv(Inv.Rock);
        h.Objects.AddInv(Inv.Jump);
        h.Objects.AddInv(Inv.Invin);

        pl.Level = 1;
        h.Enter("level");

        h.Objects.Obj(0).State = StStand;
        h.Objects.POuch(10, 0);
        for (int i = 0; i < 30 && h.HeroState == StDie; i++) h.Step();

        Assert.Equal(1, h.Objects.InvCount(Inv.Laser)); // exactly one laser kept
        Assert.Equal(0, h.Objects.InvCount(Inv.Epic));
        Assert.Equal(0, h.Objects.InvCount(Inv.Rock));
        Assert.Equal(0, h.Objects.InvCount(Inv.Jump));
        Assert.Equal(0, h.Objects.InvCount(Inv.Invin));
    }

    // ---- Camera.SetOrigin (setorigin) -----------------------------------------------------

    [Fact]
    public void SetOriginClampsOriginLikeTheDosSetorigin()
    {
        var vp = new Viewport(0, 2, VgaScreen.Width, 158);
        var hero = new Obj { Kind = (byte)ObjectKind.Player, X = 800, Y = 400, Xl = 16, Yl = 24 };
        Camera.SetOrigin(vp, hero);

        // vpox = (800 - 21*8) & ~7 = 632; within [16, (128-21)*16=1712]
        Assert.Equal(632, vp.OriginX);
        // vpoy = 400 + 16 - 11*8 = 328; within [0, (64+1-11)*16=864]
        Assert.Equal(328, vp.OriginY);

        var near = new Obj { Kind = (byte)ObjectKind.Player, X = 0, Y = 0, Xl = 16, Yl = 24 };
        Camera.SetOrigin(vp, near);
        Assert.Equal(16, vp.OriginX); // low clamp
        Assert.Equal(0, vp.OriginY);
    }

    // ---- Real-asset integration: MAP -> board_01 -> die -> respawn -> exit -----------------

    [Fact]
    public void HarnessPassesEpisodeToSession()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 2);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        var pl = PlayerState.NewGame();
        pl.Level = LevelSession.MapLevel;
        var h = new LevelHarness(n => set.LoadBoard(n.ToUpperInvariant()), shapes, set.Info, screen, pl, "MAP", episode: 2);
        Assert.Equal(XargonNET.Text.GameStrings.VMsg(2), h.Objects.Status.Message);
    }

    [Fact]
    public void IntegrationMapEnterLevelDieRespawnExit()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        var pl = PlayerState.NewGame();
        pl.Level = LevelSession.MapLevel;

        var h = new LevelHarness(n => set.LoadBoard(n.ToUpperInvariant()), shapes, set.Info, screen, pl, "MAP");
        Assert.Equal(ObjectKind.Tiny, (int)h.Board.Objects[0].Kind);

        // Enter level 1 (the map checkpoint for board_01 has counter 1).
        pl.Level = 1;
        h.Enter("board_01");
        Assert.Equal(ObjectKind.Player, (int)h.Board.Objects[0].Kind);
        int freshCount = h.Objects.Objects.Count;
        Assert.Equal(StBegin, h.HeroState);
        Assert.Equal(32, h.Objects.Obj(0).X);   // checkpoint 1 at x=32 (&~7)
        Assert.Equal(120, h.Objects.Obj(0).Y);  // y = 144 - 24

        // Die: board_01's checkpoint 1 is a reloading checkpoint (state==1).
        h.Objects.Obj(0).State = StStand;
        h.Objects.POuch(10, 0);
        for (int i = 0; i < 40 && h.HeroState == StDie; i++) h.Step();
        Assert.Equal(StBegin, h.HeroState);
        Assert.Equal(freshCount, h.Objects.Objects.Count); // enemies/board fully reloaded
        Assert.Equal(5, h.Pl.Health);

        // Exit back to the overworld map.
        h.Objects.NewLevel = "!";
        h.Step();
        Assert.Equal(ObjectKind.Tiny, (int)h.Board.Objects[0].Kind);

        // The board_01 icon (map checkpoint counter 1) is retired.
        int cp = IndexOfKind(h.Objects, ObjectKind.Checkpt, counter: 1, includeKilled: true);
        Assert.True(cp >= 0);
        Assert.Equal(ObjectKind.Killme, (int)h.Objects.Obj(cp).Kind);
    }

    // ---- helpers --------------------------------------------------------------------------

    private static int IndexOfKind(ObjectManager om, int kind, int counter = int.MinValue, bool includeKilled = false)
    {
        for (int i = 0; i < om.Objects.Count; i++)
        {
            var o = om.Obj(i);
            bool kindMatch = o.Kind == kind || (includeKilled && o.Kind == ObjectKind.Killme);
            if (kind == ObjectKind.Checkpt && counter != int.MinValue)
            {
                if (kindMatch && o.Counter == counter) return i;
            }
            else if (o.Kind == kind)
            {
                return i;
            }
        }
        return -1;
    }
}
