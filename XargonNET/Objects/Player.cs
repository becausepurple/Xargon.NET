using XargonNET.Assets;
using XargonNET.Video;

namespace XargonNET.Objects;

/// <summary>
/// The player state machine (msg_player in X_PLAYER.C), driving object 0. Ported faithfully for
/// the core states — stand/walk, jump/fall (gravity), climb (vines), and the level-entry
/// animation — plus camera follow (calc_scroll). Weapons/inventory, dying-respawn detail, water
/// swim and transport are stubbed until their supporting systems land (Phase 6/7).
/// </summary>
public sealed class Player
{
    private const int StStand = 0, StStill = 1, StJumping = 2, StClimbing = 3,
                      StBegin = 4, StDie = 5, StTransport = 6, StPlatform = 7;
    private const int FidgetMax = 300;
    private const int ScrnXs = 21, ScrnYs = 11;
    private const int F = GameFlags.PlayerThru, NS = GameFlags.NotStair, NV = GameFlags.NotVine;

    private static readonly int[] ClimbSh = { 0x0c, 0x0c, 0x0d, 0x0e, 0x0e, 0x0d, 0x0d };
    private static readonly int[] ClimbY = { 4, 0, 0, 6, 4, 4, 0 };

    private readonly ObjectManager _om;
    private readonly Viewport _vp;

    private Obj P => _om.Objects[0];

    /// <summary>
    /// Set when the death throe completes (st_die reaches statecount 19, where the C calls
    /// p_reenter(1)). The game loop observes this and runs the death transition — reload the level,
    /// restore the entry snapshot and replace this controller — rather than respawning inline, because
    /// reentering rebuilds the object list this <see cref="Player"/> is bound to.
    /// </summary>
    public bool Died { get; private set; }

    public Player(ObjectManager om, Viewport gameVp)
    {
        _om = om;
        _vp = gameVp;
    }

    private static int Sgn(int x) => (x > 0 ? 1 : 0) - (x < 0 ? 1 : 0);

    private int _dy1; // dy1 as last seen by Update; msg_draw reads the live global (platform squat)

    public void Update(Controls c)
    {
        var p = P;
        // Expose this frame's input the way the C globals (dx1/dy1/fire) do, so the form handlers
        // (dispatched below) and object handlers read the same values.
        _om.CurControls = c;
        _om.InputDx = c.Dx1;
        _om.InputDy = c.Dy1;

        // When a transform token has turned the hero into a bird/fish, object 0's kind drives a
        // different message handler (msg_herobee/msg_heroswim) — dispatch it instead of the platformer
        // state machine. On death p_ouch reverts the kind to Player, so the st_die path below resumes.
        if (p.Kind == ObjectKind.HeroSwim || p.Kind == ObjectKind.HeroBee)
        {
            _om.Dispatch(0, Msg.Update, 0);
            return;
        }

        _dy1 = c.Dy1;
        int mod1 = 0, peeky = 0;

        switch (p.State)
        {
            case StStand: mod1 = UpdateStand(p, c, ref peeky); break;
            case StBegin: UpdateBegin(p); break;
            case StPlatform: UpdatePlatform(p, c, ref peeky); break;
            case StJumping: UpdateJumping(p, c); break;
            case StClimbing: UpdateClimbing(p, c); break;
            case StDie:
                // Death throe (X_PLAYER.C st_die): birds tumble, fish sink; after 19 ticks the C calls
                // p_reenter(1). Here we flag death and let the game loop run the reenter/reload.
                if (p.SubState == 1 /*die_bird*/)
                {
                    // C: yd=min(yd++,8) with Turbo C's min() macro expands to (yd++<8 ? yd++ : 8); the
                    // assignment overwrites the increments, so yd ends at old+1 capped at 8 (not stuck).
                    p.Yd = Math.Min(p.Yd + 1, 8);
                    p.Xd = _om.XrRandom(7) - 3;
                    _om.JustMove(0, p.X + p.Xd, p.Y + p.Yd);
                }
                else if (p.SubState == 2 /*die_fish*/)
                {
                    _om.Fishdo(0, p.X, p.Y - p.StateCount);
                }
                if (p.StateCount >= 19) { Died = true; return; }
                p.StateCount++;
                return;
            case StTransport:
                // Door/teleport: hold at the pad, then hand off to the matching transport object
                // (sendtrig by substate) which relocates the player and recentres the camera.
                if (p.StateCount >= 15)
                {
                    p.State = StStand;
                    _om.SendTrig(p.SubState, Msg.Trigger, 0);
                }
                p.StateCount++;
                return;
        }

        // Common tail (X_PLAYER.C lines 296-356).
        if (p.Xd != 0) p.Info1 = p.Xd;
        Fire(p, c);
        p.StateCount++;
        if (p.Counter != 0) p.Counter--;
        _om.TouchBkgnd(0); // hazard tiles (water/lava/spikes) hurt the player
        ApplyScroll(peeky);
    }

