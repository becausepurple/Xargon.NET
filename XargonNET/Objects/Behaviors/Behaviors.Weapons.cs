using XargonNET.Assets;

namespace XargonNET.Objects;

public sealed partial class Behaviors
{
    private int Rock(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Rock) + 20;
        switch (msg)
        {
            case Msg.Update:
                if (++p.SubState >= 64 || !_w.OnScreen(n)) { _w.KillObj(n); return 1; }
                int target = _w.ClosestKillable(n, 96);
                int ax = 0, ay = 0;
                if (target >= 0) _w.PointVect(target, n, out ax, out ay, 3);
                ay = 1; // faithful: the C overwrites the vertical homing component
                p.Xd = Math.Clamp(p.Xd + ax, -12, 12);
                p.Yd = Math.Clamp(p.Yd + ay, -12, 12);
                p.SubState += _w.TryBreakWall(n, p.X + p.Xd, p.Y + p.Yd) * 10;
                if (_w.JustMove(n, p.X + p.Xd, p.Y) == 0) p.Xd = -p.Xd;
                int c = Floor16(p.Y + p.Yd) + 16 - p.Yl;
                if (_w.JustMove(n, p.X, p.Y + p.Yd) == 0 && (c == p.Y || _w.JustMove(n, p.X, c) == 0)) p.Yd = -p.Yd;
                if (_w.JustMove(n, p.X, p.Y) == 0) _w.KillObj(n);
                return 1;
            case Msg.Draw: _w.DrawSprite(sh, p.X, p.Y); break;
            case Msg.Touch:
                if (z == 0 && p.SubState > 7) { _w.KillObj(n); return 1; }
                if ((_w.KindFlags(z) & KKillable) != 0)
                {
                    _w.KillObj(n);
                    ExplodeVictim(n, z);
                    _w.PlayerKill(z);
                    return 1;
                }
                break;
        }
        return 0;
    }

    private int Laser(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Laser) + 38;
        switch (msg)
        {
            case Msg.Update:
                if (!_w.OnScreen(n)) { _w.KillObj(n); return 1; }
                if (_w.JustMove(n, p.X + p.Xd, p.Y) == 0)
                {
                    _w.AddObj(ObjectKind.HitWall, p.X + (p.Xd > 0 ? 12 : -12), p.Y - 5, 0, 0); // hitwall
                    _w.KillObj(n);
                    return 1;
                }
                p.Xd += p.Xd > 0 ? 1 : -1; // accelerate
                _w.TryBreakWall(n, p.X + p.Xd, p.Y + p.Yd);
                _w.JustMove(n, p.X, p.Y + _w.InputDy * 2); // steer up/down with the arrow keys
                return 1;
            case Msg.Draw: _w.DrawSprite(sh, p.X, p.Y); break;
            case Msg.Touch:
                if ((_w.KindFlags(z) & KKillable) != 0)
                {
                    _w.KillObj(n);
                    ExplodeVictim(n, z);
                    _w.PlayerKill(z);
                    return 1;
                }
                break;
        }
        return 0;
    }

    private int Fireball(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Fireball) + 34;
        switch (msg)
        {
            case Msg.Update:
                if (!_w.OnScreen(n)) { _w.KillObj(n); return 1; }
                p.Counter = (p.Counter + 1) & 3;
                p.Xd += p.Xd > 0 ? 1 : -1;
                _w.TryBreakWall(n, p.X + p.Xd, p.Y + p.Yd);
                if (_w.JustMove(n, p.X + p.Xd, p.Y + p.Yd) == 0)
                {
                    _w.AddObj(ObjectKind.FireExpl, p.X + (p.Xd > 0 ? 12 : -20), p.Y - 5, 0, 0); // fireexpl
                    // the C re-reads board() with pixel coordinates here; the intent is "not a breakwall"
                    if (!_w.IsBreakWallAt(p.X + p.Xd, p.Y + p.Yd)) _w.KillObj(n);
                }
                return (p.Counter & 1) == 0 ? 1 : 0;
            case Msg.Draw: _w.DrawSprite(sh + p.Counter / 2 + (p.Xd > 0 ? 2 : 0), p.X, p.Y); break;
            case Msg.Touch:
                if ((_w.KindFlags(z) & KFireball) != 0) { _w.Explode2(z); _w.PlayerKill(z); }
                else if ((_w.KindFlags(z) & KKillable) != 0)
                {
                    _w.Explode1(_w.Obj(z).X, _w.Obj(z).Y, 5, 0);
                    if (_w.Obj(z).Kind == ObjectKind.Skull) _w.Explode1(_w.Obj(z).X, _w.Obj(z).Y, 5, 1);
                    _w.PlayerKill(z);
                    return 1;
                }
                break;
        }
        return 0;
    }

    private int HitWall(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.HitWall) + 55;
        switch (msg)
        {
            case Msg.Update: if (++p.Counter >= 7 || !_w.OnScreen(n)) _w.KillObj(n); return 1;
            case Msg.Draw: _w.DrawSprite(sh + HitWallFrame(p.Counter), p.X, p.Y); break;
        }
        return 0;
    }

    private int FireExpl(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.FireExpl) + 45;
        switch (msg)
        {
            case Msg.Update: if (++p.Counter >= 9 || !_w.OnScreen(n)) _w.KillObj(n); return 1;
            case Msg.Draw: _w.DrawSprite(sh + FireExplFrame(p.Counter), p.X, p.Y); break;
        }
        return 0;
    }

    private static readonly int[] ExpTab = { 0, 1, 0, 1, 0, 1, 2, 3, 4 };

    /// <summary>msg_fireexpl frame offset: exptab[counter].</summary>
    internal static int FireExplFrame(int counter) => ExpTab[Math.Clamp(counter, 0, 8)];

    /// <summary>msg_hitwall frame offset: counter/2.</summary>
    internal static int HitWallFrame(int counter) => counter / 2;

    /// <summary>msg_bullet (kind 32): the generic enemy shot fired by troll/lizard/xargbot/ghoul/
    /// robot. Flies in a straight line, dies off-screen or on impact. state==1 marks the robot's
    /// larger "energy" bolt (grown to 6×4 on its first update). Touching the swimming hero deals the
    /// "fish" hurt (p_ouch die_fish); touching the normal hero uses hitplayer.</summary>
    private int Bullet(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Bullet);
        switch (msg)
        {
            case Msg.Update:
                if (p.State == 1) { p.State = 2; p.Xl = 6; p.Yl = 4; }
                p.Counter = (p.Counter + 1) & 7;
                p.X += p.Xd; p.Y += p.Yd;
                p.Xd = Math.Clamp(p.Xd, -12, 12);
                if (!_w.OnScreen(n) || (p.Xd == 0 && p.Yd == 0)) { _w.KillObj(n); return 1; }
                if (_w.JustMove(n, p.X + p.Xd, p.Y + p.Yd) == 0) _w.KillObj(n);
                return 1;
            case Msg.Draw:
                _w.DrawSprite(p.State == 0 ? sh + 39 : sh + 41 + p.Counter / 2, p.X, p.Y);
                break;
            case Msg.Touch:
                if (_w.Obj(z).Kind == ObjectKind.HeroSwim)
                {
                    _w.POuch(1, 2 /*die_fish*/);
                    _w.AddObj(ObjectKind.HitHero, _w.Obj(0).X, _w.Obj(0).Y, 0, 0);
                    _w.KillObj(n);
                    return 1;
                }
                if (z == 0) { _w.Hitplayer(n, 0); _w.KillObj(n); return 1; }
                break;
        }
        return 0;
    }
}
