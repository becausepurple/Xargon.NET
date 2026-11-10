using XargonNET.Video;

namespace XargonNET.Objects;

/// <summary>
/// The overworld map hero (obj_tiny / msg_tiny). Unlike the platformer player, the map hero moves
/// freely in four directions (no gravity) at 4px/step, blocked by solid tiles. Walking onto a
/// level-entrance checkpoint triggers a board transition (handled by the checkpoint behaviour).
/// Drives object 0 on the map board.
/// </summary>
public sealed class MapHero
{
    private const int Kind = ObjectKind.Tiny;

    private readonly ObjectManager _om;
    private readonly Viewport _vp;

    private Assets.Obj P => _om.Objects[0];

    public MapHero(ObjectManager om, Viewport gameVp)
    {
        _om = om;
        _vp = gameVp;
    }

    public void Update(Controls c)
    {
        var p = P;
        if (c.Dx1 != 0 || c.Dy1 != 0)
        {
            p.StateCount = (p.StateCount + 1) & 7;
            p.Xd = c.Dx1;
            p.Yd = c.Dy1;
            if (_om.Cando(0, p.X + c.Dx1 * 4, p.Y + c.Dy1 * 4, GameFlags.PlayerThru) != 0)
            {
                p.X += c.Dx1 * 4;
                p.Y += c.Dy1 * 4;
            }
        }
        Camera.Follow(_vp, p, 0);
        _om.TouchBkgnd(0);
    }

    public void Draw(VgaScreen screen, ShapeManager shapes)
    {
        var p = P;
        int sh = KindDefs.Get(Kind).ShapeTable * 256;
        if (p.Xd != 0) sh += p.StateCount / 2 + (p.Xd < 0 ? 0 : 12);
        else sh += p.StateCount / 2 + (p.Yd < 0 ? 4 : 8);
        shapes.DrawShape(screen, _vp, sh, p.X, p.Y);
    }
}
