using Xunit;
using XargonNET.Objects;

namespace XargonNET.Tests;

/// <summary>
/// Task 12: player forms (heroswim/herobee) and transform tokens (X_PLAYER.C msg_heroswim /
/// msg_herobee / playerxfm, X_OBJ.C msg_token / msg_bullet). Drives the hero through the real
/// message paths: Player.Update dispatches to the form handler when object 0's kind is a form.
/// </summary>
public class FormTests
{
    private const int StDie = 5, DieFish = 2, DieBird = 1;

    /// <summary>Level-1 world with a rectangle of cells cleared to empty air (open space for forms).</summary>
    private static ObjectManager Air()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.NumInv = 0;
        for (int y = 1; y < 14; y++)
            for (int x = 2; x < 24; x++) w.SetBoard(x, y, 0);
        return w;
    }

    private static void Fill(ObjectManager w, int tile)
    {
        for (int y = 1; y < 14; y++)
            for (int x = 2; x < 24; x++) w.SetBoard(x, y, tile);
    }

    private static Player PlaceHero(ObjectManager w, int kind, int x, int y)
    {
        var h = w.Obj(0);
        h.Kind = (byte)kind;
        ObjectManager.SetObjSize(h);
        h.X = x; h.Y = y; h.Xd = 0; h.Yd = 0; h.State = 0; h.SubState = 0; h.StateCount = 0;
        return new Player(w, TestWorld.Vp);
    }

    // ---- Transform tokens (msg_token xfm branch -> playerxfm) --------------------------------

    [Fact]
    public void FishTokenTransformsToHeroSwimAndClearsBird()
    {
        var w = Air();
        PlaceHero(w, ObjectKind.Player, 160, 96);
        w.AddInv(Inv.Bird); // a bird form item in the bag
        int t = w.AddObj(ObjectKind.Token, 160, 96, 0, 0);
        w.Obj(t).State = 6; // fish token

        Assert.Equal(1, w.Dispatch(t, Msg.Touch, 0)); // the hero (obj 0) touches it

        Assert.Equal((byte)ObjectKind.HeroSwim, w.Obj(0).Kind);
        Assert.Equal(0, w.InvCount(Inv.Bird));   // playerxfm clears every inv_xfm item
    }

    [Fact]
    public void BirdTokenTransformsToHeroBeeAndClearsFish()
    {
        var w = Air();
        PlaceHero(w, ObjectKind.Player, 160, 96);
        w.AddInv(Inv.Fish);
        int t = w.AddObj(ObjectKind.Token, 160, 96, 0, 0);
        w.Obj(t).State = 5; // bird token

        Assert.Equal(1, w.Dispatch(t, Msg.Touch, 0));

        Assert.Equal((byte)ObjectKind.HeroBee, w.Obj(0).Kind);
        Assert.Equal(0, w.InvCount(Inv.Fish));
    }

    [Fact]
    public void StateZeroTokenRevertsFormToPlayer()
    {
        var w = Air();
        PlaceHero(w, ObjectKind.HeroBee, 160, 96);
        w.PlayerXfm(0); // inv_hero: the "become human again" token
        Assert.Equal((byte)ObjectKind.Player, w.Obj(0).Kind);
    }

    [Fact]
    public void TransformFailsWhenNoRoomAndKeepsForm()
    {
        var w = Air();
        // Wall the hero in solid on all sides: the larger form cannot fit, so the kind is unchanged.
        var h = w.Obj(0);
        h.Kind = (byte)ObjectKind.Player;
        ObjectManager.SetObjSize(h);
        h.X = 160; h.Y = 96;
        Fill(w, 64); // solid tile everywhere in the arena
        w.PlayerXfm(Inv.Fish);
        Assert.Equal((byte)ObjectKind.Player, w.Obj(0).Kind);
    }

    // ---- Token state 6 float/bob (msg_token msg_update) --------------------------------------

    [Fact]
    public void FishTokenBobsAndTakesWaterHitbox()
    {
        var w = Air();
        int t = w.AddObj(ObjectKind.Token, 160, 96, 0, 0);
        w.Obj(t).State = 6;
        int y0 = w.Obj(t).Y;

        Assert.Equal(1, w.Dispatch(t, Msg.Update, 0));
        Assert.Equal(1, w.Obj(t).Info1);  // latched
        Assert.Equal(30, w.Obj(t).Xl);     // xl=30, yl=38 per the C
        Assert.Equal(38, w.Obj(t).Yl);
        Assert.Equal(1, w.Obj(t).StateCount);
        Assert.Equal(y0 + 1, w.Obj(t).Y);  // ydtab[1] = +1

        for (int i = 0; i < 7; i++) w.Dispatch(t, Msg.Update, 0); // complete the 8-step cycle
        Assert.Equal(0, w.Obj(t).StateCount);
        Assert.Equal(y0, w.Obj(t).Y);      // ydtab sums to 0 over a full cycle
    }

    [Fact]
    public void NonFishTokenDoesNotBob()
    {
        var w = Air();
        int t = w.AddObj(ObjectKind.Token, 160, 96, 0, 0);
        w.Obj(t).State = 1; // gate-key token
        int y0 = w.Obj(t).Y;
        Assert.Equal(0, w.Dispatch(t, Msg.Update, 0));
        Assert.Equal(y0, w.Obj(t).Y);
        Assert.Equal(16, w.Obj(t).Yl); // hitbox untouched
    }

    // ---- heroswim movement (msg_heroswim) ----------------------------------------------------

    [Fact]
    public void HeroSwimMovesHorizontallyThroughWater()
    {
        var w = Air();
        Fill(w, Tiles.WaterT1); // all water
        var p = PlaceHero(w, ObjectKind.HeroSwim, 160, 96);
        int x0 = w.Obj(0).X;
        p.Update(new Controls { Dx1 = 1 });
        Assert.True(w.Obj(0).X > x0, "swim hero advances into water");
    }

    [Fact]
    public void HeroSwimCannotMoveHorizontallyOutOfWaterAndFalls()
    {
        var w = Air(); // all air (non-water)
        var p = PlaceHero(w, ObjectKind.HeroSwim, 160, 96);
        int x0 = w.Obj(0).X, y0 = w.Obj(0).Y;
        p.Update(new Controls { Dx1 = 1 });
        Assert.Equal(x0, w.Obj(0).X);      // fishdo blocks horizontal move into non-water
        Assert.True(w.Obj(0).Y > y0);      // gravity (yd += 4) pulls it down
    }

    // ---- herobee flight (msg_herobee) --------------------------------------------------------

    [Fact]
    public void HeroBeeFiresLaserWithFire1()
    {
        var w = Air();
        var p = PlaceHero(w, ObjectKind.HeroBee, 160, 96);
        w.Obj(0).SubState = 1; // facing right
        int lasers = w.CountKind(ObjectKind.Laser);
        p.Update(new Controls { Fire1 = 1 });
        Assert.Equal(lasers + 1, w.CountKind(ObjectKind.Laser));
    }

    [Fact]
    public void HeroBeeFlapsUpWithFire2()
    {
        var w = Air();
        var p = PlaceHero(w, ObjectKind.HeroBee, 160, 96);
        p.Update(new Controls { Fire2 = 1 });
        Assert.Equal(-6, w.Obj(0).Yd); // jump-flap sets yd = -6
    }

    // ---- Bullet touching the swim hero (msg_bullet heroswim branch) --------------------------

    [Fact]
    public void BulletDealsFishDamageToSwimHeroAndKillsItWhenLethal()
    {
        var w = Air();
        PlaceHero(w, ObjectKind.HeroSwim, 160, 96);
        w.Pl.Health = 1;
        int b = w.AddObj(ObjectKind.Bullet, 160, 96, 0, 0);
        int hithero = w.CountKind(ObjectKind.HitHero);

        Assert.Equal(1, w.Dispatch(b, Msg.Touch, 0)); // bullet touches hero (obj 0)

        Assert.Equal(0, w.Pl.Health);
        Assert.Equal(StDie, w.Obj(0).State);
        Assert.Equal(DieFish, w.Obj(0).SubState);
        Assert.Equal((byte)ObjectKind.Player, w.Obj(0).Kind); // p_ouch reverts the form on death
        Assert.Equal((byte)ObjectKind.Killme, w.Obj(b).Kind);
        Assert.Equal(hithero + 1, w.CountKind(ObjectKind.HitHero));
    }

    // ---- Death resets kind; ouched flash ticks for a non-Player hero -------------------------

    [Fact]
    public void LethalOuchOnHeroSwimRevertsToPlayerAndDiesAsFish()
    {
        var w = Air();
        PlaceHero(w, ObjectKind.HeroSwim, 160, 96);
        w.Pl.Health = 1;
        w.POuch(1, DieFish);
        Assert.Equal((byte)ObjectKind.Player, w.Obj(0).Kind);
        Assert.Equal(StDie, w.Obj(0).State);
        Assert.Equal(DieFish, w.Obj(0).SubState);
    }

    [Fact]
    public void OuchedFlashTicksForANonPlayerHeroKind()
    {
        var w = Air();
        PlaceHero(w, ObjectKind.HeroBee, 160, 96);
        w.Pl.Ouched = 4;
        w.HeroColorFlashPre(w.Screen);
        w.HeroColorFlashPost(w.Screen);
        Assert.Equal(3, w.Pl.Ouched); // the hurt-flash countdown advances regardless of kind
    }
}