    /// <summary>
    /// Firing (the fire1 block of msg_player): in a can-fire state, spawn a weapon in the facing
    /// direction — fireball if held, else rock, else a laser (capped by the laser inventory count).
    /// </summary>
    private void Fire(Obj p, Controls c)
    {
        if (c.Fire1 == 0 || !ObjectManager.CanFire(p.State) || p.Info1 == 0) return;
        c.Fire1Off = 1;

        if (p.Xd == 0 && p.State == StStand) { p.SubState = 0; p.StateCount = 3; }
        else if (p.Xd == 0 && p.State == StPlatform) p.Xd = p.Info1; // face last direction on a platform

        if (_om.InvCount(Inv.Fire) > 0)
        {
            if (_om.CountKind(35) < 1)
            {
                _om.TakeInv(Inv.Fire);
                _om.AddObj(ObjectKind.Fireball, p.X - 8 + (p.Info1 > 0 ? 16 : 0), p.Y + 8, p.Info1 * 8, 0);
            }
        }
        else if (_om.InvCount(Inv.Rock) > 0)
        {
            if (_om.CountKind(34) < _om.InvCount(Inv.Rock))
                _om.AddObj(ObjectKind.Rock, p.X + 4, p.Y + 8, p.Info1 * 6, -8);
        }
        else if (_om.CountKind(36) < _om.InvCount(Inv.Laser))
        {
            _om.AddObj(ObjectKind.Laser, p.X + 4 + (p.Info1 > 0 ? 4 : 0), p.Y + 18 + (p.Yd == 3 ? 12 : 0), p.Info1 * 6, 0);
        }
    }

