using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Ui;
using XargonNET.Video;
using Xunit;

namespace XargonNET.Tests;

/// <summary>Task 9: buymenu() purchase logic (xargon.c:923-992) and the shared IsOnMap predicate.</summary>
public class BuyMenuTests
{
    private sealed class Rec : IPopupHost
    {
        public readonly List<int> Texts = new();
        public void TextMessage(int n, PopupStyle style) { Assert.Equal(PopupStyle.Plain, style); Texts.Add(n); }
        public void DialogMessage(int n, bool eagle) { }
    }

    private static ObjectManager World(int emeralds, int health = 3)
    {
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.NumInv = 0;
        w.Pl.Emeralds = emeralds;
        w.Pl.Health = health;
        w.Pl.Ouched = 0;
        return w;
    }

    private static void Give(ObjectManager w, int inv, int n) { for (int i = 0; i < n; i++) w.AddInv(inv); }

    // (item, cost) per the C: A 15, B 10, C 25, D 5, E 20, F 30
    public static TheoryData<BuyItem, int> Items => new()
    {
        { BuyItem.Health, 15 }, { BuyItem.Laser, 10 }, { BuyItem.RapidFire, 25 },
        { BuyItem.Fireball, 5 }, { BuyItem.FiveFireballs, 20 }, { BuyItem.Invincible, 30 },
    };

    [Theory, MemberData(nameof(Items))]
    public void CostsMatchTheC(BuyItem item, int cost) => Assert.Equal(cost, BuyMenu.Cost(item));

    [Theory, MemberData(nameof(Items))]
    public void ExactEmeraldsBuysAndLeavesZero(BuyItem item, int cost)
    {
        var w = World(cost);
        Assert.Equal(BuyResult.Bought, BuyMenu.Buy(item, w, true, out _));
        Assert.Equal(0, w.Pl.Emeralds);
    }

    [Theory, MemberData(nameof(Items))]
    public void OneShortIsNotEnoughAndChangesNothing(BuyItem item, int cost)
    {
        var w = World(cost - 1);
        int inv = w.Pl.NumInv, health = w.Pl.Health;
        Assert.Equal(BuyResult.NotEnoughEmeralds, BuyMenu.Buy(item, w, true, out var pal));
        Assert.Equal(cost - 1, w.Pl.Emeralds);
        Assert.Equal(inv, w.Pl.NumInv);
        Assert.Equal(health, w.Pl.Health);
        Assert.Equal(0, w.Pl.Ouched);
        Assert.Equal(BuyPalette.None, pal);
    }

    [Theory, MemberData(nameof(Items))]
    public void ApplyShowsDotextmsg7OnlyWhenShort(BuyItem item, int cost)
    {
        var rec = new Rec();
        Assert.Equal(BuyResult.NotEnoughEmeralds, BuyMenu.Apply(item, World(cost - 1), rec));
        Assert.Equal(new[] { 7 }, rec.Texts);
        rec.Texts.Clear();
        Assert.Equal(BuyResult.Bought, BuyMenu.Apply(item, World(cost), rec));
        Assert.Empty(rec.Texts);
    }

    // ---- A: health unit ------------------------------------------------------------------

    [Fact]
    public void HealthBuysOneHeartWithHealFlash()
    {
        var w = World(20, health: 4);
        Assert.Equal(BuyResult.Bought, BuyMenu.Buy(BuyItem.Health, w, true, out _));
        Assert.Equal(5, w.Pl.Health);
        Assert.Equal(5, w.Pl.Emeralds);
        Assert.Equal(-4, w.Pl.Ouched);
    }

    [Fact]
    public void HealthWhileInvincibleHasNoFlash()
    {
        var w = World(20, health: 4);
        w.AddInv(Inv.Invin);
        Assert.Equal(BuyResult.Bought, BuyMenu.Buy(BuyItem.Health, w, true, out _));
        Assert.Equal(5, w.Pl.Health);
        Assert.Equal(0, w.Pl.Ouched);
    }

    [Fact]
    public void HealthAtFiveDoesNothingAndIsFree()
    {
        var w = World(99, health: 5);
        Assert.Equal(BuyResult.NothingToDo, BuyMenu.Buy(BuyItem.Health, w, true, out _));
        Assert.Equal(99, w.Pl.Emeralds);
        Assert.Equal(0, w.Pl.Ouched);
    }

    [Fact]
    public void HealthAtFiveWithoutEmeraldsStillSaysNotEnough() // the emerald check comes first in the C
        => Assert.Equal(BuyResult.NotEnoughEmeralds, BuyMenu.Buy(BuyItem.Health, World(14, health: 5), true, out _));

