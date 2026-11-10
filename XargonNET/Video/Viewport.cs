namespace XargonNET.Video;

/// <summary>
/// Port of <c>vptype</c> from the original GR.H — a clipped drawing region on the
/// 320x200 VGA screen, with an origin offset used for world-to-screen scrolling.
/// Fields mirror the C names: vpx/vpy (absolute position), vpxl/vpyl (size),
/// vpox/vpoy (origin offset).
/// </summary>
public sealed class Viewport
{
    /// <summary>Absolute top-left of the viewport on the screen (vpx, vpy).</summary>
    public int X;
    public int Y;

    /// <summary>Size of the viewport in pixels (vpxl, vpyl).</summary>
    public int W;
    public int H;

    /// <summary>World-space origin offset subtracted when drawing (vpox, vpoy).</summary>
    public int OriginX;
    public int OriginY;

    public Viewport(int x, int y, int w, int h)
    {
        X = x;
        Y = y;
        W = w;
        H = h;
        OriginX = 0;
        OriginY = 0;
    }
}