    private int UpdateStand(Obj p, Controls c, ref int peeky)
    {
        int mod1 = 0;
        if (_om.Cando(0, p.X, p.Y, F) == 0) p.SubState = -1;
        if (p.Yd != 0) { p.Yd -= Sgn(p.Yd); mod1 = 1; }

        if (p.StateCount < 0)
        {
            if (p.StateCount == -1) p.StateCount = 3;
        }
        else if (p.Xd == 0)
        {
            if (c.Dx1 != 0)
            {
                p.SubState = 7; p.Xd = c.Dx1; p.StateCount = 0;
                return 1;
            }
            if (p.StateCount > FidgetMax) p.StateCount = 20;
            else if (p.StateCount == FidgetMax - 42) // 258: idle fidget line (x_player.c:85-87)
                _om.Txt(XargonNET.Text.GameStrings.FidgetMsg(_om.Episode, _om.XrRandom(4)), 6, 0);

            if (_om.Cando(0, p.X, Behaviors.Floor16(p.Y) + 16, F | NS) == (F | NS))
            {
                p.State = StJumping; p.Yd = 0; p.SubState = 2; // walked off an edge
            }
        }
        else if (c.Dx1 != 0)
        {
            if (c.Dx1 == p.Xd)
            {
                if (_om.Cando(0, p.X + c.Dx1 * 8, p.Y, F) != 0) { p.X += c.Dx1 * 8; p.Yd = 0; }
                p.SubState = (p.SubState + 1) & 7; p.StateCount = 0;
            }
            else { p.Info1 = p.Xd; p.Xd = 0; p.StateCount = 4; }
        }
        else if (p.StateCount >= 2 && p.SubState != 0)
        {
            p.Xd = 0; p.SubState = 0; p.StateCount = 0;
        }

        if (_om.Cando(0, p.X, Behaviors.Floor16(p.Y) + 16, F | NS) == (F | NS))
        {
            p.State = StJumping; p.Yd = 0; p.SubState = 0; // can't stand here -> fall
        }

        if (c.Fire2 != 0)
        {
            c.Fire2Off = 1; p.State = StJumping;
            p.Yd = -(16 + 4 * _om.InvCount(Inv.Jump)); p.SubState = 0; p.Xd = c.Dx1; // jump boots add height
        }
        else if (c.Dy1 != 0)
        {
            if ((p.X & 15) == 0 && _om.Cando(0, p.X, p.Y + c.Dy1 * 4, F) != 0
                && _om.Cando(0, p.X, p.Y + c.Dy1 * 4, NV) == 0
                && _om.Cando(0, p.X - 8, p.Y + c.Dy1 * 4, NV) == 0)
            {
                _om.MoveObj(0, p.X, p.Y + c.Dy1 * 4);
                p.State = StClimbing; p.SubState = 3; // grabbed a vine
            }
            if (p.State == StStand && c.Dx1 == 0)
            {
                p.Yd = Math.Min(Math.Max(-3, p.Yd + c.Dy1 * 2), 3);
                p.Xd = 0; p.SubState = 0; p.StateCount = 3;
                peeky = p.Yd > 1 ? 2 : (p.Yd < -1 ? -2 : 0);
            }
        }
        return mod1;
    }

    private void UpdateBegin(Obj p)
    {
        p.Xl = KindDefs.Get(ObjectKind.Player).Xl;
        p.Yl = KindDefs.Get(ObjectKind.Player).Yl;
        _om.Pl.Ouched = -1; // reset hero colours (X_PLAYER.C:158), every st_begin update
        if (p.StateCount >= 40)
        {
            p.State = StStand; p.StateCount = 60; p.Info1 = 1; p.Xd = 0; p.Yd = 0;
        }
    }

    private void UpdatePlatform(Obj p, Controls c, ref int peeky)
    {
        if (p.Yd != 0)
        {
            p.Yd = Math.Min(Math.Max(-3, p.Yd + c.Dy1 * 2), 3);
            peeky = p.Yd > 1 ? 2 : (p.Yd < -1 ? -2 : 0);
        }
    }

    private void UpdateJumping(Obj p, Controls c)
    {
        p.Counter = 0;
        if ((p.X & 15) == 0 && _om.Cando(0, p.X, p.Y, NV) == 0 && _om.Cando(0, p.X - 8, p.Y, NV) == 0)
        {
            p.State = StClimbing; p.SubState = 6; // caught a vine
            return;
        }
        if (++p.SubState > 2)
        {
            p.Yd += 2;
            if (p.Yd > 16) p.Yd = 16;
            if (c.Dx1 != 0) { p.SubState = 2; p.Xd = c.Dx1; }
            if (p.SubState > 8) p.Xd = 0;
            if (_om.TryMovey(0, p.X + c.Dx1 * 8, p.Y + p.Yd) == 0)
            {
                if (p.Yd >= 0)
                {
                    int landed = ((p.Y + p.Yl) & 15) == 0 ? 1 : 0;
                    if (landed == 0 && _om.TryMovey(0, p.X + c.Dx1 * 8, Behaviors.Floor16(p.Y + p.Yl) + 16 - p.Yl) == 0)
                        landed = 1;
                    if (landed == 1)
                    {
                        p.State = StStand; p.Counter = 6;
                        p.StateCount = (p.Yd >= 16 ? -7 : -4) + (c.Dx1 != 0 ? 1 : 0);
                        p.Xd = 0; p.Yd = 0;
                    }
                }
                else
                {
                    int desty = Behaviors.Floor16(p.Y - 1);
                    if (desty == p.Y) p.Yd = 0;
                    else if (_om.TryMovey(0, p.X + c.Dx1 * 8, desty) == 0) p.Yd = 0; // bumped head
                }
            }
        }
    }