    [Fact]
    public void HealthHasNoFormCheck()
    {
        var w = World(15);
        Assert.Equal(BuyResult.Bought, BuyMenu.Buy(BuyItem.Health, w, false, out _));
        Assert.Equal(4, w.Pl.Health);
    }

    // ---- B: extra laser ------------------------------------------------------------------

    [Fact]
    public void LaserAddsOneAndSetsExtraLaserPalette()
    {
        var w = World(10);
        Give(w, Inv.Laser, 2);
        Assert.Equal(BuyResult.Bought, BuyMenu.Buy(BuyItem.Laser, w, true, out var pal));
        Assert.Equal(3, w.InvCount(Inv.Laser));
        Assert.Equal(BuyPalette.ExtraLaser, pal);
    }

    [Fact]
    public void LaserAtFiveDoesNothingAndIsFree()
    {
        var w = World(10);
        Give(w, Inv.Laser, 5);
        Assert.Equal(BuyResult.NothingToDo, BuyMenu.Buy(BuyItem.Laser, w, true, out var pal));
        Assert.Equal(5, w.InvCount(Inv.Laser));
        Assert.Equal(10, w.Pl.Emeralds);
        Assert.Equal(BuyPalette.None, pal);
    }

    // ---- C: rapid fire -------------------------------------------------------------------

    [Fact]
    public void RapidFireFillsLasersToFive()
    {
        var w = World(25);
        Give(w, Inv.Laser, 1);
        Assert.Equal(BuyResult.Bought, BuyMenu.Buy(BuyItem.RapidFire, w, true, out var pal));
        Assert.Equal(5, w.InvCount(Inv.Laser));
        Assert.Equal(0, w.Pl.Emeralds);
        Assert.Equal(BuyPalette.RapidFire, pal);
    }

    [Fact]
    public void RapidFireChargesEvenWhenAlreadyFive()
    {
        var w = World(30);
        Give(w, Inv.Laser, 5);
        Assert.Equal(BuyResult.Bought, BuyMenu.Buy(BuyItem.RapidFire, w, true, out var pal));
        Assert.Equal(5, w.InvCount(Inv.Laser));
        Assert.Equal(5, w.Pl.Emeralds);
        Assert.Equal(BuyPalette.RapidFire, pal);
    }

    [Fact]
    public void RapidFireWithFullInventoryTerminates() // the C would spin forever; the port must not
    {
        var w = World(25);
        Give(w, Inv.Rock, 29);
        Assert.Equal(BuyResult.Bought, BuyMenu.Buy(BuyItem.RapidFire, w, true, out _));
        Assert.Equal(29, w.Pl.NumInv);
        Assert.Equal(0, w.InvCount(Inv.Laser));
    }

    // ---- D / E: fireballs ----------------------------------------------------------------

    [Fact]
    public void FireballAddsOneUpToNine()
    {
        var w = World(10);
        Give(w, Inv.Fire, 8);
        Assert.Equal(BuyResult.Bought, BuyMenu.Buy(BuyItem.Fireball, w, true, out _));
        Assert.Equal(9, w.InvCount(Inv.Fire));
        Assert.Equal(5, w.Pl.Emeralds);
        Assert.Equal(BuyResult.NothingToDo, BuyMenu.Buy(BuyItem.Fireball, w, true, out _));
        Assert.Equal(9, w.InvCount(Inv.Fire));
        Assert.Equal(5, w.Pl.Emeralds);
    }

    [Fact]
    public void FiveFireballsOnlyBelowFive()
    {
        var w = World(40);
        Give(w, Inv.Fire, 4);
        Assert.Equal(BuyResult.Bought, BuyMenu.Buy(BuyItem.FiveFireballs, w, true, out _));
        Assert.Equal(9, w.InvCount(Inv.Fire));
        Assert.Equal(20, w.Pl.Emeralds);
        Assert.Equal(BuyResult.NothingToDo, BuyMenu.Buy(BuyItem.FiveFireballs, w, true, out _));
        Assert.Equal(20, w.Pl.Emeralds);

        var w5 = World(40);
        Give(w5, Inv.Fire, 5);
        Assert.Equal(BuyResult.NothingToDo, BuyMenu.Buy(BuyItem.FiveFireballs, w5, true, out _));
        Assert.Equal(40, w5.Pl.Emeralds);
    }

    [Fact]
    public void FullInventoryStillChargesForFireballs() // addinv silently ignores at 29; the C deducts anyway
    {
        var w = World(5);
        Give(w, Inv.Rock, 29);
        Assert.Equal(BuyResult.Bought, BuyMenu.Buy(BuyItem.Fireball, w, true, out _));
        Assert.Equal(0, w.InvCount(Inv.Fire));
        Assert.Equal(0, w.Pl.Emeralds);
    }

