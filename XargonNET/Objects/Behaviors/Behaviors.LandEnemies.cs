using XargonNET.Assets;

namespace XargonNET.Objects;

public sealed partial class Behaviors
{
    private int Grunt(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Grunt);
        int[] die = { 7, 16, 7, 16, 7, 16, 7, 16, 7, 16 };
        int[] std = { 0, 1, 2, 3, 3 };
        switch (msg)
        {
            case Msg.Update:
                if (p.State >= 4)
                    return DyingTick(p, n, ObjectKind.Grunt, 10);
                if (++p.Counter >= 10) p.Counter = 0;
                if ((p.Counter & 1) != 0) return 0;
                if (_w.XrRandom(20) == 0) p.Xd = 0;
                CrawlerAi(p, n, 15, 4, setYd: false);
                return 1;
            case Msg.Draw:
                if (p.State >= 4) sh += die[Math.Clamp(p.StateCount, 0, 9)];
                else if (p.Xd == 0) sh += std[Math.Clamp(p.Counter / 2, 0, 4)] + (p.Xd > 0 ? 9 : 0);
                else sh += p.Counter / 2 + (p.Xd > 0 ? 13 : 4);
                _w.DrawSprite(sh, p.X, p.Y);
                break;
            case Msg.Touch:
                if (p.State >= 4) return 0;
                TouchHurtPlayer(n, z, 0);
                if ((_w.KindFlags(z) & KWeapon) != 0)
                {
                    p.State++;
                    _w.Explode1(p.X + 4, p.Y + 4, 3, 0);
                    _w.Explode1(p.X + 8, p.Y + 6, 4, 1);
                    ConsumeWeapon(z, true); // fireball passes through
                }
                return 1;
        }
        return 0;
    }

    /// <summary>msg_biter (kind 50): a stationary trap jaw that lunges forward on a fixed cadence
    /// (state 0→36), hurting the player while extended. Cannot be killed.</summary>
    private int Biter(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Biter);
        int mod1 = 0;
        switch (msg)
        {
            case Msg.Update:
                switch (p.State)
                {
                    case 0: p.Counter = 0; p.Xl = 10; p.X += p.Xd * 6; mod1 = 1; break;
                    case 27: p.Counter = 1; p.Xl = 24; p.X -= p.Xd * 14; mod1 = 1; break;
                    case 34: p.Counter = 2; mod1 = 1; break;
                    case 36: p.State = 0; p.Counter = 0; p.Xl = 10; p.X += p.Xd * 14; mod1 = 1; break;
                }
                p.State++;
                return mod1;
            case Msg.Draw: _w.DrawSprite(sh + p.Counter + p.Xd * 3, p.X, p.Y); break;
            case Msg.Touch: if (TouchHurtPlayer(n, z, 0)) return 1; break;
        }
        return 0;
    }

    /// <summary>msg_troll (kind 55): a shambling crawler that seeks the player, lobs bullets, and
    /// takes a few weapon hits before dying.</summary>
    private int Troll(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Troll);
        switch (msg)
        {
            case Msg.Update:
                if (p.State >= 4)
                {
                    _w.AddScore(KindDefs.Get(ObjectKind.Troll).Score, p.X, p.Y);
                    _w.AddObj(ObjectKind.HitHero, p.X, p.Y, 0, 0); // hithero
                    _w.KillObj(n);
                }
                if (++p.Counter >= 32) p.Counter = 0;
                if ((p.Counter & 1) != 0) return 0;
                CrawlerAi(p, n, 20, 4);
                if (_w.XrRandom(30) == 0 && p.Xd != 0)
                    _w.AddObj(ObjectKind.Bullet, p.X + 6 + (p.Xd > 0 ? 20 : 0), p.Y + 10, Math.Sign(p.Xd) * 6, 0);
                return 1;
            case Msg.Draw:
                _w.DrawSprite(sh + (p.Xd > 0 ? 0 : 8) + p.Counter / 4, p.X, p.Y);
                break;
            case Msg.Touch:
                TouchHurtPlayer(n, z, 0);
                if ((_w.KindFlags(z) & KWeapon) != 0)
                {
                    p.State++;
                    _w.Explode1(p.X, p.Y, 3, 0);
                    _w.Explode1(p.X + 4, p.Y + 10, 3, 1);
                    ConsumeWeapon(z, true);
                }
                return 1;
        }
        return 0;
    }

    private static readonly int[] BlobTab = { 0, 0, 1, 1, 2, 2, 3, 3 };

    private static readonly int[] BlobLtab = { 4, 5, 6, 7, 11, 10, 9, 8 };

    private static readonly int[] BlobRtab = { 8, 9, 10, 11, 7, 6, 5, 4 };

    /// <summary>msg_blob (kind 56): a floor-bound ooze that creeps back and forth and occasionally
    /// lunges a full body-length left (state 1) or right (state 2), provided solid floor extends that
    /// way. Cannot be killed.</summary>
    private int Blob(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Blob);
        switch (msg)
        {
            case Msg.Update:
                p.Counter = (p.Counter + 1) & 15;
                switch (p.State)
                {
                    case 0:
                        if (p.Counter >= (p.Xd < 0 ? 0 : 12) && p.Counter < (p.Xd < 0 ? 4 : 16))
                            if (_w.Crawl(n, p.Xd, 0) == 0 || _w.XrRandom(40) == 0) p.Xd = -p.Xd;
                        if (_w.XrRandom(50) == 0)
                        {
                            if (p.Xd < 0)
                            {
                                if (_w.StandFloor(n, -78, 0) == 0) return 0;
                                p.State = 1; p.X -= 23;
                            }
                            else
                            {
                                if (_w.StandFloor(n, 78, 0) == 0) return 0;
                                p.State = 2; p.X += 2;
                            }
                            p.Xl = 52; p.Yl = 28; p.Y -= 15; p.Counter = 0;
                        }
                        return 1;
                    case 1:
                        if (p.Counter == 8) p.X -= 52;
                        if (p.Counter > 14) { p.Xl = 30; p.Yl = 13; p.Y += 15; p.X -= 2; p.Counter = 0; p.State = 0; }
                        return 1;
                    case 2:
                        if (p.Counter == 8) p.X += 52;
                        if (p.Counter > 14) { p.Xl = 30; p.Yl = 13; p.Y += 15; p.X += 23; p.Counter = 12; p.State = 0; }
                        break;
                }
                return 1;
            case Msg.Draw:
                sh += p.State switch { 1 => BlobLtab[p.Counter / 2], 2 => BlobRtab[p.Counter / 2], _ => BlobTab[p.Counter / 2] };
                _w.DrawSprite(sh, p.X, p.Y);
                break;
            case Msg.Touch: if (TouchHurtPlayer(n, z, 0)) return 1; break;
        }
        return 0;
    }

    /// <summary>msg_lizard (kind 58): a quick crawling spitter. Seeks the player, fires bullets, and
    /// cannot be killed (only avoided).</summary>
    private int Lizard(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Lizard);
        switch (msg)
        {
            case Msg.Update:
                p.Counter = (p.Counter + 1) & 7;
                CrawlerAi(p, n, 20, 2);
                if (_w.XrRandom(55) == 0 && p.Xd != 0)
                    _w.AddObj(ObjectKind.Bullet, p.X - 4 + (p.Xd > 0 ? 16 : 0), p.Y + 4, p.Xd * 4, 0);
                return 1;
            case Msg.Draw: _w.DrawSprite(sh + p.Counter / 2 + (p.Xd < 0 ? 4 : 0), p.X, p.Y); break;
            case Msg.Touch: if (TouchHurtPlayer(n, z, 0)) return 1; break;
        }
        return 0;
    }

    /// <summary>msg_krusty (kind 64): a crusty crawler that blows bubbles and crumbles after a few
    /// hits (state 3), then detonates. Touch uses the "fish" hurt mode.</summary>
    private int Krusty(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Krusty);
        switch (msg)
        {
            case Msg.Update:
                if (p.State >= 3)
                {
                    _w.Explode1(_w.Obj(z).X, _w.Obj(z).Y, 5, 0); // z==0 in update: burst at the player
                    _w.PlayerKill(n);
                    return 1;
                }
                if (++p.Counter >= 12) p.Counter = 0;
                if (_w.XrRandom(30) == 0) { _w.SeekPlayer(n, out int dx, out _); p.Xd = dx; p.Yd = 0; }
                if (_w.Crawl(n, p.Xd, 0) == 0) p.Xd = -p.Xd;
                if (_w.XrRandom(12) == 0) _w.AddObj(ObjectKind.Bubble, p.X + 6, p.Y - 2, 0, 0); // bubble
                return 1;
            case Msg.Draw: _w.DrawSprite(sh + p.Counter / 2 + (p.Xd < 0 ? 6 : 0), p.X, p.Y); break;
            case Msg.Touch:
                if (p.State >= 3) return 0;
                TouchHurtPlayer(n, z, 1);
                if ((_w.KindFlags(z) & KWeapon) != 0)
                {
                    p.State++;
                    _w.Explode1(p.X, p.Y, 4, 1);
                    _w.KillObj(z); // the crust shatters any weapon, fireball included
                }
                return 1;
        }
        return 0;
    }

    /// <summary>msg_creeper (kind 75): a low crawler that seeks the player; three hits fold it into a
    /// death pose that lingers (statecount) before vanishing.</summary>
    private int Creeper(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Creeper);
        switch (msg)
        {
            case Msg.Update:
                if (p.State >= 3)
                    return DyingTick(p, n, ObjectKind.Creeper, 20);
                if (++p.Counter >= 12) p.Counter = 0;
                CrawlerAi(p, n, 30, 1);
                return 1;
            case Msg.Draw:
                if (p.State >= 3) _w.DrawSprite(sh + p.StateCount / 4 + 6, p.X, p.Y);
                else _w.DrawSprite(sh + p.Counter / 4 + (p.Xd > 0 ? 3 : 0), p.X, p.Y);
                break;
            case Msg.Touch:
                if (p.State >= 3) return 0;
                TouchHurtPlayer(n, z, 0);
                if ((_w.KindFlags(z) & KWeapon) != 0)
                {
                    p.State++;
                    if (p.State >= 3) { p.Xl = 20; p.Yl = 36; p.X += 5; p.Y -= 22; }
                    _w.Explode1(p.X + 8, p.Y + 4, 4, 1);
                    ConsumeWeapon(z, true);
                }
                return 1;
        }
        return 0;
    }

    /// <summary>msg_spider (kind 79): a scuttling crawler that darts toward the player; two hits
    /// pop it.</summary>
    private int Spider(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Spider);
        switch (msg)
        {
            case Msg.Update:
                if (++p.Counter >= 18) p.Counter = 0;
                if (p.State >= 2)
                {
                    _w.AddScore(KindDefs.Get(ObjectKind.Spider).Score, p.X, p.Y);
                    _w.KillObj(n);
                }
                CrawlerAi(p, n, 40, 1);
                return 1;
            case Msg.Draw: _w.DrawSprite(sh + p.Counter / 2 + (p.Xd < 0 ? 9 : 0), p.X, p.Y); break;
            case Msg.Touch:
                TouchHurtPlayer(n, z, 0);
                if ((_w.KindFlags(z) & KWeapon) != 0)
                {
                    p.State++;
                    _w.Explode1(p.X + 30, p.Y + 4, 5, 0);
                    ConsumeWeapon(z, true);
                }
                return 1;
        }
        return 0;
    }

    /// <summary>msg_robot (kind 82): an armoured walker that fires energy bolts (bullet state 1) and
    /// explodes after three hits.</summary>
    private int Robot(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Robot) + 14;
        switch (msg)
        {
            case Msg.Update:
                if (p.State >= 3)
                {
                    _w.AddScore(KindDefs.Get(ObjectKind.Robot).Score, p.X, p.Y);
                    _w.KillObj(n);
                }
                if (++p.Counter >= 16) p.Counter = 0;
                if ((p.Counter & 1) != 0) return 0;
                CrawlerAi(p, n, 40, 4);
                if (_w.XrRandom(50) == 0 && p.Xd != 0)
                    _w.Obj(_w.AddObj(ObjectKind.Bullet, p.X - 3 + (p.Xd > 0 ? 28 : 0), p.Y + 12, Math.Sign(p.Xd) * 4, 0)).State = 1;
                return 1;
            case Msg.Draw:
                _w.DrawSprite(sh + (p.Xd > 0 ? 0 : 4) + p.Counter / 4, p.X, p.Y);
                break;
            case Msg.Touch:
                if (TouchHurtPlayer(n, z, 0)) return 1;
                if ((_w.KindFlags(z) & KWeapon) != 0)
                {
                    p.State++;
                    _w.Explode2(n);
                    ConsumeWeapon(z, true);
                }
                break;
        }
        return 0;
    }

    private static readonly int[,] CentMidtab =
    {
        { 1, 2, 3, 4, 5, 6 }, { 2, 3, 4, 5, 6, 1 }, { 3, 4, 5, 6, 1, 2 },
        { 4, 5, 6, 1, 2, 3 }, { 5, 6, 1, 2, 3, 4 }, { 6, 1, 2, 3, 4, 5 },
    };

    /// <summary>msg_centipede (obj_centipede, kind 52): a multi-segment crawler. Each weapon hit lops
    /// off eight pixels of length (spawning a bouncing leg fragment) until it is short enough to die.</summary>
    private int Centipede(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Centipede);
        switch (msg)
        {
            case Msg.Update:
                if (++p.Counter >= 12) p.Counter = 0;
                CrawlerAi(p, n, 30, 1, setYd: false);
                return 1;
            case Msg.Draw:
                if (p.Xd > 0) // going right
                {
                    _w.DrawSprite(sh + 8, p.X + p.Xl - 16, p.Y + (p.Counter < 4 ? 1 : 0)); // head
                    for (int s = 1; s <= (p.Xl - 28) / 8; s++)
                        _w.DrawSprite(sh + CentMidtab[s - 1, p.Counter / 2] + 8, p.X + 4 + s * 8, p.Y + 5);
                    _w.DrawSprite(sh + 15, p.X, p.Y + (p.Counter > 8 ? 6 : 7)); // tail
                }
                else // going left
                {
                    _w.DrawSprite(sh, p.X, p.Y + (p.Counter < 4 ? 1 : 0)); // head
                    for (int s = 1; s <= (p.Xl - 28) / 8; s++)
                        _w.DrawSprite(sh + CentMidtab[s - 1, p.Counter / 2], p.X + 8 * (s + 1), p.Y + 5);
                    _w.DrawSprite(sh + 7, p.X + p.Xl - 12, p.Y + (p.Counter > 8 ? 6 : 7)); // tail
                }
                break;
            case Msg.Touch:
                TouchHurtPlayer(n, z, 0);
                if ((_w.KindFlags(z) & KWeapon) != 0)
                {
                    p.Xl -= 8;
                    _w.KillObj(z); // faithful: fireball is consumed too
                    int fx = p.X + (p.Xd > 0 ? p.Xl - 24 : 16);
                    if (p.Xl < 37)
                    {
                        for (int c = 0; c < 7; c++)
                            _w.AddObj(ObjectKind.CentExpl, fx, p.Y + 5, _w.XrRandom(7) - 3, _w.XrRandom(4) - 10);
                        _w.Explode1(_w.Obj(z).X, _w.Obj(z).Y, 4, 0);
                        _w.PlayerKill(n);
                    }
                    else
                    {
                        _w.AddObj(ObjectKind.CentExpl, fx, p.Y + 5, _w.XrRandom(7) - 3, _w.XrRandom(4) - 10);
                    }
                }
                return 1;
        }
        return 0;
    }

    /// <summary>msg_centexpl (obj_centexpl, kind 10): a bouncing centipede leg fragment flung on hit.
    /// It is lethal to touch (counts as an enemy) and expires after a short life or off-screen.</summary>
    private int Centexpl(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.CentExpl);
        switch (msg)
        {
            case Msg.Update:
                if (++p.StateCount >= 50 || !_w.OnScreen(n)) _w.KillObj(n);
                if (++p.Yd > 12) p.Yd = 12;
                p.State = _w.XrRandom(6) + (p.Xd > 0 ? 9 : 1);
                if (_w.TryMovey(n, p.X + p.Xd, p.Y + p.Yd) == 0) p.Yd = -p.Yd;
                return 1;
            case Msg.Draw: _w.DrawSprite(sh + p.State, p.X, p.Y); break;
            case Msg.Touch:
                if (z != 0) return 0;
                _w.AddObj(ObjectKind.Flash, p.X - 3, p.Y - 1, 0, 1);
                _w.PlayerKill(n);
                return 1;
        }
        return 0;
    }

    private static readonly int[] AlienDie =
        { 18, 18, 19, 19, 20, 20, 18, 19, 20, 18, 19, 20, 18, 19, 20, 20, 20, 20, 20, 20 };

    /// <summary>msg_alien (obj_alien, kind 53): a wandering crawler that lunges toward the player. Three
    /// weapon hits start a 20-frame death animation before it awards score and dies.</summary>
    private int Alien(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Alien);
        switch (msg)
        {
            case Msg.Update:
                if (p.State >= 3)
                    return DyingTick(p, n, ObjectKind.Alien, 20);
                if (++p.Counter >= 20) p.Counter = 0;
                if (++p.Info1 >= 32) p.Info1 = 0;
                if ((p.Counter & 1) != 0) return 0;
                if (p.Xd != 0) p.SubState = p.Xd;
                if (_w.XrRandom(20) == 0) p.Xd = p.Xd > 0 ? 0 : p.SubState;
                CrawlerAi(p, n, 15, 4);
                return 1;
            case Msg.Draw:
                if (p.State >= 3) sh += AlienDie[p.StateCount];
                else if (p.Xd == 0) sh += p.Info1 / 4 + 10;
                else sh += p.Counter / 4 + (p.Xd > 0 ? 0 : 5);
                _w.DrawSprite(sh, p.X, p.Y);
                break;
            case Msg.Touch:
                if (p.State >= 3) return 0;
                TouchHurtPlayer(n, z, 0);
                if ((_w.KindFlags(z) & KWeapon) != 0)
                {
                    p.State++;
                    _w.Explode1(p.X, p.Y, 4, 0);
                    _w.Explode1(p.X + 8, p.Y + 4, 4, 1);
                    ConsumeWeapon(z, true);
                }
                return 1;
        }
        return 0;
    }

    /// <summary>msg_leech (obj_leech, kind 54, x_obj2.c:199): a slow floor crawler that pauses
    /// (statecount ticks) and seeks the player. Every weapon hit bumps state; the hit with state already
    /// at 3 or more (the fourth) kills it for score. Fireballs pass through. Touch always returns 1.</summary>
    private int Leech(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Leech);
        switch (msg)
        {
            case Msg.Update:
                p.Counter = (p.Counter + 1) & 7;
                if ((p.Counter & 1) != 0) return 0;
                bool resume = p.StateCount == 0;
                p.StateCount = (short)(p.StateCount - 1); // 16-bit int in the C (post-decrement)
                if (resume) p.Xd = p.SubState;
                if (p.Xd != 0) p.SubState = p.Xd;
                if (_w.XrRandom(20) == 0) { p.Xd = 0; p.StateCount = 2; return 0; }
                if (_w.XrRandom(40) == 0)
                {
                    _w.SeekPlayer(n, out int dx, out int dy);
                    p.Xd = dx * 4; p.Yd = dy;
                }
                if (_w.Crawl(n, p.Xd, 0) == 0) p.Xd = -p.Xd;
                return 1;
            case Msg.Draw:
                sh += LeechFrame(p.Xd, p.SubState, p.Counter);
                _w.DrawSprite(sh, p.X, p.Y);
                break;
            case Msg.Touch:
                TouchHurtPlayer(n, z, 0); // falls through to the weapon test (the hero is no weapon)
                if ((_w.KindFlags(z) & KWeapon) != 0)
                {
                    if (p.State >= 3)
                    {
                        _w.Explode1(p.X, p.Y + 2, 5, 0);
                        _w.Explode1(p.X + 8, p.Y + 4, 6, 1);
                        _w.PlayerKill(n);
                    }
                    else
                    {
                        _w.Explode1(p.X, p.Y + 2, 3, 0);
                        _w.Explode1(p.X + 8, p.Y + 4, 4, 1);
                    }
                    ConsumeWeapon(z, true); // fireball passes through
                    p.State++;
                }
                return 1; // unconditional in the C (also for the hero)
        }
        return 0;
    }

    /// <summary>Leech shape offset: idle (xd == 0) shows 4 (last heading left) or 9; moving shows
    /// counter/2 plus 0 (left) or 5 (right).</summary>
    internal static int LeechFrame(int xd, int subState, int counter) =>
        xd == 0 ? (subState < 0 ? 4 : 9) : counter / 2 + (xd < 0 ? 0 : 5);

    private static readonly int[] ClimbDwn = { 7, 6, 5, 4, 3, 2, 1, 0 };

    /// <summary>msg_climber (obj_climber, kind 78): rides up and down a vine, reversing at the ends.
    /// On contact it shoves the player off to one side and resets their state, then deals a hit.</summary>
    private int Climber(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Climber);
        int yl = KindDefs.Get(ObjectKind.Climber).Yl;
        switch (msg)
        {
            case Msg.Update:
                if (p.Info1 == 0)
                {
                    p.Info1 = 1;
                    p.X += 4;
                    if (p.Yd == 0) p.Yd = 2;
                }
                p.Counter = (p.Counter + 1) & 15;
                if (p.Yd > 0) // going down the vine
                {
                    if (_w.ObjDo(n, p.X, p.Y + p.Yd + yl, GameFlags.NotVine) == 0)
                        _w.MoveObj(n, p.X, p.Y + p.Yd);
                    else p.Yd = -p.Yd;
                }
                else
                {
                    if (_w.ObjDo(n, p.X, p.Y + p.Yd - yl, GameFlags.NotVine) == 0)
                        _w.MoveObj(n, p.X, p.Y + p.Yd);
                    else p.Yd = -p.Yd;
                }
                return 1;
            case Msg.Draw:
                sh += p.Yd > 0 ? ClimbDwn[p.Counter / 2] : p.Counter / 2;
                _w.DrawSprite(sh, p.X, p.Y);
                break;
            case Msg.Touch:
                if (z == 0)
                {
                    var pl = _w.Obj(0);
                    if (!ObjectManager.IsInvincible(pl.State))
                    {
                        int k = _w.JustMove(0, pl.X - 16, pl.Y);
                        if (k == 0) _w.JustMove(0, pl.X + 16, pl.Y);
                        pl.State = 0;
                        pl.SubState = 0;
                    }
                    _w.Hitplayer(n, 0);
                    return 1;
                }
                break;
        }
        return 0;
    }

    /// <summary>msg_turret (obj_turret, kind 74): aims its barrel toward the player and fires an energy
    /// bolt every 36 frames. A trigger toggles it on/off; weapon hits spark but don't destroy it.</summary>
    private int Turret(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Turret) + 11;
        switch (msg)
        {
            case Msg.Update:
                if (p.State == 1) return 0;
                int mod1 = 0;
                _w.PointVect(0, n, out int dx, out _, 3);
                if (dx < -2) dx = -2;
                if (dx > 2) dx = 2;
                if (dx != p.Xd) { p.Xd = dx; mod1 = 1; }
                if (p.StateCount == 36)
                {
                    p.StateCount = 0;
                    int newx, newy;
                    switch (p.Xd)
                    {
                        case -2: newx = p.X + 1; newy = p.Y + 6; break;
                        case -1: newx = p.X + 5; newy = p.Y + 9; break;
                        case 0: newx = p.X + 8; newy = p.Y + 10; break;
                        case 1: newx = p.X + 11; newy = p.Y + 11; break;
                        default: newx = p.X + 15; newy = p.Y + 8; break;
                    }
                    _w.Obj(_w.AddObj(ObjectKind.Bullet, newx, newy, p.Xd, 2)).State = 1;
                    mod1 = 1;
                }
                p.StateCount++;
                return mod1;
            case Msg.Draw: _w.DrawSprite(sh + p.Xd, p.X, p.Y); break;
            case Msg.Touch:
                if ((_w.KindFlags(z) & KWeapon) != 0)
                {
                    _w.Explode1(p.X + _w.XrRandom(16), p.Y + 8, 4, 1);
                    ConsumeWeapon(z, true);
                    return 1;
                }
                if (p.State == 1) return 0;
                TouchHurtPlayer(n, z, 0);
                return 1;
            case Msg.Trigger: p.State = 1 - p.State; return 1;
        }
        return 0;
    }
}
