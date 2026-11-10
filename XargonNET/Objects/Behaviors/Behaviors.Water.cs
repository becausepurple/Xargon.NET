using XargonNET.Assets;

namespace XargonNET.Objects;

public sealed partial class Behaviors
{
    /// <summary>msg_searock (obj_searock, kind 48): a static underwater rock (draws when state 0).
    /// Every update it has a 1-in-14 chance of spawning a rising bubble above the rock.</summary>
    private int Searock(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        if (msg == Msg.Update && _w.XrRandom(14) == 0)
        {
            _w.AddObj(ObjectKind.Bubble, p.X + 2, p.Y + 4, 0, 0);
            return 1;
        }
        if (msg == Msg.Draw && p.State == 0) _w.DrawSprite(Table(ObjectKind.SeaRock) + 19, p.X, p.Y);
        return 0;
    }

    /// <summary>msg_mine (obj_mine, kind 4): a bobbing sea-mine that drifts in water and belches
    /// bubbles. Touching the player drowns them and detonates; a weapon hit sprays a fan of bullets.</summary>
    private int Mine(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Mine) + 20;
        switch (msg)
        {
            case Msg.Update:
                if (p.Counter == 8) { p.Counter = 0; p.Yd = -p.Yd; }
                if (_w.Fishdo(n, p.X + p.Xd, p.Y) == 0) p.Xd = -p.Xd;
                if (_w.Fishdo(n, p.X, p.Y + p.Yd) == 0) p.Yd = -p.Yd;
                if (_w.XrRandom(16) == 0) _w.AddObj(ObjectKind.Bubble, p.X + 6, p.Y - 2, 0, 0);
                p.Counter++;
                return 1;
            case Msg.Draw: _w.DrawSprite(sh, p.X, p.Y); break;
            case Msg.Touch:
                if (z == 0)
                {
                    _w.POuch(2, 2 /*die_fish*/);
                    _w.Explode2(n);
                    _w.KillObj(n);
                    return 1;
                }
                if ((_w.KindFlags(z) & KWeapon) != 0)
                {
                    _w.Explode2(n);
                    _w.AddObj(ObjectKind.Bullet, p.X - 4, p.Y + 8, -4, -2);
                    _w.AddObj(ObjectKind.Bullet, p.X - 4, p.Y + 8, -4, 0);
                    _w.AddObj(ObjectKind.Bullet, p.X - 4, p.Y + 8, -4, 2);
                    _w.AddObj(ObjectKind.Bullet, p.X + 11, p.Y + 8, 4, -2);
                    _w.AddObj(ObjectKind.Bullet, p.X + 11, p.Y + 8, 4, 0);
                    _w.AddObj(ObjectKind.Bullet, p.X + 11, p.Y + 8, 4, 2);
                    _w.KillObj(n);
                    _w.KillObj(z);
                    return 1;
                }
                break;
        }
        return 0;
    }

    /// <summary>Common water-wander step: occasionally pick a new (xd,yd) drift, bounce off non-water,
    /// and emit the odd bubble. Returns nothing — mutates the object in place like the vanilla fish.</summary>
    private void FishWander(Obj p, int n, int bubbleRate)
    {
        if (_w.XrRandom(20) == 0)
        {
            p.Xd = _w.XrRandom(3) * 4 - 4;
            if (p.Xd == 0) p.Yd = _w.XrRandom(2) * 4 - 2;
            else p.Yd = _w.XrRandom(3) * 2 - 2;
        }
        if (_w.Fishdo(n, p.X + p.Xd, p.Y) == 0) p.Xd = -p.Xd;
        if (_w.Fishdo(n, p.X, p.Y + p.Yd) == 0) p.Yd = -p.Yd;
        if (_w.XrRandom(bubbleRate) == 0) _w.AddObj(ObjectKind.Bubble, p.X + 6, p.Y - 2, 0, 0);
    }

    /// <summary>msg_eel (obj_eel, kind 67): drifts through water, harmless to shoot but drowns the
    /// player on contact.</summary>
    private int Eel(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Eel) + 12;
        switch (msg)
        {
            case Msg.Update:
                if (++p.Counter >= 10) p.Counter = 0;
                FishWander(p, n, 10);
                return 1;
            case Msg.Draw: _w.DrawSprite(sh + p.Counter / 2 + (p.Xd > 0 ? 5 : 0), p.X, p.Y); break;
            case Msg.Touch:
                if (TouchHurtPlayer(n, z, 1)) return 1;
                break;
        }
        return 0;
    }

    /// <summary>msg_badfish (obj_badfish, kind 68): a water drifter that takes two weapon hits; the
    /// second starts a 20-frame sink-and-die while it keeps settling downward.</summary>
    private int Badfish(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.BadFish) + 6;
        switch (msg)
        {
            case Msg.Update:
                if (p.State >= 2)
                {
                    if (p.StateCount == 0) _w.AddScore(KindDefs.Get(ObjectKind.BadFish).Score, p.X, p.Y);
                    _w.Fishdo(n, p.X, p.Y + 2);
                    if (++p.StateCount >= 20) _w.KillObj(n);
                    return 1;
                }
                p.Counter = (p.Counter + 1) & 7;
                FishWander(p, n, 10);
                return 1;
            case Msg.Draw:
                if (p.State >= 2) _w.DrawSprite(sh + 4 + (p.Xd > 0 ? 5 : 0), p.X, p.Y);
                else _w.DrawSprite(sh + p.Counter / 2 + (p.Xd > 0 ? 5 : 0), p.X, p.Y);
                break;
            case Msg.Touch:
                if (p.State >= 2) return 0;
                TouchHurtPlayer(n, z, 1);
                if ((_w.KindFlags(z) & KWeapon) != 0)
                {
                    p.State++;
                    _w.Explode1(p.X, p.Y, 4, 1);
                    _w.KillObj(z); // faithful: fireball consumed too
                }
                return 1;
        }
        return 0;
    }

    /// <summary>msg_redfish (obj_redfish, kind 83): a water drifter that bursts when it touches the
    /// player (drowning them), harmless to shoot.</summary>
    private int Redfish(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.RedFish) + 22;
        switch (msg)
        {
            case Msg.Update:
                p.Counter = (p.Counter + 1) & 7;
                FishWander(p, n, 14);
                return 1;
            case Msg.Draw: _w.DrawSprite(sh + p.Counter / 2 + (p.Xd > 0 ? 4 : 0), p.X, p.Y); break;
            case Msg.Touch:
                if (z == 0)
                {
                    _w.Hitplayer(n, 1);
                    _w.Explode1(p.X + 4, p.Y + 4, 5, 0);
                    _w.KillObj(n);
                    return 1;
                }
                break;
        }
        return 0;
    }

    /// <summary>msg_seamonster (obj_seamonster, kind 81): a stationary creature that animates and emits
    /// bubbles; lethal (drowning) on contact.</summary>
    private int Seamonster(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.SeaMonster) + 22;
        switch (msg)
        {
            case Msg.Update:
                if (++p.Counter >= 20) p.Counter = 0;
                if (_w.XrRandom(30) == 0) _w.AddObj(ObjectKind.Bubble, p.X + 10, p.Y + 4, 0, 0);
                return 1;
            case Msg.Draw: _w.DrawSprite(sh + p.Counter / 4, p.X, p.Y); break;
            case Msg.Touch:
                if (TouchHurtPlayer(n, z, 1)) return 1;
                break;
        }
        return 0;
    }
}
