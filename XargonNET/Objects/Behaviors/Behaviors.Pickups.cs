using XargonNET.Text;
using XargonNET.Assets;

namespace XargonNET.Objects;

public sealed partial class Behaviors
{
    /// <summary>Score awarded for collecting a bonus of <paramref name="state"/> (msg_bonus kindscore table).</summary>
    internal static int BonusScore(int state, int epicCount) => state switch
    {
        3 => 120,
        7 => 200,
        12 => epicCount == 3 ? 4000 : 100,
        18 => 75,
        19 => 2000,
        23 => 50,
        28 => 1000,
        1 or 2 or 6 or 8 or 9 or 10 or 11 or 14 => 100,
        _ => 25,
    };

    /// <summary>inv_shape: shape offset (from table+15) drawn for each inventory-kind token.</summary>
    internal static readonly int[] InvShape = { 0, 1, 0, 0, 2, 3, 0, 5, 6, 7, 0, 0, 0, 23, 24, 25, 26 };

    private int Bonus(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Bonus);
        switch (msg)
        {
            case Msg.Draw: _w.DrawSprite(sh + p.State, p.X + (p.Xd == 1 ? 4 : 0), p.Y); break;
            case Msg.Touch:
                if (z != 0) return 0;
                int epic = _w.InvCount(Inv.Epic);
                int score = BonusScore(p.State, epic); // evaluated before inventory changes
                switch (p.State)
                {
                    case 1:
                        while (_w.InvCount(Inv.Laser) < 5) _w.AddInv(Inv.Laser);
                        _w.Screen.SetColor6(162, 32, 0, 0); _w.Screen.SetColor6(163, 38, 0, 0);
                        _w.Screen.SetColor6(164, 44, 0, 0); _w.Screen.SetColor6(165, 50, 0, 0);
                        _w.Screen.SetColor6(166, 56, 0, 0); _w.Screen.SetColor6(167, 63, 0, 0);
                        _w.Txt("You now have RAPID FIRE", 3, 0);
                        break;
                    case 2: case 6: case 8: case 14:
                        if (_w.Hints.Fruit) { _w.Hints.Fruit = false; _w.TextMsg(38); } // first fruit: dotextmsg(38)
                        if (_w.Pl.Fruit < 16) _w.Pl.Fruit++;
                        _w.Pl.FruitIcon = p.State switch { 2 => 0, 6 => 1, 8 => 2, _ => 3 };
                        break;
                    case 9: if (epic == 0) _w.AddInv(Inv.Epic);
                        _w.Txt("HINT: Collect EPIC poolballs in order for bonus", 3, 0);
                        _w.AddObj(ObjectKind.Flash, p.X - 1, p.Y - 3, 0, 0); break;
                    case 10: if (epic == 1) _w.AddInv(Inv.Epic);
                        _w.AddObj(ObjectKind.Flash, p.X - 1, p.Y - 3, 0, 0); break;
                    case 11: if (epic == 2) _w.AddInv(Inv.Epic);
                        _w.AddObj(ObjectKind.Flash, p.X - 1, p.Y - 3, 0, 0); break;
                    case 12:
                        if (epic == 3) _w.Txt("Special EPIC bonus: 4,000 Points!", 3, 0);
                        _w.AddObj(ObjectKind.Flash, p.X - 1, p.Y - 3, 0, 0); break;
                    case 13: // journal entry
                        _w.TextMsg(p.Counter, PopupStyle.Dialog); // dotextmsg(counter, dialog) journal page
                        _w.KillObj(n); return 0;
                    case 28:
                        for (int c = 0; c < 5; c++) if (_w.InvCount(Inv.Fire) < 9) _w.AddInv(Inv.Fire);
                        _w.Txt("You now have 5 FIREBALLS--Use them wisely", 3, 0);
                        break;
                }
                if (p.State < 9 || p.State > 12)
                    _w.AddObj(ObjectKind.Flash, p.X + (p.Xd == 1 ? 5 : 1), p.Y, 0, 1);
                _w.PlayerKill(n, score); // playerkill with kindscore[] set per state
                return 1;
        }
        return 0;
    }

    /// <summary>msg_box (kind 26): a floating crate that falls when triggered (substate 1) and, when
    /// struck by a weapon, breaks open to spill its contents (keyed by state). The player touching a
    /// landed box shows the one-time "you can break boxes" popup (first_box / dotextmsg 3).</summary>
    private int Box(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        const int ptns = GameFlags.PlayerThru | GameFlags.NotStair;
        switch (msg)
        {
            case Msg.Update:
                if (p.Yd == -1) { p.SubState = 1; p.Yd = 0; return 0; }
                if (p.SubState == 0) return 0;
                if (_w.Cando(n, p.X, p.Y + 1, ptns) == ptns)
                {
                    if (p.Yd++ > 9) p.Yd = 9;
                    if (_w.TryMove(n, p.X, p.Y + p.Yd) != 1)
                        _w.TryMove(n, p.X, Floor16(p.Y + p.Yd - 1) + (16 - KindDefs.Get(ObjectKind.Box).Yl));
                    return 1;
                }
                p.Yd = 0;
                break;
            case Msg.Draw:
                if (p.SubState == 1) _w.DrawSprite(Table(ObjectKind.Box) + 24 + p.Xd, p.X, p.Y);
                break;
            case Msg.Touch:
                // Player touching a landed box: show the one-time "you can break boxes" hint (dotextmsg 3).
                if (z == 0 && _w.Hints.Box && p.SubState == 1) { _w.Hints.Box = false; _w.TextMsg(3); }
                if ((_w.KindFlags(z) & KWeapon) != 0 && p.SubState == 1)
                {
                    BoxReward(p);
                    _w.Explode1(p.X, p.Y, 4, 0);
                    if (p.Yd != 0) _w.AddScore(50, p.X, p.Y);
                    _w.KillObj(n);
                    ConsumeWeapon(z, true);
                    return 1;
                }
                break;
            case Msg.Trigger:
                p.SubState = 1;
                ConsumePad(z); // consume the pad
                return 1;
        }
        return 0;
    }

    /// <summary>Spill a broken box's contents (msg_box state table): fruit (bonus), keys, token,
    /// heart, emerald, or a wall of flame.</summary>
    private void BoxReward(Obj p)
    {
        void Bonus(int state) => _w.Obj(_w.AddObj(ObjectKind.Bonus, p.X, p.Y, 0, 0)).State = state;
        void Key(int state) => _w.Obj(_w.AddObj(ObjectKind.Key, p.X, p.Y, 0, 0)).State = state;
        switch (p.State)
        {
            case 0: _w.AddObj(ObjectKind.Heart, p.X, p.Y, 0, 0); break; // heart
            case 1: Bonus(2); break;  // grapes
            case 2: Bonus(6); break;  // cherries
            case 3: Bonus(8); break;  // strawberry
            case 4: Bonus(14); break; // orange
            case 5: Bonus(3); break;  // 1.44 disk
            case 6: _w.AddObj(ObjectKind.Key, p.X, p.Y, 0, 0); break; // key 0
            case 7: Key(1); break;
            case 8: Key(2); break;
            case 9: Key(3); break;
            case 10: _w.Obj(_w.AddObj(ObjectKind.Token, p.X, p.Y, 0, 0)).State = 8; break; // token
            case 11: _w.AddObj(ObjectKind.Emerald, p.X, p.Y, 0, 0); break; // emerald
            case 12:
                for (int dx = -32; dx <= 32; dx += 16) _w.AddObj(ObjectKind.Fire, p.X + dx, p.Y - 16, 0, 0);
                break;
            case 13: break;
        }
    }

    private int Emerald(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Emerald) + 28;
        switch (msg)
        {
            case Msg.Update: if (++p.Counter >= 6) p.Counter = 0; return (p.Counter & 1) == 0 ? 1 : 0;
            case Msg.Draw: _w.DrawSprite(sh + p.Counter / 2, p.X, p.Y); break;
            case Msg.Touch:
                if (z != 0) return 0;
                if (_w.Pl.Emeralds < 99) _w.Pl.Emeralds++;
                int er = Collect(n, 1, -2, 1);
                if (_w.Hints.Emerald) { _w.Hints.Emerald = false; _w.TextMsg(39); } // first emerald: dotextmsg(39)
                return er;
        }
        return 0;
    }

    private int Heart(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Heart);
        int[] beat = { 33, 34, 35, 34 };
        switch (msg)
        {
            case Msg.Update: p.Counter = (p.Counter + 1) & 7; return (p.Counter & 1) == 0 ? 1 : 0;
            case Msg.Draw: _w.DrawSprite(sh + beat[p.Counter / 2], p.X, p.Y); break;
            case Msg.Touch:
                if (z != 0) return 0;
                if (_w.Pl.Health < 5) _w.Pl.Health++;
                int r = Collect(n, -1, -3, 0);
                if (_w.Hints.Heart) { _w.Hints.Heart = false; _w.Txt("HEARTS GIVE YOU HEALTH", 7, 0); }
                return r;
        }
        return 0;
    }

    private int Gems(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Gems) + 29;
        switch (msg)
        {
            case Msg.Draw: _w.DrawSprite(sh + p.State, p.X, p.Y); break;
            case Msg.Touch:
                if (z != 0) return 0;
                return Collect(n, -1, -1, 1);
        }
        return 0;
    }

    private int Key(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Key);
        switch (msg)
        {
            case Msg.Update: if (++p.Counter >= 16) p.Counter = 0; return (p.Counter & 1) == 0 ? 1 : 0;
            case Msg.Draw: _w.DrawSprite(sh + p.State * 8 + p.Counter / 2, p.X, p.Y); break;
            case Msg.Touch:
                if (z != 0) return 0;
                int invKey = Inv.Key(p.State);
                if (_w.InvCount(invKey) == 0) { _w.AddInv(invKey); p.ZapHold = 1; }
                if (p.ZapHold == 1)
                {
                    _w.AddObj(ObjectKind.Flash, p.X - 3, p.Y - 2, 0, 0); _w.KillObj(n);
                    _w.Txt(GameStrings.InvKeyMsg[p.State], 3, 0);
                    return 1;
                }
                _w.Txt("You already have a key of that COLOR!", 5, 0);
                break;
        }
        return 0;
    }

    private int Epic(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Epic);
        switch (msg)
        {
            case Msg.Draw: _w.DrawSprite(sh + (_w.Gamecount & 7), p.X, p.Y); break;
            case Msg.Touch:
                if (z != 0) return 0;
                _w.AddScore(25, p.X, p.Y);
                if (++p.State > 12) { _w.Explode1(p.X, p.Y, 5, 0); _w.KillObj(n); }
                return 1;
        }
        return 0;
    }

    private static readonly int[] TokenYd = { 1, 1, 0, 0, -1, -1, 0, 0 };

    /// <summary>inv_xfm: token states that transform the hero — inv_hero(0, revert), inv_bird(5),
    /// inv_fish(6).</summary>
    private static bool IsXfmToken(int state) => state is 0 or 5 or 6;

    private int Token(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Token) + 15;
        switch (msg)
        {
            case Msg.Update:
                // Only the fish token (state 6) lives: it latches a larger hitbox and bobs on an 8-step
                // cycle. All other tokens are static.
                if (p.State != 6) return 0;
                if (p.Info1 == 0) { p.Info1 = 1; p.Xl = 30; p.Yl = 38; }
                if (++p.StateCount >= 8) p.StateCount = 0;
                _w.JustMove(n, p.X, p.Y + TokenYd[p.StateCount]);
                return 1;
            case Msg.Draw:
                if (p.State == 6) { if (p.SubState == 0) _w.DrawSprite(Table(ObjectKind.HeroSwim) + 21, p.X, p.Y); }
                else _w.DrawSprite(sh + InvShape[p.State], p.X, p.Y);
                break;
            case Msg.Touch:
                if (z != 0 || p.SubState != 0) return 0;
                if (IsXfmToken(p.State))
                {
                    // Transform tokens change the hero's form and stay inert (substate 1), not killed.
                    // (snd_splash/snd_bee/snd_stalagcrash dropped.)
                    p.SubState = 1;
                    _w.PlayerXfm(p.State);
                }
                else
                {
                    _w.Txt(GameStrings.InvGetMsg[p.State], 3, 0);
                    _w.AddObj(ObjectKind.Flash, p.X - 1, p.Y - 3, 0, 0);
                    if (p.State == 4)
                    {
                        if (_w.InvCount(Inv.Laser) < 5)
                        {
                            _w.AddInv(Inv.Laser);
                            _w.Screen.SetColor6(162, 23, 13, 33); _w.Screen.SetColor6(163, 29, 19, 39);
                            _w.Screen.SetColor6(164, 35, 25, 45); _w.Screen.SetColor6(165, 42, 32, 51);
                            _w.Screen.SetColor6(166, 49, 40, 57); _w.Screen.SetColor6(167, 57, 50, 63);
                        }
                    }
                    else _w.AddInv(p.State);
                    _w.KillObj(n);
                }
                return 1;
            case Msg.Trigger: p.SubState = 0; break;
        }
        return 0;
    }

    /// <summary>msg_icons (obj_icons, kind 84): the three collectible power icons. Each shows its
    /// "POWER OBJECT #n" popup (dotextmsg 8/9/10) before being added to inventory, then flashes and vanishes.</summary>
    private int Icons(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        switch (msg)
        {
            case Msg.Draw: _w.DrawSprite(Table(ObjectKind.Icons) + 31 + p.State, p.X, p.Y); break;
            case Msg.Touch:
                if (z == 0)
                {
                    _w.TextMsg(p.State switch { 0 => 8, 1 => 9, _ => 10 }); // dotextmsg(8/9/10) before addinv
                    _w.AddInv(p.State switch { 0 => Inv.Icon1, 1 => Inv.Icon2, _ => Inv.Icon3 });
                    return Collect(n, -1, -1, 1);
                }
                break;
        }
        return 0;
    }
}
