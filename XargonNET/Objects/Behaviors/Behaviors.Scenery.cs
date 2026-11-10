using XargonNET.Assets;
using XargonNET.Ui;

namespace XargonNET.Objects;

public sealed partial class Behaviors
{
    /// <summary>msg_text6 (kind 6): 6x6 text from Inside, colour Xd on back Yd; dies when Counter runs out.</summary>
    private int Text6(int n, int msg, int z) => TextObj(n, msg, TextRenderer.Font6, 1);

    /// <summary>msg_text8 (kind 7): 8x8 text from Inside, colour Xd on back Yd; dies when Counter runs out.</summary>
    private int Text8(int n, int msg, int z) => TextObj(n, msg, TextRenderer.Font8, 0);

    private int TextObj(int n, int msg, int font, int dy)
    {
        var p = _w.Obj(n);
        switch (msg)
        {
            case Msg.Update:
                if (p.Counter > 0 && --p.Counter <= 0) { _w.KillObj(n); return 1; }
                break;
            case Msg.Draw:
                _w.DrawText(font, p.X, p.Y + dy, p.Xd, p.Yd, p.Inside);
                break;
        }
        return 0;
    }

    private int Score(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        switch (msg)
        {
            case Msg.Update:
                if (--p.Counter < 0 || !_w.OnScreen(n)) _w.KillObj(n);
                else { p.X += p.Xd; p.Y += p.Yd; p.Yd--; return 1; }
                break;
            case Msg.Draw:
                // fontcolor(&gamevp, 2 + (counter & 3), -1): table 3 is font-flagged, so the colour cycles.
                // (x_obj.c spaces digits 8px apart; the unused routines.c copy uses 4px.)
                string digits = p.State.ToString();
                for (int c = 0; c < digits.Length; c++)
                    _w.DrawFontSprite(0x0300 + digits[c] - '0', p.X + 8 * c, p.Y, 2 + (p.Counter & 3), -1);
                break;
        }
        return 0;
    }

