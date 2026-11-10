using Xunit;
using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Ui;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>cnt_fruit / icon_fruit: msg_bonus pickup (X_OBJ.C:1090-1145), the 16-fruit heart rule inside
/// drawstats (XARGON.C:197), stats() persistence (XARGON.C:451-458) and the HUD icon + count (XARGON.C:229-233).</summary>
public class FruitTests
{
    private static ObjectManager Touch(ObjectManager w, int state)
    {
        int n = w.AddObj(ObjectKind.Bonus, 100, 100, 0, 0);
        w.Obj(n).State = state;
        w.Dispatch(n, Msg.Touch, 0);
        return w;
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(6, 1)]
    [InlineData(8, 2)]
    [InlineData(14, 3)]
    public void FruitPickupSetsIconAndIncrements(int state, int icon)
    {
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.NumInv = 0;
        w.Pl.Fruit = 3;
        Touch(w, state);
        Assert.Equal(4, w.Pl.Fruit);
        Assert.Equal(icon, w.Pl.FruitIcon);
    }

    [Fact]
    public void FruitCountCapsAtSixteen()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.Fruit = 15;
        Touch(w, 2);
        Assert.Equal(16, w.Pl.Fruit);
        Touch(w, 8);
        Assert.Equal(16, w.Pl.Fruit);
        Assert.Equal(2, w.Pl.FruitIcon); // the icon still changes at the cap
    }

    [Fact]
    public void NonFruitBonusLeavesFruitAlone()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.Fruit = 5; w.Pl.FruitIcon = 1;
        Touch(w, 3);
        Assert.Equal(5, w.Pl.Fruit);
        Assert.Equal(1, w.Pl.FruitIcon);
    }

    [Fact]
    public void SixteenthFruitGrantsOneHeartAndResetsCounter()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.Health = 3; w.Pl.Fruit = 15;
        Touch(w, 6);
        Assert.Equal(16, w.Pl.Fruit);
        Assert.Equal(3, w.Pl.Health); // the rule lives in drawstats, not at pickup
        w.UpdateFruitHeart();
        Assert.Equal(4, w.Pl.Health);
        Assert.Equal(0, w.Pl.Fruit);
        w.UpdateFruitHeart(); // repeated refreshes do nothing more
        Assert.Equal(4, w.Pl.Health);
        Assert.Equal(0, w.Pl.Fruit);
    }

    [Fact]
    public void HeartGrantFlashesHeroBlueUnlessInvincible()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.Health = 2; w.Pl.Fruit = 16; w.Pl.Ouched = 0;
        w.UpdateFruitHeart();
        Assert.Equal(-4, w.Pl.Ouched);

        w.Pl.Health = 2; w.Pl.Fruit = 16; w.Pl.Ouched = 0;
        w.AddInv(Inv.Invin);
        w.UpdateFruitHeart();
        Assert.Equal(0, w.Pl.Ouched);
        Assert.Equal(3, w.Pl.Health);
    }

    [Fact]
    public void AtFullHealthCounterStaysSixteenUntilDamage()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.Health = 5; w.Pl.Fruit = 16;
        w.UpdateFruitHeart();
        Assert.Equal(5, w.Pl.Health);
        Assert.Equal(16, w.Pl.Fruit);

        w.Pl.Health = 4; // took damage
        w.UpdateFruitHeart();
        Assert.Equal(5, w.Pl.Health);
        Assert.Equal(0, w.Pl.Fruit);
    }

    [Fact]
    public void BelowSixteenNothingHappens()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.Health = 1; w.Pl.Fruit = 15;
        w.UpdateFruitHeart();
        Assert.Equal(1, w.Pl.Health);
        Assert.Equal(15, w.Pl.Fruit);
    }

    // ---- PlayerState plumbing -------------------------------------------------------------

    [Fact]
    public void NewGameZeroesFruit()
    {
        var p = PlayerState.NewGame();
        Assert.Equal(0, p.Fruit);
        Assert.Equal(0, p.FruitIcon);
    }

    [Fact]
    public void CloneCarriesFruitFields()
    {
        var p = new PlayerState { Fruit = 9, FruitIcon = 3 };
        var c = p.Clone();
        Assert.Equal(9, c.Fruit);
        Assert.Equal(0, c.FruitIcon); // icon_fruit is not in pltype / stats()
    }

    [Fact]
    public void CopyFromCarriesFruitFields()
    {
        var src = new PlayerState { Fruit = 11, FruitIcon = 2 };
        var dst = new PlayerState { FruitIcon = 1 };
        dst.CopyFrom(src);
        Assert.Equal(11, dst.Fruit);
        Assert.Equal(1, dst.FruitIcon); // icon_fruit is not in pltype / stats()
    }

    // ---- LevelSession stats(1)/stats(0) ---------------------------------------------------

    private static (ShapeManager shapes, InfoTable info, VgaScreen screen) Rig()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        return (new ShapeManager(set.Graphics), set.Info, screen);
    }

    private static Board Loader(string name)
    {
        var b = new Board();
        if (name == "MAP")
        {
            b.Objects.Add(new Obj { Kind = (byte)ObjectKind.Tiny, X = 320, Y = 320 });
            b.Objects.Add(new Obj { Kind = (byte)ObjectKind.Checkpt, Counter = 1, State = 0, X = 240, Y = 160, Inside = "level" });
        }
        else
        {
            b.Objects.Add(new Obj { Kind = (byte)ObjectKind.Player, X = 100, Y = 100 });
            b.Objects.Add(new Obj { Kind = (byte)ObjectKind.Checkpt, Counter = 1, State = 1, X = 64, Y = 160, Inside = "*song" });
        }
        b.FruitCount = 0; // the level file's own cnt_fruit; stats(0) overrides it
        return b;
    }

    private static LevelHarness Harness(PlayerState pl)
    {
        var (shapes, info, screen) = Rig();
        return new LevelHarness(Loader, shapes, info, screen, pl, "MAP");
    }

    [Fact]
    public void FruitSurvivesEnteringALevelAndReturningToTheMap()
    {
        var pl = new PlayerState { Health = 5, Level = LevelSession.MapLevel, Fruit = 7 };
        var h = Harness(pl);
        pl.Level = 1;
        h.Enter("level");
        Assert.Equal(7, h.Pl.Fruit);       // stats(1) then stats(0): carried into the level
        h.Pl.Fruit = 12;                   // picked up inside the level
        h.Objects.NewLevel = "!";
        h.Step();                          // exit: stats(1) takes the live 12, stats(0) restores it
        Assert.Equal(12, h.Pl.Fruit);
    }

    [Fact]
    public void DeathWithReloadRestoresFruitToTheLevelEntrySnapshot()
    {
        var pl = new PlayerState { Health = 5, Level = LevelSession.MapLevel, Fruit = 7 };
        var h = Harness(pl);
        pl.Level = 1;
        h.Enter("level");
        h.Pl.Fruit = 14;                   // picked up after entry
        h.Objects.Obj(0).State = 0;
        h.Objects.POuch(10, 0);
        for (int i = 0; i < 30 && h.HeroState == 5; i++) h.Step();
        Assert.Equal(7, h.Pl.Fruit);       // stats(0): old_fruit from level entry
    }

    [Fact]
    public void IconIsNotPartOfTheStatsSnapshot()
    {
        var pl = new PlayerState { Health = 5, Level = LevelSession.MapLevel, Fruit = 7, FruitIcon = 1 };
        var h = Harness(pl);
        pl.Level = 1;
        h.Enter("level");
        h.Pl.FruitIcon = 3;                // icon_fruit is a plain global; stats() never saves it
        h.Objects.Obj(0).State = 0;
        h.Objects.POuch(10, 0);
        for (int i = 0; i < 30 && h.HeroState == 5; i++) h.Step();
        Assert.Equal(3, h.Pl.FruitIcon);
    }

    // ---- HUD ------------------------------------------------------------------------------

    private static (VgaScreen screen, ShapeManager shapes, TextRenderer text) HudRig()
    {
        var (shapes, _, screen) = Rig();
        return (screen, shapes, new TextRenderer(shapes));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 2)]
    [InlineData(16, 3)]
    public void HudDrawsFruitIconAndCountAsDrawstats(int count, int icon)
    {
        var (screen, shapes, text) = HudRig();
        var statvp = new Viewport(0, 160, 320, 28);
        var pl = new PlayerState { Health = 5, Fruit = count, FruitIcon = icon };
        Hud.Draw(screen, shapes, text, pl, new StatusLine(), 0);

        // Expected: the same HUD with an empty fruit slot (icon 0 / count 0), then the C's direct draws.
        var (exp, _, _) = HudRig();
        Hud.Draw(exp, shapes, text, new PlayerState { Health = 5 }, new StatusLine(), 0);
        shapes.DrawShape(exp, statvp, 0x1e4c, 170, 13);
        shapes.DrawShape(exp, statvp, 0x1e48 + icon, 170, 13);
        text.Draw(exp, statvp, 185, 15, TextRenderer.Font6, 3, 121, "  ");
        string s = count.ToString();
        text.Draw(exp, statvp, 197 - (1 + s.Length * 6), 15, TextRenderer.Font6, 3, 121, s);
        Assert.Equal(exp.Pixels, screen.Pixels);
    }

    [Fact]
    public void HudFruitCountUsesColour3AndIconChangesWithKind()
    {
        var (screen, shapes, text) = HudRig();
        Hud.Draw(screen, shapes, text, new PlayerState { Health = 5, Fruit = 8, FruitIcon = 0 }, new StatusLine(), 0);
        int digits = 0;
        for (int y = 175; y < 183; y++)
            for (int x = 185; x < 198; x++)
                if (screen.Pixels[y * 320 + x] == 3) digits++;
        Assert.True(digits > 0, "no colour-3 count pixels");

        var icon0 = Region(screen, 170, 173, 14, 12);
        var (s2, _, _) = HudRig();
        Hud.Draw(s2, shapes, text, new PlayerState { Health = 5, Fruit = 8, FruitIcon = 3 }, new StatusLine(), 0);
        Assert.NotEqual(icon0, Region(s2, 170, 173, 14, 12));
    }

    private static byte[] Region(VgaScreen s, int x0, int y0, int w, int h)
    {
        var r = new byte[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) r[y * w + x] = s.Pixels[(y0 + y) * 320 + x0 + x];
        return r;
    }
}
