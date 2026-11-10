using Xunit;
using XargonNET.Objects;

namespace XargonNET.Tests;

/// <summary>Task 10: player draw states, pain (p_ouch), crouch hitbox, jump boots (X_PLAYER.C / X_OBJMAN.C).</summary>
public class PlayerTests
{
    private const int StStand = 0, StJumping = 2, StBegin = 4, StDie = 5, StPlatform = 7;
    private const int Base = 0x600;             // kindtable[obj_player]*256
    private const int DieAsh = 0, DieBird = 1, DieFish = 2;

    private static (ObjectManager w, Player p) Hero()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.NumInv = 0;
        var h = w.Obj(0);
        h.State = StStand; h.StateCount = 60; h.Xd = 0; h.Yd = 0; h.Info1 = 1;
        return (w, new Player(w, TestWorld.Vp));
    }

    // ---- Jump boots ------------------------------------------------------------------------

    [Theory]
    [InlineData(0, -16)]
    [InlineData(2, -24)]
    public void JumpHeightScalesWithJumpBoots(int boots, int expectedYd)
    {
        var (w, p) = Hero();
        for (int i = 0; i < boots; i++) w.AddInv(Inv.Jump);
        p.Update(new Controls { Fire2 = 1 });
        Assert.Equal(StJumping, w.Obj(0).State);
        Assert.Equal(expectedYd, w.Obj(0).Yd);
    }

    [Fact]
    public void FireOnPlatformFacesLastDirection()
    {
        var (w, p) = Hero();
        var h = w.Obj(0);
        h.State = StPlatform; h.Xd = 0; h.Info1 = -1;
        p.Update(new Controls { Fire1 = 1 });
        Assert.Equal(-1, h.Xd); // C: xd==0 && st_platform -> xd=info1
    }

    // ---- Crouch hitbox (upd_objs) ----------------------------------------------------------

    private static uint TouchBonusAtBottom(bool crouch, int bottomOffset)
    {
        var (w, _) = Hero();
        var h = w.Obj(0);
        h.X = 100; h.Y = 100; h.Yd = crouch ? 3 : 0;
        int yl = KindDefs.Get(ObjectKind.Bonus).Yl;
        int n = w.AddObj(ObjectKind.Bonus, 100, 100 + bottomOffset - yl, 0, 0);
        w.Obj(n).State = 18; // 75 points
        uint before = w.Pl.Score;
        w.Gamecount++;
        w.UpdateObjects(TestWorld.Vp);
        return w.Pl.Score - before;
    }

    [Fact]
    public void CrouchingHeroIsNotTouchedAboveY18() => Assert.Equal(0u, TouchBonusAtBottom(true, 18));

    [Fact]
    public void CrouchingHeroIsTouchedBelowY18() => Assert.Equal(75u, TouchBonusAtBottom(true, 19));

    [Fact]
    public void StandingHeroUsesFullBox() => Assert.Equal(75u, TouchBonusAtBottom(false, 18));

    // ---- p_ouch -----------------------------------------------------------------------------

    [Fact]
    public void OuchOnTinyIsNoOp()
    {
        var (w, _) = Hero();
        w.Obj(0).Kind = (byte)ObjectKind.Tiny;
        int hp = w.Pl.Health;
        w.POuch(10, DieAsh);
        Assert.Equal(hp, w.Pl.Health);
        Assert.Equal(StStand, w.Obj(0).State);
        Assert.Equal((byte)ObjectKind.Tiny, w.Obj(0).Kind);
    }

    [Fact]
    public void OuchAsHeroBeeDiesAsBird()
    {
        var (w, _) = Hero();
        w.Obj(0).Kind = (byte)ObjectKind.HeroBee;
        w.POuch(10, DieAsh);
        var h = w.Obj(0);
        Assert.Equal(StDie, h.State);
        Assert.Equal(DieBird, h.SubState);
        Assert.Equal(2, h.Yd);
        Assert.Equal(0, w.Pl.Ouched);
        Assert.Equal((byte)ObjectKind.Player, h.Kind);
    }

    [Fact]
    public void LethalOuchOnPlatformStaysAt18()
    {
        var (w, _) = Hero();
        w.Obj(0).State = StPlatform;
        w.POuch(10, DieAsh);
        Assert.Equal(StDie, w.Obj(0).State);
        Assert.Equal(18, w.Obj(0).StateCount);
    }

    [Fact]
    public void NonLethalOuchSetsHurtFlash()
    {
        var (w, _) = Hero();
        w.POuch(1, DieAsh);
        Assert.Equal(4, w.Pl.Ouched);
        Assert.Equal(StStand, w.Obj(0).State);
    }

    // ---- Draw shapes (msg_draw) -------------------------------------------------------------

    private static int Shape(int state, int sub = 0, int sc = 0, int xd = 0, int yd = 0, int info1 = 1,
        int dy1 = 0, int ouched = 0) =>
        Player.PlayerShape(state, sub, sc, xd, yd, info1, dy1, ouched).Shape;

    [Theory]
    [InlineData(1, 0x1b)]
    [InlineData(4, 0x1b)]
    public void HurtFlashFacesLeftOrRight(int ouched, int off)
    {
        Assert.Equal(Base + off, Shape(StStand, xd: 0, ouched: ouched));
        Assert.Equal(Base + off + 1, Shape(StStand, xd: 1, ouched: ouched));
        Assert.Equal(Base + off, Shape(StJumping, xd: -1, ouched: ouched));
    }

    [Fact]
    public void NoHurtFlashWhenOuchedZeroOrNegative()
    {
        Assert.NotEqual(Base + 0x1b, Shape(StStand, sc: 10, ouched: 0));
        Assert.Equal(Base + 0x0a + 1, Shape(StStand, sc: 10, info1: 1, ouched: -4));
    }

    [Theory]
    [InlineData(0, 0x13)]
    [InlineData(3, 0x13)]
    [InlineData(4, 0x14)]
    [InlineData(19, 0x13 + 4)]
    public void AshDeathAnimates(int sc, int off) => Assert.Equal(Base + off, Shape(StDie, DieAsh, sc));

    [Fact]
    public void BirdAndFishDeathUseTheirOwnShapeTables()
    {
        Assert.Equal(KindDefs.Get(ObjectKind.HeroBee).ShapeTable * 256 + 6, Shape(StDie, DieBird, 5));
        Assert.Equal(KindDefs.Get(ObjectKind.HeroSwim).ShapeTable * 256 + 5, Shape(StDie, DieFish, 5));
    }

    [Fact]
    public void PlatformDrawUsesXPlus3AndSquatOrWalk()
    {
        var stand = Player.PlayerShape(StPlatform, 0, 0, 0, 0, 1, 0, 0);
        Assert.Equal((Base + 0x08, 3), (stand.Shape, stand.XOff));
        Assert.Equal(Base + 0x1a, Player.PlayerShape(StPlatform, 0, 0, 0, 0, 1, dy1: 1, 0).Shape);
        Assert.Equal(Base + 0x0a, Player.PlayerShape(StPlatform, 0, 0, -1, 0, 1, 0, 0).Shape);
        Assert.Equal(Base + 0x0b, Player.PlayerShape(StPlatform, 0, 0, 1, 0, 1, -1, 0).Shape);
        // xd != 0 but dy1 > 0: the walk shape is skipped, squat wins
        var sq = Player.PlayerShape(StPlatform, 0, 0, 1, 0, 1, dy1: 1, 0);
        Assert.Equal((Base + 0x1a, 3), (sq.Shape, sq.XOff));
    }

    [Fact]
    public void StandAndOtherStatesKeepTheirShapes()
    {
        Assert.Equal(Base + 0x1a, Shape(StStand, sc: -4));
        Assert.Equal(Base + 0x1a, Shape(StStand, sc: 10, yd: 3));
        Assert.Equal(Base + 0x0b, Shape(StStand, sc: 10, info1: 1));
        Assert.Equal(Base + 0x08, Shape(StStand, sc: 100));
        Assert.Equal(Base + 0x09, Shape(StStand, sc: 200));
        Assert.Equal(Base + 0x18, Shape(StJumping, yd: -2));
        Assert.Equal(Base + 0x10 + 2, Shape(StJumping, xd: 1, yd: 2));
        Assert.Equal(Base + 0x08, Shape(StBegin, sc: 5));
        Assert.Equal(Base + 0x1d + 2, Shape(6, sc: 9));
    }

    // ---- Hurt-flash countdown (refresh / drawstats) -----------------------------------------

    [Theory]
    [InlineData(4, 3)]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    [InlineData(-4, -3)]
    [InlineData(-1, -1)]
    public void OuchedTicksAfterDraw(int from, int to) => Assert.Equal(to, Player.OuchedAfterDraw(from));

    [Theory]
    [InlineData(1, 0)]
    [InlineData(-1, 0)]
    [InlineData(2, 2)]
    [InlineData(0, 0)]
    public void OuchedResetsBeforeDraw(int from, int to) => Assert.Equal(to, Player.OuchedBeforeDraw(from));

    [Fact]
    public void HurtFlashShowsForThreeDraws()
    {
        int o = 4, flashes = 0;
        for (int i = 0; i < 8; i++)
        {
            o = Player.OuchedBeforeDraw(o);
            if (o > 0) flashes++;
            o = Player.OuchedAfterDraw(o);
        }
        Assert.Equal(3, flashes);
    }
}
