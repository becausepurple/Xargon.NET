using XargonNET.Assets;
using XargonNET.Video;

namespace XargonNET.Objects;

/// <summary>calc_scroll (X_PLAYER.C): scroll the gameplay viewport to keep the hero within a band,
/// shared by the platformer player and the top-down map hero.</summary>
public static class Camera
{
    private const int ScrnXs = 21, ScrnYs = 11;

    public static void Follow(Viewport vp, Obj hero, int peeky)
    {
        int vpox = vp.OriginX, vpoy = vp.OriginY;
        int xscroll = hero.Xl == 10 ? 4 : 8;
        int sx = 0, sy = 0;

        if (hero.X < vpox + 116 && vpox >= 24) sx = -xscroll;
        if (hero.X > vpox + 16 * ScrnXs - 140 && vpox < 16 * (Board.W - ScrnXs - 1)) sx = xscroll;

        int limit = 16 * (Board.H - ScrnYs + 1);
        int top = Math.Max(0, Math.Min(limit, hero.Y - 16 * ScrnYs + 96));
        int bot = Math.Max(0, Math.Min(limit, hero.Y - 32));
        int peek = vpoy + peeky;
        if (peek >= top && peek <= bot) sy = peeky;
        else if (vpoy > bot) sy = bot - vpoy;
        else if (vpoy < top) sy = top - vpoy;

        vp.OriginX = Math.Clamp(vpox + sx, 0, Math.Max(0, Board.W * 16 - vp.W));
        vp.OriginY = Math.Clamp(vpoy + sy, 0, Math.Max(0, Board.H * 16 - vp.H));
    }

    /// <summary>Snap the viewport to centre on the hero (setorigin), used when a board loads.</summary>
    public static void Center(Viewport vp, Obj hero)
    {
        vp.OriginX = Math.Clamp(hero.X - vp.W / 2, 0, Math.Max(0, Board.W * 16 - vp.W));
        vp.OriginY = Math.Clamp(hero.Y - vp.H / 2, 0, Math.Max(0, Board.H * 16 - vp.H));
    }

    /// <summary>
    /// setorigin() (X_OBJMAN.C): snap the gameplay viewport origin so the hero is framed, with the
    /// exact DOS clamps — X aligned to 8px and held in [16 .. (boardxs-scrnxs)*16], Y in
    /// [0 .. (boardys+1-scrnys)*16]. Used on every board load / reenter (p_reenter calls setorigin).
    /// </summary>
    public static void SetOrigin(Viewport vp, Obj hero)
    {
        int vpox = (hero.X - ScrnXs * 8) & ~7;
        if (vpox < 16) vpox = 16;
        else if (vpox > (Board.W - ScrnXs) * 16) vpox = (Board.W - ScrnXs) * 16;

        int vpoy = hero.Y + 16 - ScrnYs * 8;
        if (vpoy < 0) vpoy = 0;
        else if (vpoy > (Board.H + 1 - ScrnYs) * 16) vpoy = (Board.H + 1 - ScrnYs) * 16;

        vp.OriginX = vpox;
        vp.OriginY = vpoy;
    }
}
