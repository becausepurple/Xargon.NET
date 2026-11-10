using XargonNET.Assets;

namespace XargonNET.Video;

/// <summary>
/// Resolves and draws shapes onto a <see cref="VgaScreen"/>, porting drawshape()/ldrawsh_vga
/// from GRAPHICS.C. Shape tables are loaded lazily and cached (the shm_want/shm_do lifecycle;
/// we simply cache since a modern machine has ample memory). Colour index 0 is transparent.
/// </summary>
public sealed class ShapeManager
{
    private readonly ShapeFile _file;
    private readonly ShapeTable?[] _tables = new ShapeTable?[ShapeFile.MaxTables];

    public ShapeManager(ShapeFile file) => _file = file;

    public ShapeTable? Table(int index)
    {
        if ((uint)index >= ShapeFile.MaxTables) return null;
        if (_tables[index] is null && _file.HasTable(index))
            _tables[index] = _file.LoadTable(index);
        return _tables[index];
    }

    public Shape? GetShape(int table, int index) => Table(table)?[index];

    /// <summary>
    /// Draw shape <paramref name="shapeId"/> (table = (id&gt;&gt;8)&amp;0x3f, shape = id&amp;0xff)
    /// at world position (x,y) within a viewport, honouring its origin offset and clipping.
    /// </summary>
    public void DrawShape(VgaScreen screen, Viewport vp, int shapeId, int x, int y)
    {
        int table = (shapeId >> 8) & 0x3f;
        int index = shapeId & 0xff;
        var sh = GetShape(table, index);
        if (sh is not null)
            Blit(screen, vp, sh, x, y);
    }

    /// <summary>Blit a shape at world (x,y): screen pos = vp origin + (x,y) - vp scroll offset.</summary>
    public static void Blit(VgaScreen screen, Viewport vp, Shape sh, int x, int y)
    {
        int dx = vp.X + (x - vp.OriginX);
        int dy = vp.Y + (y - vp.OriginY);

        int clipL = Math.Max(vp.X, 0);
        int clipT = Math.Max(vp.Y, 0);
        int clipR = Math.Min(vp.X + vp.W, VgaScreen.Width);
        int clipB = Math.Min(vp.Y + vp.H, VgaScreen.Height);

        byte[] src = sh.Pixels;
        byte[] buf = screen.Pixels;

        for (int sy = 0; sy < sh.Height; sy++)
        {
            int py = dy + sy;
            if (py < clipT || py >= clipB) continue;
            int rowSrc = sy * sh.Width;
            int rowDst = py * VgaScreen.Width;
            for (int sx = 0; sx < sh.Width; sx++)
            {
                int px = dx + sx;
                if (px < clipL || px >= clipR) continue;
                byte c = src[rowSrc + sx];
                if (c != 0) buf[rowDst + px] = c; // colour 0 = transparent
            }
        }
    }
}
