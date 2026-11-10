using XargonNET.Assets;

namespace XargonNET.Objects;

public sealed partial class Behaviors
{
    /// <summary>
    /// msg_heroswim (obj_heroswim, kind 2): the underwater form. Horizontal motion is water-gated
    /// (fishdo) so the hero can only swim through water, while gravity (yd += 4 out of water) drags it
    /// down when it leaves; in water it drifts with Up/Down, rises with jump (Fire2), emits bubbles,
    /// and fires paired torpedoes (Fire1). Dispatched for object 0 by Player.Update/Draw.
    /// </summary>
    private int HeroSwim(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.HeroSwim);
        var c = _w.CurControls;
        int dx1 = _w.InputDx, dy1 = _w.InputDy;
        switch (msg)
        {
            case Msg.Update:
                int onwater = _w.Fishdo(n, p.X, p.Y);
                p.Xd = dx1 * 8;
                if (onwater != 0)
                {
                    p.Yd += dy1 * 3 + (p.Yd < 2 ? 1 : 0);
                    if (_w.XrRandom(15) == 0) _w.AddObj(ObjectKind.Bubble, p.X + 8, p.Y - 2, 0, 0);
                    p.Yd = Math.Min(8, Math.Max(p.Yd, -8));
                }
                else
                {
                    p.Yd += 4;
                    p.Yd = Math.Min(16, Math.Max(p.Yd, -16));
                }
                if (c is not null && c.Fire2 != 0 && onwater != 0) { c.Fire2Off = 1; p.Yd = -3; }
                if (c is not null && c.Fire1 != 0)
                {
                    c.Fire1Off = 1;
                    if (_w.CountKind(ObjectKind.Torpedo) < 1)
                    {
                        _w.AddObj(ObjectKind.Torpedo, p.X + 22, p.Y + 25, 6, 0);
                        _w.AddObj(ObjectKind.Torpedo, p.X - 3, p.Y + 25, -6, 0);
                    }
                }
                if (p.Yd > 8) p.Yd = 8; else if (p.Yd < -8) p.Yd = -8;

                int destx = p.X + p.Xd;
                int desty = p.Y + p.Yd;
                if (_w.JustMove(n, p.X, desty) == 0)
                {
                    int tempy = p.Yd < 0 ? Floor16(desty + 16) : (p.Yd > 0 ? Floor16(desty) + 32 - p.Yl : p.Y);
                    if (p.Yd != 0 && _w.JustMove(n, p.X, tempy) == 0) p.Yd = 0;
                    p.Yd = 0;
                }
                _w.Fishdo(n, destx, p.Y); // horizontal move, gated to water
                _w.CalcScroll(0);
                return 1;
            case Msg.Draw:
                if (p.Yd > 0 && p.Xd == 0) p.State = 0;
                else if (p.Yd < 0 && p.Xd == 0) p.State = 1;
                else if (p.Xd < 0) p.State = 2;
                else if (p.Xd > 0) p.State = 3;
                else if (p.Yd == 0) p.State = 4;
                _w.DrawSprite(sh + p.State, p.X, p.Y);
                break;
        }
        return 0;
    }

    /// <summary>
    /// msg_herobee (obj_herobee, kind 3): the bumblebee form. Flies freely — horizontal speed from the
    /// arrow keys (fine 4px near a tile edge, 8px otherwise), a constant downward sag (yd++ every other
    /// frame) offset by the jump-flap (Fire2 → yd=-6). Fire1 shoots a single laser in the facing
    /// direction (substate), aimed by Up/Down. Dispatched for object 0 by Player.Update/Draw.
    /// </summary>
    private int HeroBee(int n, int msg, int z)
    {
        var p = _w.Obj(n);
        int sh = Table(ObjectKind.HeroBee);
        var c = _w.CurControls;
        int dx1 = _w.InputDx, dy1 = _w.InputDy;
        switch (msg)
        {
            case Msg.Update:
                p.Counter = (p.Counter + 1) & 3;
                if (p.Xd != 0) p.SubState = p.Xd;
                p.Xd = ((p.X & 7) != 0 || p.Xd == 0) ? dx1 * 4 : dx1 * 8;
                if ((p.Counter & 1) != 0) p.Yd++;
                if (c is not null && c.Fire2 != 0) { c.Fire2Off = 1; p.Yd = -6; }
                if (c is not null && c.Fire1 != 0)
                {
                    c.Fire1Off = 1;
                    if (_w.CountKind(ObjectKind.Laser) < 1)
                    {
                        p.Xd = p.SubState;
                        _w.AddObj(ObjectKind.Laser, p.X + Math.Sign(p.SubState) * 8, p.Y + 14, Math.Sign(p.SubState) * 8, dy1 * 2);
                    }
                }
                if (p.Yd > 8) p.Yd = 8; else if (p.Yd < -8) p.Yd = -8;

                int destx = p.X + p.Xd;
                if (_w.JustMove(n, destx, p.Y) == 0) destx = p.X;
                if (p.Yd != 0 && _w.JustMove(n, destx, p.Y + p.Yd) == 0) p.Yd = 0;
                _w.CalcScroll(0);
                _w.TouchBkgnd(0);
                return 1;
            case Msg.Draw:
                sh += p.Xd == 0 ? p.Counter / 2 + 4 : p.Counter / 2 + (p.Xd > 0 ? 2 : 0);
                _w.DrawSprite(sh, p.X, p.Y);
                break;
        }
        return 0;
    }
}