    // ---- F: invincibility ----------------------------------------------------------------

    [Fact]
    public void InvincibleOnceOnly_NoFormCheck()
    {
        var w = World(60);
        Assert.Equal(BuyResult.Bought, BuyMenu.Buy(BuyItem.Invincible, w, false, out _));
        Assert.Equal(1, w.InvCount(Inv.Invin));
        Assert.Equal(30, w.Pl.Emeralds);
        Assert.Equal(BuyResult.NothingToDo, BuyMenu.Buy(BuyItem.Invincible, w, false, out _));
        Assert.Equal(30, w.Pl.Emeralds);
    }

    // ---- form check (B-E only, before the emerald check) -----------------------------------

    [Theory]
    [InlineData(BuyItem.Laser)] [InlineData(BuyItem.RapidFire)]
    [InlineData(BuyItem.Fireball)] [InlineData(BuyItem.FiveFireballs)]
    public void TransformedHeroIsSilentlyIgnored(BuyItem item)
    {
        foreach (int emeralds in new[] { 0, 99 }) // no message even when too poor
        {
            var rec = new Rec();
            var w = World(emeralds);
            w.Obj(0).Kind = ObjectKind.Tiny; // hero is not obj_player
            Assert.Equal(BuyResult.WrongForm, BuyMenu.Apply(item, w, rec));
            Assert.Equal(emeralds, w.Pl.Emeralds);
            Assert.Equal(0, w.Pl.NumInv);
            Assert.Empty(rec.Texts);
        }
    }

    // ---- palette helpers (shared with the pickups) ---------------------------------------

    private static uint[] Ramp(int[,] c)
    {
        var s = new VgaScreen();
        for (int i = 0; i < 6; i++) s.SetColor6(162 + i, c[i, 0], c[i, 1], c[i, 2]);
        return s.Palette[162..168];
    }

    private static readonly int[,] RapidRamp = { { 32, 0, 0 }, { 38, 0, 0 }, { 44, 0, 0 }, { 50, 0, 0 }, { 56, 0, 0 }, { 63, 0, 0 } };
    private static readonly int[,] LaserRamp = { { 23, 13, 33 }, { 29, 19, 39 }, { 35, 25, 45 }, { 42, 32, 51 }, { 49, 40, 57 }, { 57, 50, 63 } };

    [Fact]
    public void PaletteHelpersWriteTheCConstants()
    {
        var s = new VgaScreen();
        Effects.SetRapidFirePalette(s);
        Assert.Equal(Ramp(RapidRamp), s.Palette[162..168]);
        Effects.SetExtraLaserPalette(s);
        Assert.Equal(Ramp(LaserRamp), s.Palette[162..168]);
    }

    [Fact]
    public void ApplyAppliesThePalettes()
    {
        var w = World(25);
        BuyMenu.Apply(BuyItem.RapidFire, w, new Rec());
        Assert.Equal(Ramp(RapidRamp), w.Screen.Palette[162..168]);
        var w2 = World(10);
        BuyMenu.Apply(BuyItem.Laser, w2, new Rec());
        Assert.Equal(Ramp(LaserRamp), w2.Screen.Palette[162..168]);
    }

    // ---- IsOnMap (checkpoint-0 state == 5) -----------------------------------------------

    [Fact]
    public void MapBoardIsOnMap() => Assert.True(GameFlow.IsOnMap(TestWorld.Load(1, "MAP")));

    [Fact]
    public void LevelBoardIsNotOnMap() => Assert.False(GameFlow.IsOnMap(TestWorld.Load(1, "BOARD_01")));

    [Fact]
    public void CheckpointZeroStateDecides()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        int c = w.FindCheckpt(0);
        if (c == 0) { c = w.AddObj(ObjectKind.Checkpt, 100, 100, 0, 0); w.Obj(c).Counter = 0; }
        w.Obj(c).State = 5;
        Assert.True(GameFlow.IsOnMap(w));
        w.Obj(c).State = 1;
        Assert.False(GameFlow.IsOnMap(w));
    }

    [Fact]
    public void NoCheckpointZeroFallsBackToTheHeroState()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        for (int i = 0; i < w.Objects.Count; i++)
            if (w.Obj(i).Kind == ObjectKind.Checkpt && w.Obj(i).Counter == 0) w.Obj(i).Counter = 77;
        Assert.Equal(0, w.FindCheckpt(0));
        w.Obj(0).State = 5;
        Assert.True(GameFlow.IsOnMap(w));
        w.Obj(0).State = 2;
        Assert.False(GameFlow.IsOnMap(w));
    }
}
