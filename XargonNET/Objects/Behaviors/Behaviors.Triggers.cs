using XargonNET.Text;
using XargonNET.Assets;

namespace XargonNET.Objects;

public sealed partial class Behaviors
{
    private int Pad(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Pad);
        switch (msg)
        {
            case Msg.Update:
                if (p.Xd == 0) return 0;
                if (++p.StateCount > 15) p.StateCount = 0;
                return (p.StateCount & 1) == 0 ? 1 : 0;
            case Msg.Draw:
                if (p.Xd == 1) _w.DrawSprite(sh + p.StateCount / 4, p.X, p.Y);
                break;
            case Msg.Touch:
                if (z != 0) return 0;
                if (p.ZapHold == 0)
                {
                    int m = p.State == -1 ? Msg.TrigOff : (p.State == 1 ? Msg.TrigOn : Msg.Trigger);
                    _w.SendTrig(p.Counter, m, n);
                }
                p.ZapHold = 3;
                return 1;
        }
        return 0;
    }

    private int Button(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Button) + 9;
        switch (msg)
        {
            case Msg.Draw: _w.DrawSprite(sh - p.State, p.X + 8, p.Y); break;
            case Msg.Touch:
                if (p.ZapHold == 0)
                {
                    p.State = 1 - p.State;
                    _w.SendTrig(p.Counter, p.State == 1 ? Msg.TrigOff : Msg.TrigOn, n);
                }
                p.ZapHold = 10;
                return 1;
        }
        return 0;
    }

    private int Switch(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Switch) + 23;
        switch (msg)
        {
            case Msg.Draw: _w.DrawSprite(sh + p.State, p.X, p.Y); break;
            case Msg.Touch:
                if (z != 0) return 0;
                if (p.ZapHold == 0)
                {
                    p.ZapHold = 25;
                    _w.Txt("Press UP/DOWN to toggle switch", 7, 0);
                }
                if (_w.InputDy < 0 && p.State == 1)
                {
                    p.State = 0;
                    _w.SendTrig(p.Counter, p.Xd == 1 ? Msg.Trigger : Msg.TrigOn, n);
                    return 1;
                }
                if (_w.InputDy > 0 && p.State == 0)
                {
                    p.State = 1;
                    _w.SendTrig(p.Counter, p.Xd == 1 ? Msg.Trigger : Msg.TrigOff, n);
                    return 1;
                }
                break;
        }
        return 0;
    }

    private int Door(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Door) + 32;
        int xc = p.X / 16, yc = p.Y / 16;
        switch (msg)
        {
            case Msg.Update:
                if (p.StateCount == 0) return 0;
                // TODO(draw): drawcell(xc,yc-1); drawcell(xc,yc+1); moddrawboard() before killing (no cell-redraw facility yet)
                if (++p.StateCount > 16) { _w.KillObj(n); return 1; }
                break;
            case Msg.Draw:
                if (p.StateCount == 0 && p.State != -1) _w.DrawSprite(sh + p.State, p.X + 6, p.Y + 8);
                // TODO(draw): opening animation (drawcell above/below, stldr1/stldr3 sliding halves at y-16-statecount / y+16+statecount)
                break;
            case Msg.Trigger:
                if (p.StateCount != 0) return 0;
                if (_w.BoardCell(xc, yc) == Tiles.MpGate)
                {
                    if (_w.TakeInv(Inv.MapKey))
                    {
                        _w.Txt("THE GATE OPENS", 2, 0);
                        _w.SetBoard(xc, yc, _w.BoardCell(xc + p.Xd, yc + p.Yd));
                        _w.KillObj(n);
                        ConsumePad(z);
                        return 1;
                    }
                    _w.Txt("YOU NEED A GATE KEY TO PASS", 5, 0);
                    break;
                }
                // Standard keyed door, literal to the C: a successful takeinv of the matching key sets
                // substate=1, then substate==1 opens the door (so board data with substate already 1
                // opens it without a key; states outside 0..3 take nothing).
                if (p.State is >= 0 and <= 3 && _w.TakeInv(Inv.Key(p.State))) p.SubState = 1;
                if (p.SubState == 1)
                {
                    p.StateCount = 1;
                    _w.Txt(GameStrings.OpenMsg[_w.XrRandom(4)], 2, 0);
                    for (int c = 0; c <= 1; c++) _w.SetBoard(xc, yc + c, _w.BoardCell(xc - 1, yc + c));
                    _w.SetBoard(xc, yc - 1, _w.BoardCell(xc - 1, yc - 1));
                    ConsumePad(z); // consume the pad
                    return 1;
                }
                _w.Txt(GameStrings.DoorMsg[_w.XrRandom(4)], 5, 0);
                break;
        }
        return 0;
    }

    /// <summary>
    /// msg_txtmsg (obj_txtmsg, kind 71): a sign/auto-trigger. Drawn only in design mode (nothing in
    /// play). On the player touching it (z==0): a state-0 sign re-posts the "press UP" hint every tick
    /// and only shows its message (AUDIO <c>counter</c>, plain style) when the player presses Up; a
    /// state-1 object auto-triggers its message once and removes itself.
    /// </summary>
    private int Txtmsg(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        if (msg == Msg.Touch && z == 0)
        {
            if (p.State == 0) _w.Txt("Press UP arrow key to read the sign", 7, 0);
            if (_w.InputDy < 0 || p.State == 1)
            {
                _w.TextMsg(p.Counter);
                if (p.State == 1) _w.KillObj(n);
            }
            return 1;
        }
        return 0;
    }

    private int Poker(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Poker) + 13;
        switch (msg)
        {
            case Msg.Update: if (p.StateCount == 0) return 0; p.StateCount--; return 1;
            case Msg.Draw: _w.DrawSprite(sh + (p.StateCount == 0 ? 0 : 1), p.X, p.Y); break;
            case Msg.Touch:
                if (z != 0) return 0;
                if (p.State == 0) _w.Hitplayer(n, 0);
                else _w.POuch(5, 0);
                p.StateCount = 15;
                return 1;
            case Msg.Trigger: p.StateCount = 15; break;
        }
        return 0;
    }

    private int Checkpt(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        var p0 = _w.Obj(0);
        switch (msg)
        {
            case Msg.Update:
                if (p0.X < p.X + p.Xl && p.X < p0.X + p0.Xl && p0.Y < p.Y + p.Yl && p.Y < p0.Y + p0.Yl)
                {
                    p.Xd = p0.X; // save player's coordinates
                    p.Yd = p0.Y;
                }
                break;
            case Msg.Touch:
                if (z != 0) return 0;
                if (p.State == 2 && _w.CountObj(ObjectKind.Token, true) != 0)
                {
                    _w.Txt("FIND THE GATE KEY ON THIS LEVEL BEFORE YOU LEAVE", 5, 0);
                    return 1;
                }
                if (p.State == 3 && _w.CountObj(ObjectKind.Icons, false) != 0)
                {
                    _w.Txt("YOU CAN'T LEAVE WITHOUT THE POWER OBJECT", 5, 0);
                    return 1;
                }
                if (p.State == 4)
                {
                    _w.GameWon = true; // C: macrecend(); gameover=2
                    return 0;
                }
                if (_w.Pl.Level != p.Counter)
                {
                    if (p.Counter != 0) _w.Pl.Level = p.Counter;
                    if (p0.Kind == ObjectKind.Tiny) _w.MoveObj(0, p.Xd, p.Yd);
                    else
                    {
                        p0.X = p.X;
                        p0.Y = p.Y - 24;
                        p0.State = 4; // st_begin
                        p0.StateCount = 0;
                    }
                    // The checkpoint's "inside" text drives a transition: a board name enters that level,
                    // "!" returns to the map, and #/*/& prefixes are level songs.
                    if (!string.IsNullOrEmpty(p.Inside)) _w.NewLevel = p.Inside;
                    return 1;
                }
                break;
        }
        return 0;
    }

    /// <summary>
    /// msg_transpad (obj_transpad, kind 62): the "door" tile the player walks into. Invisible in
    /// play. When the player (z==0) overlaps it and presses Up (InputDy&lt;0) — or it is an
    /// auto-transport (xd==1) — the player enters st_transport anchored to the pad; the paired
    /// transport object (matching counter) then relocates them. xd encodes the pad type: 0 normal,
    /// 1 auto (consumes the pad), 2 two-way. counter==-1 is a one-way door that cannot be entered.
    /// </summary>
    private int Transpad(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        switch (msg)
        {
            case Msg.Update:
                if (p.State == 0) return 0;
                p.State--;
                return 1;
            case Msg.Touch:
                if (z != 0) return 0; // only the player activates a pad
                if (p.Counter == -1 && p.ZapHold == 0)
                {
                    _w.Txt("This is a ONE-WAY door and cannot be entered", 7, 0);
                    p.ZapHold = 25; return 0; // one-way: no entry
                }
                if (p.ZapHold == 0)
                {
                    var p0 = _w.Obj(0);
                    if (_w.InputDy != 0) p0.Yd = 0;
                    if (p.Xd == 0 && p.State == 0) _w.Txt("Press the UP arrow key to walk through doors", 7, 0);
                    else if (p.Xd == 2 && p.State == 0) _w.Txt("Press the UP arrow key to go to the other side", 3, 0);
                    if (_w.InputDy < 0 || p.Xd == 1)
                    {
                        if (p.Xd == 1) { _w.KillObj(n); p0.StateCount = 16; }
                        else { p0.StateCount = 0; p.ZapHold = 20; }
                        p0.State = 6; // st_transport
                        p0.X = p.X;
                        p0.Y = p.Y - 8;
                        p0.SubState = p.Counter;
                    }
                }
                p.State = 3;
                return 1;
        }
        return 0;
    }

    /// <summary>
    /// msg_transport (obj_transport, kind 61): the destination of a transpad, matched by counter.
    /// On trigger it drops the player at (x, y-24), recentres the camera (setorigin), and — for an
    /// auto-transport (xd==1) — removes itself so it fires only once.
    /// </summary>
    private int Transport(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        if (msg == Msg.Trigger)
        {
            _w.MoveObj(0, p.X, p.Y - 24);
            _w.CenterCamera();
            if (p.Xd == 1) _w.KillObj(n);
            return 1;
        }
        return 0;
    }

    /// <summary>msg_timerpad (obj_timerpad, kind 73): a multi-mode hidden pad. yd selects behaviour:
    /// 1=downward fire-breather, 2=upward fire burst, 3=spawns xargbots, 4=one-shot bonus spawn,
    /// 5=spawns creepers, else=periodic trigger of linked objects.</summary>
    private int Timerpad(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.TimerPad);
        switch (msg)
        {
            case Msg.Update:
                if (p.Yd == 1) // fire-breather, downward
                {
                    if (_w.XrRandom(70) == 0 && p.Info1 == 0) { p.StateCount = 15; p.Info1 = 1; }
                    if (p.Info1 == 0) return 0;
                    if (p.StateCount == 5) _w.AddObj(ObjectKind.Fire, p.X + 7, p.Y + 27, 0, 1);
                    if (p.StateCount == 0) p.Info1 = 0;
                    p.StateCount--;
                    return 0;
                }
                if (p.Yd == 2) // fire burst, upward
                {
                    if (_w.XrRandom(50) == 0) p.Info1 = 2;
                    if (p.Info1 == 0) return 0;
                    _w.AddObj(ObjectKind.Fire, p.X, p.Y - 26, 0, 0);
                    p.Info1 = 0;
                    return 1;
                }
                if (p.Yd == 3 && _w.Obj(0).State != 1)
                {
                    if (p.Counter > 0 && _w.XrRandom(p.Counter) == 0)
                    {
                        _w.AddObj(ObjectKind.Flash, p.X - 1, p.Y - 1, 0, 0);
                        _w.Obj(_w.AddObj(ObjectKind.Xargbot, p.X, p.Y, 0, 0)).State = _w.XrRandom(2);
                        return 1;
                    }
                    return 0;
                }
                if (p.Yd == 4)
                {
                    if (_w.XrRandom(200) == 0)
                    {
                        _w.AddObj(ObjectKind.Flash, p.X - 1, p.Y - 3, 0, 0);
                        _w.Obj(_w.AddObj(ObjectKind.Bonus, p.X, p.Y, 0, 0)).State = p.State;
                        _w.KillObj(n);
                        return 1;
                    }
                    return 0;
                }
                if (p.Yd == 5 && _w.Obj(0).State != 1)
                {
                    if (p.Counter > 0 && _w.XrRandom(p.Counter) == 0)
                    {
                        _w.AddObj(ObjectKind.Flash, p.X + 6, p.Y - 6, 0, 0);
                        _w.AddObj(ObjectKind.Creeper, p.X, p.Y + 2, -1, 0);
                        return 1;
                    }
                    return 0;
                }
                if (p.SubState++ == p.State)
                {
                    _w.SendTrig(p.Counter, Msg.Trigger, n);
                    p.SubState = 0;
                    return 1;
                }
                break;
            case Msg.Draw:
                if (p.Info1 == 1) _w.DrawSprite(sh + 12, p.X + 2, p.Y + 7);
                break;
        }
        return 0;
    }
}