    private void UpdateClimbing(Obj p, Controls c)
    {
        if (c.Dx1 != 0)
        {
            if (p.SubState != 6 && _om.Cando(0, p.X + c.Dx1 * 8, p.Y, F) != 0)
            {
                p.Counter = 0;
                _om.MoveObj(0, p.X + c.Dx1 * 8, p.Y);
                p.State = StJumping; p.SubState = 2; p.Xd = c.Dx1;
                if (c.Fire2 != 0) { c.Fire2Off = 1; p.Yd = -16; }
                else p.Yd = -4;
            }
        }
        else
        {
            p.Xd = 0;
            if (p.SubState == 6) p.SubState = 0;
            if (c.Dy1 != 0)
            {
                int desty = p.Y + (c.Dy1 < 0 ? -ClimbY[p.SubState] : 4);
                if (_om.Cando(0, p.X, desty, F) == 0)
                {
                    desty = c.Dy1 > 0 ? Behaviors.Floor16(p.Y) + 16 : Behaviors.Floor16(p.Y - 1);
                    if (_om.Cando(0, p.X, desty, F) == 0) desty = 0;
                }
                if (desty != 0)
                {
                    if (c.Dy1 < 0) { p.SubState += c.Dy1; if (p.SubState >= 6) p.SubState = 0; }
                    else p.SubState = 2;
                    _om.MoveObj(0, p.X, desty);
                    if (_om.Cando(0, p.X, p.Y, NV) != 0) { p.State = StStand; p.Xd = 0; }
                }
            }
            if (p.SubState < 0) p.SubState = 5;
            else if (p.SubState > 6) p.SubState = 6;
        }
    }

    // ---- Draw (msg_player msg_draw) -------------------------------------------------------

    /// <summary>
    /// Draw-shape selection (msg_draw in X_PLAYER.C), pure. Returns the absolute shape id and the x
    /// offset to draw it at (platform drawing is shifted +3). The caller handles st_begin's
    /// ground-clipped rise and the landing dust.
    /// </summary>
    internal static (int Shape, int XOff) PlayerShape(int state, int subState, int stateCount,
        int xd, int yd, int info1, int dy1, int ouched)
    {
        int sh = KindDefs.Get(ObjectKind.Player).ShapeTable * 256; // 0x600
        if (ouched > 0) return (sh + 0x1b + (xd > 0 ? 1 : 0), 0);  // hurt flash overrides every state

        switch (state)
        {
            case StStand:
                if (stateCount < 0) sh += 0x1a;
                else if (yd == 3) sh += 0x1a; // squat
                else if (xd == 0 && stateCount >= 3)
                {
                    if (stateCount < 60) sh += 0x0a + (info1 > 0 ? 1 : 0);
                    else if (stateCount > 120) sh += 0x09;
                    else sh += 0x08;
                }
                else if (subState == 8) sh += 0x0a + (xd > 0 ? 1 : 0);
                else sh += ((subState & 7) / 2) + (xd > 0 ? 4 : 0);
                break;
            case StJumping:
                if (xd == 0) sh += yd <= 0 ? 0x18 : 0x19;
                else { sh += yd <= 0 ? 0x0f : 0x10; if (xd > 0) sh += 2; }
                break;
            case StClimbing:
                sh += ClimbSh[Math.Clamp(subState, 0, 6)];
                break;
            case StBegin:
                sh += 0x08;
                break;
            case StDie:
                switch (subState)
                {
                    case 0 /*die_ash*/: sh += 0x13 + stateCount / 4; break;
                    case 1 /*die_bird*/: return (KindDefs.Get(ObjectKind.HeroBee).ShapeTable * 256 + 6, 0);
                    case 2 /*die_fish*/: return (KindDefs.Get(ObjectKind.HeroSwim).ShapeTable * 256 + 5, 0);
                }
                break;
            case StTransport:
                sh += 0x1d + stateCount / 4; // walking-into-door animation
                break;
            case StPlatform:
                if (xd != 0 && dy1 <= 0) return (sh + 0x0a + (xd > 0 ? 1 : 0), 3);
                return (sh + (dy1 > 0 ? 0x1a : 0x08), 3); // squat when pressing down
            case StStill:
                sh += 0x09;
                break;
        }
        return (sh, 0);
    }