    /// <summary>msg_bubble (kind 66): a rising bubble (krusty spit, searock). Faithful apart from the
    /// fishdo water-containment check (deferred with swim) — here it simply rises until it leaves the
    /// screen or completes its three-frame pop.</summary>
    private int Bubble(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Bubble) + 16;
        switch (msg)
        {
            case Msg.Update:
                if (_w.XrRandom(15) == 0) p.Counter++;
                if (p.Counter > 2 || !_w.OnScreen(n)) { _w.KillObj(n); return 1; }
                if (_w.Fishdo(n, p.X + _w.XrRandom(3) - 1, p.Y - p.Counter - 1) == 0) _w.KillObj(n);
                return 1;
            case Msg.Draw: _w.DrawSprite(sh + p.Counter, p.X, p.Y); break;
        }
        return 0;
    }

    /// <summary>msg_stalag (kind 44): a stalactite that, once triggered (after its xd delay counts
    /// down), falls until it hits the floor and shatters. A player rising into a resting one is
    /// bonked back down; contact hurts.</summary>
    private int Stalag(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        switch (msg)
        {
            case Msg.Update:
                if (p.Yd != 0)
                {
                    if (_w.TryMovey(n, p.X, p.Y + p.Yd) == 0)
                    {
                        _w.Explode1(p.X, p.Y + 10, 5, 1);
                        _w.KillObj(n);
                        return 1;
                    }
                    if ((p.Yd += 2) > 16) { p.Yd = 16; return 1; }
                }
                break;
            case Msg.Draw: _w.DrawSprite(0x4f02, p.X, p.Y); break;
            case Msg.Touch:
                if (z == 0)
                {
                    var p0 = _w.Obj(0);
                    if (p0.Yd < 0 && p.Yd == 0) // jumped up into a hanging stalactite
                    {
                        p0.Y = p.Y + p.Yl;
                        p0.State = 2; // st_jumping
                        p0.Yd = 0;
                        p0.SubState = 2;
                    }
                    _w.Hitplayer(n, 0);
                    return 1;
                }
                break;
            case Msg.Trigger:
                if (p.Xd-- > 0) return 0;
                if (p.Yd == 0)
                {
                    p.Yd = 2;
                    _w.KillObj(z);
                    return 1;
                }
                break;
        }
        return 0;
    }

    /// <summary>msg_boulder (kind 45): a rolling boulder. Falls under gravity; once grounded it rolls
    /// toward the player, reversing at walls. Three weapon hits smash it, and a smashed boulder
    /// (state 3) crushes the player.</summary>
    private int Boulder(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Boulder) + 19;
        const int ptns = GameFlags.PlayerThru | GameFlags.NotStair;
        switch (msg)
        {
            case Msg.Update:
                if (p.State >= 3)
                {
                    _w.Explode1(p.X, p.Y, 7, 1);
                    _w.PlayerKill(n);
                }
                if (_w.Cando(n, p.X, p.Y + 1, ptns) == ptns) // airborne: fall
                {
                    if ((p.Yd += 2) > 12) p.Yd = 12;
                    if (_w.TryMove(n, p.X + p.Xd, p.Y + p.Yd) != 1)
                    {
                        _w.TryMove(n, p.X, Floor16(p.Y + p.Yd - 1) + (16 - KindDefs.Get(ObjectKind.Boulder).Yl));
                        p.Xd = 0;
                    }
                }
                else // grounded: roll toward the player
                {
                    p.Yd = 0;
                    if (p.Xd == 0) { _w.SeekPlayer(n, out int dx, out _); p.Xd = dx * 4; }
                    p.Counter = (p.Counter + (p.Xd > 0 ? 1 : -1)) & 3;
                    if (_w.TryMove(n, p.X + p.Xd, p.Y) != 1) p.Xd = -p.Xd;
                }
                return 1;
            case Msg.Draw: _w.DrawSprite(sh + p.Counter, p.X, p.Y); break;
            case Msg.Touch:
                TouchHurtPlayer(n, z, 0);
                if ((_w.KindFlags(z) & KWeapon) != 0)
                {
                    _w.Explode1(p.X, p.Y, 5, 1);
                    _w.KillObj(z);
                    p.State++;
                    return 1;
                }
                break;
        }
        return 0;
    }

    /// <summary>msg_spear (kind 43): a spring-loaded spike that jabs out of a wall on a fixed cadence
    /// (statecount 0→24). yd picks the jab direction (floor spikes vs ceiling spikes).</summary>
    private int Spear(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Spear) + 7;
        int mod1 = 0;
        switch (msg)
        {
            case Msg.Update:
                switch (p.StateCount)
                {
                    case 0: p.Counter = 0; p.Yl = 4; p.Y += p.Yd * 12; mod1 = 1; break;
                    case 20: p.Counter = 1; p.Yl = 12; p.Y -= p.Yd * 8; mod1 = 1; break;
                    case 22: p.Counter = 2; p.Yl = 20; p.Y -= p.Yd * 8; mod1 = 1; break;
                    case 24: p.StateCount = 0; p.Counter = 0; p.Yl = 4; p.Y += p.Yd * 16; mod1 = 1; break;
                }
                p.StateCount++;
                return mod1;
            case Msg.Draw: _w.DrawSprite(sh + p.Counter + p.Yd * 3, p.X, p.Y); break;
            case Msg.Touch: TouchHurtPlayer(n, z, p.State); return 1;
        }
        return 0;
    }

    /// <summary>msg_masher (kind 15): a ceiling crusher that slams down and retracts on a fixed
    /// cadence (state 1→24), hurting the player while extended.</summary>
    private int Masher(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int mod1 = 0;
        switch (msg)
        {
            case Msg.Update:
                switch (p.State)
                {
                    case 1: p.Counter = 0; p.Yl = 4; mod1 = 1; break;
                    case 20: p.Counter = 1; p.Yl = 12; mod1 = 1; break;
                    case 22: p.Counter = 2; p.Yl = 20; mod1 = 1; break;
                    case 24: p.State = 0; p.Counter = 1; p.Yl = 12; break;
                }
                p.State++;
                return mod1;
            case Msg.Draw: _w.DrawSprite(Table(ObjectKind.Masher) + 4 + p.Counter, p.X, p.Y); break;
            case Msg.Touch: TouchHurtPlayer(n, z, 0); return 1;
        }
        return 0;
    }

    private static readonly int[] BallLtab = { 12, 14, 16, 16 };

    /// <summary>msg_ball (kind 46): a bouncing ball affected by gravity. Heavier balls (higher
    /// counter) tug lighter ones toward them (pointvect flocking). state 1 balls pop when touched.</summary>
    private int Ball(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Ball) + 4;
        switch (msg)
        {
            case Msg.Update:
                if (p.Info1 == 0)
                {
                    p.Xl = BallLtab[p.Counter]; p.Yl = BallLtab[p.Counter]; p.Info1 = 1;
                }
                int ax = p.Xd, ay = 1;
                int closeboll = _w.ClosestBall(n, p.Counter);
                if (closeboll >= 0) _w.PointVect(closeboll, n, out ax, out ay, 4);
                p.Xd = Math.Clamp(p.Xd + ax, -12, 12);
                p.Yd = Math.Clamp(p.Yd + ay, -12, 12);
                if (_w.JustMove(n, p.X + p.Xd, p.Y) == 0) p.Xd = -p.Xd;
                if (_w.JustMove(n, p.X, p.Y + p.Yd) == 0)
                {
                    int c = Floor16(p.Y + p.Yd) + 16 - p.Yl;
                    if (c == p.Y || _w.JustMove(n, p.X, c) == 0)
                    {
                        if (p.State == 1) p.Yd = 0;
                        p.Yd = -p.Yd;
                    }
                }
                return 1;
            case Msg.Draw: _w.DrawSprite(sh + p.Counter, p.X, p.Y); break;
            case Msg.Touch:
                if (z == 0)
                {
                    _w.Hitplayer(n, 0);
                    if (p.State == 1) { _w.Explode2(n); _w.KillObj(n); }
                    return 1;
                }
                break;
        }
        return 0;
    }

    /// <summary>msg_eagle (kind 23): invisible until triggered; after the hero touches it (Info1 2) it
    /// speaks messages State..State+Yd-1 (even = hero face, odd = eagle face), then flies off by Xd.</summary>
    private int Eagle(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        switch (msg)
        {
            case Msg.Update:
                p.StateCount = (p.StateCount + 1) & 15;
                if (p.Info1 < 2) return 0;
                if (p.SubState < p.Yd)
                {
                    if (p.SubState <= 5) _w.DialogMsg(p.State + p.SubState, (p.SubState & 1) == 1);
                    p.SubState++;
                }
                if (p.SubState == p.Yd && (_w.JustMove(n, p.X + p.Xd, p.Y - 1) == 0 || !_w.OnScreen(n)))
                {
                    _w.KillObj(n);
                    return 1;
                }
                return (p.StateCount & 1) == 0 ? 1 : 0;
            case Msg.Draw:
                if (p.Info1 > 0) _w.DrawSprite(Table(ObjectKind.Eagle) + p.StateCount / 2, p.X, p.Y);
                break;
            case Msg.Touch:
                if (p.Info1 == 0 || p.Info1 == 2 || z != 0) return 0;
                p.Info1 = 2;
                return 1;
            case Msg.Trigger:
                // TEXT/sound: snd_play (4,snd_geticon) dropped.
                _w.AddObj(ObjectKind.HitHero, p.X + 5, p.Y + 2, 0, 0);
                _w.AddObj(ObjectKind.Flash, p.X - 8, p.Y - 4, 0, 0);
                _w.AddObj(ObjectKind.Flash, p.X, p.Y + 12, 0, 0);
                _w.AddObj(ObjectKind.Flash, p.X + 12, p.Y + 6, 0, 0);
                p.Info1 = 1;
                ConsumePad(z);
                return 1;
        }
        return 0;
    }

    private int Torch(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Torch) + 12;
        switch (msg)
        {
            case Msg.Update: p.Counter = (p.Counter + 1) & 7; return (p.Counter & 1) == 0 ? 1 : 0;
            case Msg.Draw: _w.DrawSprite(sh + p.Counter / 2, p.X, p.Y); break;
        }
        return 0;
    }

    private int Cloud(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Cloud) + 33;
        switch (msg)
        {
            case Msg.Update:
                if (p.Xd == 0) return 0;
                p.Counter = (p.Counter + 1) & 15;
                if ((p.Counter & 1) != 0 && _w.JustMove(n, p.X + p.Xd, p.Y) == 0) p.Xd = -p.Xd;
                return 1;
            case Msg.Draw: _w.DrawSprite(sh, p.X, p.Y); break;
        }
        return 0;
    }

    private int Flash(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Flash);
        int[] e1 = { 3, 3, 2, 2, 1, 1, 0, 0, 0, 0, 1, 1, 2, 2, 3, 3 };
        int[] e2 = { 0, 0, 1, 1, 2, 2, 3, 3, 3, 3, 2, 2, 1, 1, 0, 0 };
        switch (msg)
        {
            case Msg.Update:
                if (p.Yd == 1 && p.Info1 == 0) { p.Info1 = 1; p.Xl = 14; p.Yl = 15; }
                if (++p.Counter >= 16 || !_w.OnScreen(n)) _w.KillObj(n);
                return 1;
            case Msg.Draw:
                int i = Math.Clamp(p.Counter, 0, 15);
                _w.DrawSprite(p.Yd == 0 ? sh + 59 + e1[i] : sh + 68 + e2[i], p.X, p.Y);
                break;
        }
        return 0;
    }

    private int FragExpl(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.FragExpl) + 50;
        switch (msg)
        {
            case Msg.Update:
                if (p.State == -1) { p.State = -2; p.Yl = 4; }
                if (++p.StateCount >= 40 || !_w.OnScreen(n)) _w.KillObj(n);
                if (++p.Yd > 12) p.Yd = 12;
                _w.MoveObj(n, p.X + p.Xd, p.Y + p.Yd);
                return 1;
            case Msg.Draw:
                _w.DrawSprite(p.State >= 0 ? sh + p.State : 0x2422, p.X, p.Y);
                break;
        }
        return 0;
    }

    private int HitHero(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.HitHero) + 63;
        int[] tab = { 4, 4, 3, 2, 1, 0, 1, 2, 3, 4, 4 };
        switch (msg)
        {
            case Msg.Update: if (++p.Counter >= 11 || !_w.OnScreen(n)) _w.KillObj(n); return 1;
            case Msg.Draw: _w.DrawSprite(sh + tab[Math.Clamp(p.Counter, 0, 10)], p.X, p.Y); break;
        }
        return 0;
    }

    private int EffectsObj(int n, int msg, int z)
    {
        if (msg != Msg.Update) return 0;
        Effects.Update(_w, _w.Obj(n));
        return 0;
    }

    /// <summary>
    /// msg_front (obj_front, kind 72): front-layer decorations — pillars, weeds, signs, etc. Each
    /// state maps to a fixed shape (some in other tables). States 13/14 are the end-boss reactor
    /// (its destruction sequence depends on boss damage and the end-game flow, not yet ported).
    /// </summary>
    private int Front(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Front);
        switch (msg)
        {
            case Msg.Update:
                if (p.State is < 13 or > 14) return 0;
                // Ambient fire licking the reactor casing.
                if (p.Info1 >= 3 && _w.XrRandom(Math.Abs(p.Info1 - 42)) == 0)
                {
                    int span = p.State == 13 ? 36 : 56, vspan = p.State == 13 ? 8 : 32;
                    int f = _w.AddObj(ObjectKind.Fire, p.X + _w.XrRandom(span), p.Y + _w.XrRandom(vspan), 0, 0);
                    _w.Obj(f).State = 1;
                }
                // BOSS: info1>=40 reactor destroyed: dim/undim flicker, explode1+fireexpl bursts, +20000 score, 60-frame countdown, then per-volume ending (counter 0/1/2: next board / gameover=2 / sendtrig+killobj). Also deliberately unwired here (endings plan): the C's Xargon end-of-volume message txt("BEWARE MALVINEOUS! XARGON IS VERY CLOSE!", 3, 0) (x_obj.c:2032) and txt("REACTOR DESTROYED! Press ESC to continue", 7, 0) (x_obj.c:2002)
                return 0;
            case Msg.Draw:
                switch (p.State)
                {
                    case 0: break;
                    case 1: sh += 1; break;
                    case 2: sh += 2; break;
                    case 3: sh = 0x5408; break;
                    case 4: sh = 0x2423; break;
                    case 5: sh = 0x4b01; break;
                    case 6: sh = 0x530f; break;
                    case 7: sh += 3; break;
                    case 8: sh += 4; break;
                    case 9: sh += 5; break;
                    case 10: sh += 6; break;
                    case 11: sh = 0x2424; p.Xl = 32; break;
                    case 12: sh = 0x5310; break;
                    case 13: sh = 0x2603; p.Xl = 44; p.Yl = 45; break;
                    case 14: sh = 0x2604; p.Xl = 64; p.Yl = 64; break;
                    case 15: sh = 0x2605; p.Xl = 32; p.Yl = 10; break;
                    case 16: sh = 0x2606; p.Xl = 66; p.Yl = 10; break;
                }
                if (p.Info1 < 40) _w.DrawSprite(sh, p.X + p.Xd, p.Y + p.Yd);
                break;
            case Msg.Touch:
                if (p.Info1 >= 40 || p.State is < 13 or > 14) return 0;
                TouchHurtPlayer(n, z, 0);
                if ((_w.KindFlags(z) & GameFlags.Weapon) != 0)
                {
                    p.Info1++;
                    // TEXT/sound: snd_play (4,snd_hitenemy4) dropped.
                    int s = p.State == 13 ? 36 : 56, f = p.State == 13 ? 28 : 48;
                    _w.Explode1(p.X + _w.XrRandom(s), p.Y + _w.XrRandom(s), 2, 0);
                    _w.Explode1(p.X + _w.XrRandom(s), p.Y + _w.XrRandom(s), 3, 1);
                    _w.AddObj(ObjectKind.FireExpl, p.X + _w.XrRandom(f), p.Y + _w.XrRandom(f), 0, 0);
                    _w.AddObj(ObjectKind.FireExpl, p.X + _w.XrRandom(f), p.Y + _w.XrRandom(f), 0, 0);
                    _w.KillObj(z);
                    return 1;
                }
                break;
        }
        return 0;
    }

    /// <summary>msg_fire (obj_fire, kind 47): a short-lived flame that burns the player on contact.</summary>
    private int Fire(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Fire);
        switch (msg)
        {
            case Msg.Update:
                if (++p.Counter >= 12 || p.Counter < 0) _w.KillObj(n);
                return (p.Counter & 1) == 0 ? 1 : 0;
            case Msg.Draw:
                _w.DrawSprite(sh + p.Counter / 2 + (p.Yd > 0 ? 6 : 0), p.X, p.Y);
                break;
            case Msg.Touch:
                if (z == 0 && p.State != 1 && !ObjectManager.IsInvincible(_w.Obj(0).State))
                {
                    _w.POuch(2, 0); _w.Explode2(0); p.State = 1; // burned; now safe
                    return 1;
                }
                break;
        }
        return 0;
    }

    private static readonly int[] SpikeTab = { 1, 2, 3, 3, 3, 2, 1, 0 };

    /// <summary>msg_spikes (obj_spikes, kind 59): spikes that periodically extend (counter interval)
    /// and retract, hurting the player while extended.</summary>
    private int Spikes(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        switch (msg)
        {
            case Msg.Update:
                if (p.Info1 == 0)
                {
                    if (++p.StateCount == p.Counter) { p.Info1 = 1; p.StateCount = 0; }
                    else return 1;
                }
                p.State = SpikeTab[p.SubState & 7];
                if (p.State == 0) { p.SubState = 0; p.Info1 = 0; return 1; }
                p.SubState++;
                break;
            case Msg.Draw:
                _w.DrawSprite(Table(ObjectKind.Spikes) + 25 + p.State + p.Yd * 4, p.X, p.Y);
                break;
            case Msg.Touch:
                if (z == 0 && p.State > 0) _w.Hitplayer(n, 0);
                return 1;
        }
        return 0;
    }

    /// <summary>msg_story1/msg_story2 (kinds 85/86): draw-only story-board illustrations.</summary>
    private int Story1(int n, int msg, int z) => Story(n, msg, Table(ObjectKind.Story1));

    private int Story2(int n, int msg, int z) => Story(n, msg, Table(ObjectKind.Story2));

    private int Story(int n, int msg, int baseSh)
    {
        var p = _w.Obj(n);
        if (msg == Msg.Draw) _w.DrawSprite(baseSh + p.State, p.X + p.Xd * 4, p.Y + p.Yd * 4);
        return 0;
    }

    private static readonly int[] MapDemoOfs = { 34, 38 };
    private static readonly int[] MapDemoLen = { 4, 3 };

    /// <summary>
    /// msg_mapdemo (obj_mapdemo, kind 19): the HUD-pinned banner at the viewport top-left. Each update
    /// re-pins it to <c>gamevp.vpox+16, vpoy+10</c> (the C also adds the frame's scrollxd/scrollyd
    /// deltas; the port advances the camera before objects update, so OriginX/Y already hold the
    /// post-scroll origin). Draw: the first draw nudges X by Yd, then tiles table 27 shapes 34..37
    /// (Xd==0) or 38..40 (Xd==1) across at 16 px spacing.
    /// </summary>
    private int MapDemo(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = 0x4000 + Table(ObjectKind.MapDemo); // 0x4000 + 27*256
        switch (msg)
        {
            case Msg.Update:
                p.X = _w.ViewOriginX + 16;
                p.Y = _w.ViewOriginY + 10;
                return 1;
            case Msg.Draw:
                if (p.Info1 == 0) { p.Info1 = 1; p.X += p.Yd; }
                if ((uint)p.Xd < 2)
                    for (int c = 0; c < MapDemoLen[p.Xd]; c++)
                        _w.DrawSprite(sh + MapDemoOfs[p.Xd] + c, p.X + c * 16, p.Y);
                break;
        }
        return 0;
    }

    /// <summary>msg_map (obj_map, kind 88): overworld map markers (level icons, the "you are here"
    /// X). State -1 is the small marker; 6..12 are the larger level badges.</summary>
    private int MapIcon(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Map) + 18, sh2 = Table(ObjectKind.Map) + 16;
        switch (msg)
        {
            case Msg.Update:
                if (p.Info1 == 0)
                {
                    p.Info1 = 1;
                    switch (p.State)
                    {
                        case -1: p.Xl = 16; p.Yl = 16; break;
                        case 6: p.Xl = 68; p.Yl = 52; break;
                        case 7: case 8: case 9: case 10: case 11: case 12: p.Xl = 38; p.Yl = 42; break;
                    }
                }
                break;
            case Msg.Draw:
                if (p.State == -1) _w.DrawSprite(sh2 + p.SubState, p.X + 2, p.Y + 2);
                else _w.DrawSprite(sh + p.State, p.X + p.Xd, p.Y + p.Yd);
                break;
            case Msg.Touch:
                if (z == 0 && p.SubState == 0) p.SubState = 1;
                return 1;
        }
        return 0;
    }
}
