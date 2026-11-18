using XargonNET.Assets;

namespace XargonNET.Objects;

public sealed partial class Behaviors
{
    private static readonly int[] BossYd = { 1, 2, 1, 0, -1, -2, -1, 0 };
    private static readonly int[] BossYDie = { 4, -2, 6, -4, 8, -6, 8, -6 };
    private static readonly int[] BossXDie = { 4, -4, 4, -4, 4, -4, 4, -4 };

    /// <summary>msg_xargon (kind 87, x_obj2.c:867): the end boss. Hovers at a distance bobbing and spitting
    /// skulls; 50 weapon hits destroy it (shaking, bursting, then the game is won once ESC is pressed).</summary>
    private int Xargon(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Xargon);
        switch (msg)
        {
            case Msg.Update:
                p.Counter = (p.Counter + 1) & 15;
                if (p.State > 0) p.State--;
                if (p.Info1 > 0) p.Info1--;
                if (p.SubState >= 50) { BossDying(p, n); return 1; }
                if (_w.XrRandom(20) == 0) { _w.SeekPlayer(n, out int dx, out _); p.Xd = dx * 4; }
                int dist = _w.VectDist(n, 0);
                if (dist < 80 && p.Xd < 0) p.Xd = -p.Xd;
                if (dist > 256 && p.Xd >= 0) p.Xd = -p.Xd;
                if (_w.JustMove(n, p.X + p.Xd, p.Y) == 0) p.Xd = -p.Xd;
                _w.JustMove(n, p.X, p.Y + BossYd[p.Counter / 2]);
                if (_w.XrRandom(20) == 0 && p.Info1 == 0 && p.Yd == 0)
                {
                    p.Info1 = 15;
                    _w.AddObj(ObjectKind.Skull, p.X - 10, p.Y + 50, -2, 0);
                    p.State = 5; // sound: snd_play (2,snd_enemyfire)
                }
                return 1;
            case Msg.Draw:
                if (p.StateCount < 59) _w.DrawSprite(sh + (p.State > 0 ? 1 : 0), p.X, p.Y);
                break;
            case Msg.Touch:
                if (p.SubState >= 50) return 0;
                if (z == 0) _w.Hitplayer(n, 0);
                if ((_w.KindFlags(z) & GameFlags.Weapon) != 0)
                {
                    p.SubState++; // sound: snd_play (4,snd_hitenemy4)
                    _w.Explode1(p.X + _w.XrRandom(55), p.Y + _w.XrRandom(60), 2, 1);
                    _w.Explode1(p.X + _w.XrRandom(55), p.Y + _w.XrRandom(60), 3, 1);
                    _w.AddObj(ObjectKind.FireExpl, p.X + _w.XrRandom(44), p.Y + _w.XrRandom(52), 0, 0);
                    _w.AddObj(ObjectKind.FireExpl, p.X + _w.XrRandom(44), p.Y + _w.XrRandom(52), 0, 0);
                    _w.KillObj(z);
                }
                return 1;
        }
        return 0;
    }

    /// <summary>The destroyed-boss branch of msg_xargon's update (x_obj2.c:880-910).</summary>
    private void BossDying(Obj p, int n)
    {
        const int StStill = 1; // player state (private in Player.cs)
        var screen = _w.Screen;
        p.State = 3;
        _w.JustMove(n, p.X + BossXDie[p.Counter / 2], p.Y + BossYDie[p.Counter / 2]);
        if (p.Counter / 2 == 0) screen.Dim(); else screen.Undim();
        screen.SetColor6(250, 20, 20, 23);
        _w.Explode1(p.X + _w.XrRandom(54), p.Y + _w.XrRandom(60), 1, 1);
        _w.Explode1(p.X + _w.XrRandom(54), p.Y + _w.XrRandom(60), 1, 0);
        _w.AddObj(ObjectKind.FireExpl, p.X + _w.XrRandom(60) - 15, p.Y + _w.XrRandom(68) - 15, 0, 0);
        _w.AddObj(ObjectKind.HitHero, p.X + _w.XrRandom(60) - 12, p.Y + _w.XrRandom(68) - 17, 0, 0);
        if (p.StateCount == 0)
        {
            _w.Obj(0).State = StStill;
            // sound: snd_play (5,snd_enemykill1)
            _w.AddScore(KindDefs.Get(ObjectKind.Xargon).Score, p.X, p.Y);
            _w.Pl.Health = 5; // statmodflg|=mod_screen: the port redraws the status bar every frame
        }
        if (p.StateCount == 58)
        {
            // sound: snd_play (4,snd_enemykill2)
            _w.Txt("XARGON IS DEAD! Press ESC to continue", 7, 0);
        }
        if (++p.StateCount >= 60)
        {
            screen.Undim();
            screen.SetColor6(250, 20, 20, 23);
            // sound: snd_play (5,snd_enemykill1)
            _w.RequestEscWait(() => _w.GameWon = true);
        }
    }
}