    /// <summary>drawstats() at the head of refresh: a hero colour-flash counter at +-1 is cleared.</summary>
    internal static int OuchedBeforeDraw(int ouched) => ouched is 1 or -1 ? 0 : ouched;

    /// <summary>End of refresh: the counter steps toward +-1 each drawn frame (colours: <see cref="Effects.OuchedColors"/>).</summary>
    internal static int OuchedAfterDraw(int ouched) => ouched >= 2 ? ouched - 1 : (ouched <= -2 ? ouched + 1 : ouched);

    /// <summary>
    /// The landing-dust puff after the state switch in msg_draw. It is skipped only where the C returns
    /// early: the hurt flash (ouched&gt;0), st_begin, the bird/fish death sprites and the moving-platform pose.
    /// </summary>
    internal static bool DrawsLandingDust(int counter, int state, int subState, int xd, int dy1, int ouched)
    {
        if (counter <= 0 || ouched > 0 || state == StBegin) return false;
        if (state == StDie && subState is 1 /*die_bird*/ or 2 /*die_fish*/) return false;
        if (state == StPlatform && xd != 0 && dy1 <= 0) return false;
        return true;
    }

    public void Draw(VgaScreen screen, ShapeManager shapes)
    {
        var p = P;
        // A transformed hero (bird/fish) draws through its own handler (msg_herobee/msg_heroswim).
        if (p.Kind == ObjectKind.HeroSwim || p.Kind == ObjectKind.HeroBee)
        {
            _om.Dispatch(0, Msg.Draw, 0);
            return;
        }

        var pl = _om.Pl;
        // The hero hurt/heal colour flash (drawstats/refresh) is ticked once per frame by the caller
        // (ObjectManager.HeroColorFlash*), hero-kind independent; here we only read it for the sprite.
        var (sh, xoff) = PlayerShape(p.State, p.SubState, p.StateCount, p.Xd, p.Yd, p.Info1, _dy1, pl.Ouched);

        if (pl.Ouched <= 0 && p.State == StBegin)
        {
            // Rise out of the ground: clip the viewport bottom to the hero's feet (tempvp.vpyl).
            var clip = new Viewport(_vp.X, _vp.Y, _vp.W, p.Y + p.Yl - _vp.OriginY)
            { OriginX = _vp.OriginX, OriginY = _vp.OriginY };
            shapes.DrawShape(screen, clip, sh, p.X, p.Y + 41 - p.StateCount);
        }
        else
        {
            shapes.DrawShape(screen, _vp, sh, p.X + xoff, p.Y);
            if (DrawsLandingDust(p.Counter, p.State, p.SubState, p.Xd, _dy1, pl.Ouched)) // landing dust
                shapes.DrawShape(screen, _vp, 0x0600 + 37 - p.Counter / 2, p.X + 4, p.Y + 37);
        }
    }

    // ---- Camera (calc_scroll) -------------------------------------------------------------

    private void ApplyScroll(int peeky) => Camera.Follow(_vp, P, peeky);
}
