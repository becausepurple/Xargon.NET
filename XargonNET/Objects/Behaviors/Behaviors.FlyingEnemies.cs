using XargonNET.Assets;

namespace XargonNET.Objects;

public sealed partial class Behaviors
{
    private int Bat(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Bat) + 8;
        switch (msg)
        {
            case Msg.Update:
                p.Counter = (p.Counter + 1) & 7;
                if (++p.StateCount >= 4) p.StateCount = 0;
                if (_w.JustMove(n, p.X + p.Xd, p.Y) == 0) p.Xd = -p.Xd;
                _w.JustMove(n, p.X, p.Y + Ydtab4[p.StateCount]);
                return 1;
            case Msg.Draw: _w.DrawSprite(sh + p.Counter / 2, p.X, p.Y); break;
            case Msg.Touch: if (TouchHurtPlayer(n, z, 0)) return 1; break;
        }
        return 0;
    }

    private int Hopper(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Hopper);
        int mod1 = 0;
        switch (msg)
        {
            case Msg.Update:
                if (p.State == 0)
                {
                    if (++p.Counter > 16) { p.State = 1; _w.SeekPlayer(n, out int dx, out _); p.Xd = dx * 4; p.Yd = -12; mod1 = 1; }
                }
                else
                {
                    mod1 = 1;
                    if (++p.Yd > 12) p.Yd = 10;
                    int nx = p.X + p.Xd, ny = p.Y + p.Yd;
                    if ((_w.TryMove(n, nx, ny) & 3) == 0)
                    {
                        if (p.Yd < 0) p.Yd = 0;
                        else
                        {
                            ny = Floor16(ny) + 16 - p.Yl;
                            _w.TryMove(n, nx, ny);
                            _w.SeekPlayer(n, out int dx, out _);
                            p.Xd = dx; p.State = 0; p.Counter = 0;
                        }
                    }
                }
                return mod1;
            case Msg.Draw:
                sh += p.Xd < 0 ? 3 : 0;
                if (p.State == 1) sh += p.Yd > 0 ? 2 : 1;
                _w.DrawSprite(sh, p.X, p.Y);
                break;
            case Msg.Touch: if (TouchHurtPlayer(n, z, 0)) return 1; break;
        }
        return 0;
    }

    private int Bee(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Bee);
        switch (msg)
        {
            case Msg.Update:
                p.Counter = (p.Counter + 1) & 3;
                if (++p.StateCount >= 4) p.StateCount = 0;
                if (p.Xd != 0) p.SubState = p.Xd;
                if (_w.XrRandom(20) == 0) p.Xd = p.Xd > 0 ? 0 : p.SubState;
                if (_w.XrRandom(40) == 0) { _w.SeekPlayer(n, out int dx, out int dy); p.Xd = dx * 4; p.Yd = dy * 2; }
                if (_w.JustMove(n, p.X + p.Xd, p.Y) == 0) p.Xd = -p.Xd;
                _w.JustMove(n, p.X, p.Y + Ydtab4[p.StateCount] + p.Yd);
                return 1;
            case Msg.Draw:
                sh += p.Xd == 0 ? p.Counter / 2 + 4 : p.Counter / 2 + (p.Xd > 0 ? 2 : 0);
                _w.DrawSprite(sh, p.X, p.Y);
                break;
            case Msg.Touch: if (TouchHurtPlayer(n, z, 0)) return 1; break;
        }
        return 0;
    }

    private int Skull(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Skull) + 2;
        switch (msg)
        {
            case Msg.Update:
                p.Counter = (p.Counter + 1) & 7;
                if (!_w.OnScreen(n)) { _w.KillObj(n); return 1; }
                if (_w.JustMove(n, p.X + p.Xd, p.Y) == 0)
                {
                    _w.AddObj(ObjectKind.HitWall, p.X + (p.Xd > 0 ? 16 : -12), p.Y, 0, 0);
                    _w.KillObj(n);
                    return 1;
                }
                p.Xd--;
                if (p.Xd < -10) p.Xd = -10;
                return 1;
            case Msg.Draw: _w.DrawSprite(sh, p.X, p.Y); break;
            case Msg.Touch: if (z == 0) { _w.Hitplayer(n, 0); _w.KillObj(n); return 1; } break;
        }
        return 0;
    }

    private static readonly int[] XargbotXd = { -8, 8 };

    /// <summary>msg_xargbot (kind 60): the floating wizard. Drifts, bounces off walls, and lobs
    /// bullets; a hit (except a glancing blow on its charged state 2) sends it tumbling (info1=1) in a
    /// burst of fire before it shatters.</summary>
    private int Xargbot(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Xargbot);
        switch (msg)
        {
            case Msg.Update:
                if (p.Info1 == 1)
                {
                    p.Counter = (p.Counter + 1) & 1;
                    if (p.Counter == 0) _w.Obj(_w.AddObj(ObjectKind.Fire, p.X, p.Y - 24, 0, 0)).State = 1; // trailing fire
                    _w.JustMove(n, p.X + XargbotXd[p.Counter], p.Y + p.Yd);
                    if (_w.JustMove(n, p.X, p.Y + p.Yd) == 0 || !_w.OnScreen(n))
                    {
                        _w.Explode1(p.X + 4, p.Y + 4, 5, 0);
                        _w.KillObj(n);
                    }
                    return 1;
                }
                if (_w.XrRandom(15) == 0) { _w.SeekPlayer(n, out int dx, out int dy); p.Xd = dx * 2; p.Yd = dy * 4; }
                if (_w.JustMove(n, p.X + p.Xd, p.Y) == 0) p.Xd = -p.Xd;
                if (_w.JustMove(n, p.X, p.Y + p.Yd) == 0 || !_w.OnScreen(n)) p.Yd = -p.Yd;
                if (_w.XrRandom(20) == 0) { p.Xd = 0; p.SubState = 1; return 1; }
                if (p.Xd < 0) p.SubState = 0;
                else if (p.Xd > 0) p.SubState = 2;
                if (_w.XrRandom(p.State != 2 ? 50 : 40) == 0 && p.Xd != 0)
                    _w.AddObj(ObjectKind.Bullet, p.X - 4 + (p.Xd > 0 ? 16 : 0), p.Y + 18, p.Xd * 3, 0);
                return 1;
            case Msg.Draw: _w.DrawSprite(sh + p.SubState + p.State * 3, p.X, p.Y); break;
            case Msg.Touch:
                if (p.Info1 == 1) return 0;
                TouchHurtPlayer(n, z, 0);
                if ((_w.KindFlags(z) & KWeapon) != 0)
                {
                    if (p.State == 2 && p.StateCount == 0)
                    {
                        p.StateCount = 1;
                        _w.Explode1(p.X, p.Y, 5, 1);
                        ConsumeWeapon(z, true);
                    }
                    else
                    {
                        p.Info1 = 1; p.Yd = 2; p.SubState = 1;
                        _w.AddScore(p.State == 2 ? 130 : KindDefs.Get(ObjectKind.Xargbot).Score, p.X, p.Y);
                        ConsumeWeapon(z, true);
                    }
                }
                return 1;
        }
        return 0;
    }

    /// <summary>msg_ghoul (kind 65): a free-floating wraith that homes on the player, bouncing off
    /// walls. Its shape (and firing) escalates as the player closes in; eight weapon hits dispel it.</summary>
    private int Ghoul(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Ghoul);
        switch (msg)
        {
            case Msg.Update:
                if (p.Info1 >= 8)
                {
                    _w.AddScore(KindDefs.Get(ObjectKind.Ghoul).Score, p.X, p.Y);
                    _w.Explode2(n); _w.KillObj(n); return 1;
                }
                if (p.StateCount > 0) p.StateCount--;
                if (_w.JustMove(n, p.X, p.Y + p.Yd) == 0) p.Yd = -p.Yd;
                if (_w.XrRandom(20) == 0) { _w.SeekPlayer(n, out int dx, out int dy); p.Xd = dx * 2; p.Yd = dy * 2; }
                if (_w.JustMove(n, p.X + p.Xd, p.Y) == 0) p.Xd = -p.Xd;
                int dist = _w.VectDist(n, 0);
                if (dist < 224) p.State = 0;
                if (dist < 192) p.State = 1;
                if (dist < 160) p.State = 2;
                if (dist < 128) p.State = 3;
                if (dist < 96)
                {
                    p.State = 4;
                    if (p.StateCount == 0)
                    {
                        p.StateCount = 30;
                        _w.AddObj(ObjectKind.Bullet, p.X - 4 + (p.Xd > 0 ? 30 : 0), p.Y + 4, p.Xd * 3, 0);
                    }
                }
                return 1;
            case Msg.Draw: _w.DrawSprite(sh + p.State + (p.Xd > 0 ? 0 : 5), p.X, p.Y); break;
            case Msg.Touch:
                TouchHurtPlayer(n, z, 0);
                if ((_w.KindFlags(z) & KWeapon) != 0)
                {
                    p.Info1++;
                    _w.Explode2(n);
                    ConsumeWeapon(z, true);
                }
                return 1;
        }
        return 0;
    }
}
