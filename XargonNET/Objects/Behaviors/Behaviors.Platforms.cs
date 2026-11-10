using XargonNET.Assets;

namespace XargonNET.Objects;

public sealed partial class Behaviors
{
    private int Spring(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Spring);
        switch (msg)
        {
            case Msg.Update: if (p.StateCount > 0) p.StateCount--; return 1;
            case Msg.Draw:
                _w.DrawSprite(sh + (p.StateCount > 6 ? 1 : p.StateCount > 3 ? 3 : 2), p.X, p.Y);
                break;
            case Msg.Touch:
                if (z != 0 || p.ZapHold != 0) return 0;
                var pl = _w.Obj(0);
                if (pl.Yd > 0 && pl.Y + pl.Yl >= p.Y + 6)
                {
                    pl.State = 2; // st_jumping
                    pl.Y -= 6;
                    pl.Yd = -(16 + 4 * p.Counter);
                    if (p.Xd == 1 && p.Counter++ >= 4) p.Counter = 0;
                    pl.Xd = _w.InputDx;
                    pl.SubState = 0;
                    p.ZapHold = 5;
                    p.StateCount = 10;
                    return 1;
                }
                break;
        }
        return 0;
    }

    /// <summary>msg_platform (kind 16): a rider platform. It patrols along (xd,yd), reversing at
    /// walls; while the player stands on it (substate == st_platform) it carries them and reads their
    /// input — jump launches off, down+jump drops through, left/right turns them. state 1 is a
    /// trigger-halted platform. Mounting one the first time shows the "ride" popup (first_platform / dotextmsg 2).</summary>
    private int Platform(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        var p0 = _w.Obj(0);
        int playerXl = KindDefs.Get(ObjectKind.Player).Xl;
        const int ptns = GameFlags.PlayerThru | GameFlags.NotStair;
        const int stStand = 0, stJumping = 2, stPlatform = 7;
        switch (msg)
        {
            case Msg.Update:
                p.StateCount = (p.StateCount + 1) & 7;
                if (p.State != 1)
                {
                    if (p.Yd < 0 && p.SubState == stPlatform) // rising while ridden: needs headroom
                    {
                        if (_w.Cando(0, p0.X, p.Y - 42, ptns) == ptns) _w.MoveObj(n, p.X + p.Xd, p.Y + p.Yd);
                        else
                        {
                            p.Xd = -p.Xd; p.Yd = -p.Yd;
                            if (_w.Cando(0, p0.X, p.Y - 42, ptns) == ptns) _w.MoveObj(n, p.X + p.Xd, p.Y + p.Yd);
                        }
                    }
                    else if (_w.Cando(n, p.X + p.Xd, p.Y + p.Yd, ptns) == ptns) _w.MoveObj(n, p.X + p.Xd, p.Y + p.Yd);
                    else
                    {
                        p.Xd = -p.Xd; p.Yd = -p.Yd;
                        if (_w.Cando(n, p.X + p.Xd, p.Y + p.Yd, ptns) == ptns) _w.MoveObj(n, p.X + p.Xd, p.Y + p.Yd);
                    }
                }
                if (p.SubState == stPlatform)
                {
                    var c = _w.CurControls;
                    int dx1 = _w.InputDx, dy1 = _w.InputDy, fire2 = c?.Fire2 ?? 0;
                    if (dy1 > 0 && fire2 != 0) // drop through
                    {
                        if (c is not null) c.Fire2Off = 1;
                        p0.State = stJumping; p0.Yd = 0; p0.SubState = 2; p0.Xl = playerXl;
                        p.SubState = stStand; p.ZapHold = 10; return 1;
                    }
                    if (fire2 != 0) // jump off
                    {
                        if (c is not null) c.Fire2Off = 1;
                        p0.State = stJumping;
                        p0.Yd = -(16 + 4 * _w.InvCount(Inv.Jump));
                        p0.SubState = 0; p0.Xd = dx1; p0.Xl = playerXl;
                        p.SubState = stStand; p.ZapHold = 5; return 1;
                    }
                    if (dx1 != 0) { p0.Xd = dx1; p0.StateCount = 0; }
                    else if (p0.StateCount > 20) { p0.Xd = 0; p0.StateCount = 0; }
                    p0.X = p.X; p0.Y = p.Y - p0.Yl;
                    p0.Yd = p.Yd != 0 ? 0 : dy1;
                    if (p.Yd < 0) p0.SubState = 0;
                    else if (p.Yd > 0) p0.SubState = 1;
                    _w.MoveObj(0, p0.X, p0.Y);
                }
                if (_w.OnScreen(n)) return 1;
                break;
            case Msg.Draw: _w.DrawSprite(Table(ObjectKind.Platform) + 13 + p.StateCount / 2, p.X, p.Y); break;
            case Msg.Touch:
                if (_w.Obj(z).Kind != ObjectKind.Player || p.ZapHold != 0 || p.SubState == stPlatform || p0.State == stPlatform) return 0;
                if (p0.Y + p0.Yl > p.Y + p.Yl + p0.Yd) return 0; // mount only when landing from above
                if (_w.Hints.Platform) { _w.Hints.Platform = false; _w.TextMsg(2); } // first ride: dotextmsg(2)
                p.SubState = stPlatform;
                p0.State = stPlatform;
                p0.Counter = 6;
                p0.Xl = playerXl + 3;
                return 1;
            case Msg.Trigger:
                if (p.State == -1) return 0;
                p.State = 1 - p.State;
                return 1;
        }
        return 0;
    }

    /// <summary>msg_elev (kind 18): the TurboLift. A player standing on it rides up or down one cell
    /// per activation, the lift stamping/clearing the elevl/elevr shaft cells as it travels. Up moves
    /// at once; down is deferred a frame (info1) so the player stays glued to it.</summary>
    private int Elev(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.Elev);
        int xc = p.X / 16, yc = p.Y / 16;
        switch (msg)
        {
            case Msg.Update:
                if (p.State > 0) p.State--;
                if (p.State == 0 && _w.BoardCell(xc, yc + 1) == Tiles.Elevl && p.Counter != -1)
                {
                    _w.SetBoard(xc, yc + 1, _w.BoardCell(xc, yc));
                    _w.SetBoard(xc + 1, yc + 1, _w.BoardCell(xc + 1, yc));
                    _w.MoveObj(n, p.X, p.Y + 16);
                    return 1;
                }
                if (p.Info1 != 0 && _w.BoardCell(xc, yc + 1) == Tiles.Elevl)
                {
                    _w.SetBoard(xc, yc + 1, _w.BoardCell(xc, yc));
                    _w.SetBoard(xc + 1, yc + 1, _w.BoardCell(xc + 1, yc));
                    _w.MoveObj(n, p.X, p.Y + 16);
                    _w.JustMove(0, _w.Obj(0).X, yc * 16 - 8);
                    p.Info1 = 0;
                    return 1;
                }
                break;
            case Msg.Draw: _w.DrawSprite(sh, p.X, p.Y); break;
            case Msg.Touch:
                if (_w.Obj(z).Kind != ObjectKind.Player) break;
                p.State = 6;
                if (_w.Hints.Elev) { _w.Hints.Elev = false; _w.Txt("Press UP/DOWN to activate TurboLift", 7, 0); }
                if (_w.InputDy < 0) // ride up
                {
                    if (_w.JustMove(0, _w.Obj(0).X, (yc - 2) * 16 - 8) != 0)
                    {
                        _w.MoveObj(n, p.X, p.Y - 16);
                        _w.SetBoard(xc, yc, Tiles.Elevl);
                        _w.SetBoard(xc + 1, yc, Tiles.Elevr);
                    }
                }
                else if (_w.InputDy > 0) // ride down (next frame)
                {
                    if (_w.BoardCell(xc, yc + 1) == Tiles.Elevl) p.Info1 = 1;
                }
                p.SubState = _w.InputDy;
                return 1;
        }
        return 0;
    }

    /// <summary>msg_bridger (kind 8): an invisible controller that extrudes or retracts a run of beam
    /// (stlbeam, when xd==0) or bridge cells from (x,y) along (xd,yd) in response to triggers. On
    /// retract it backfills with a passable neighbour cell so the gap blends in.</summary>
    private int Bridger(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        const int ptns = GameFlags.PlayerThru | GameFlags.NotStair;
        int xc = p.X / 16, yc = p.Y / 16;
        int dircell = p.Xd == 0 ? Tiles.Stlbeam : Tiles.Bridge;
        int oldcell = 0, newcell = 0;
        bool dochange = false;
        switch (msg)
        {
            case Msg.TrigOn:
                dochange = true; oldcell = _w.BoardCell(xc, yc); newcell = dircell; break;
            case Msg.TrigOff:
                dochange = true; oldcell = dircell; newcell = 0; break;
            case Msg.Trigger:
                if (_w.BoardCell(xc, yc) == dircell) { newcell = 0; oldcell = dircell; }
                else { newcell = dircell; oldcell = _w.BoardCell(xc, yc); }
                dochange = true;
                ConsumePad(z);
                break;
            default: return 0;
        }
        if (!dochange) return 0;
        if (newcell == 0) // find a passable neighbour cell to leave behind
            for (int c = -1; c <= 1; c += 2)
            {
                int d = _w.BoardCell(xc + c * p.Yd, yc + c * p.Xd);
                if ((_w.InfoFlags(d) & ptns) == ptns) newcell = d;
            }
        while (_w.BoardCell(xc, yc) == oldcell)
        {
            _w.SetBoard(xc, yc, newcell); xc += p.Xd; yc += p.Yd;
        }
        return 1;
    }

    /// <summary>msg_addstep (kind 11): a one-shot step-builder. When triggered it stamps a small group
    /// of solid cells (a stair, platform, or block, keyed by xd) into the board, then removes
    /// itself.</summary>
    private int Addstep(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int xc = p.X / 16, yc = p.Y / 16;
        switch (msg)
        {
            case Msg.Update:
                if (p.State == 0) return 0;
                switch (p.Xd)
                {
                    case 1: _w.SetBoard(xc, yc, Tiles.S1l); _w.SetBoard(xc + 1, yc, Tiles.S1r); break;
                    case 2: _w.SetBoard(xc, yc, Tiles.S2); _w.SetBoard(xc, yc + 1, Tiles.S2u); break;
                    case 3: _w.SetBoard(xc, yc, Tiles.S3); break;
                    case 4:
                        _w.SetBoard(xc, yc, Tiles.S3); _w.SetBoard(xc + 1, yc, Tiles.S3);
                        _w.SetBoard(xc, yc + 1, Tiles.C2); _w.SetBoard(xc + 1, yc + 1, Tiles.C2);
                        _w.SetBoard(xc + 2, yc, Tiles.C8); _w.SetBoard(xc + 2, yc + 1, Tiles.C8); break;
                    case 6: _w.SetBoard(xc, yc, Tiles.S6l); _w.SetBoard(xc + 1, yc, Tiles.S6r); break;
                    case 7: _w.SetBoard(xc, yc, Tiles.Platl); _w.SetBoard(xc + 1, yc, Tiles.Platr); break;
                }
                _w.KillObj(n);
                break;
            case Msg.Trigger:
                p.State = 1;
                if (_w.Obj(z).Kind == ObjectKind.Pad)
                {
                    _w.AddObj(ObjectKind.Flash, _w.Obj(z).X - 1, _w.Obj(z).Y - 3, 0, 0); // flash
                    _w.KillObj(z);
                    return 1;
                }
                break;
        }
        return 0;
    }
}
