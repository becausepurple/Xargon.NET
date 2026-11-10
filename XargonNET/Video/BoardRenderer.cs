using XargonNET.Assets;

namespace XargonNET.Video;

/// <summary>
/// Draws a board's tile grid (drawboard/drawcell from XARGON.C). Each cell value indexes the
/// info[] table; the cell's shape is info[cell].Sh. Cells flagged f_msgdraw are animated and
/// normally drawn by block-message handlers — for now they're drawn with their base shape.
/// </summary>
public static class BoardRenderer
{
    private const int F_MsgDraw = 16;

    public static void DrawBoard(VgaScreen screen, Viewport vp, Board board, InfoTable info, ShapeManager shapes)
    {
        // Visible tile window derived from the viewport's scroll origin (16px tiles).
        int firstX = Math.Max(0, vp.OriginX >> 4);
        int firstY = Math.Max(0, vp.OriginY >> 4);
        int lastX = Math.Min(Board.W, firstX + (vp.W >> 4) + 2);
        int lastY = Math.Min(Board.H, firstY + (vp.H >> 4) + 2);

        for (int ty = firstY; ty < lastY; ty++)
            for (int tx = firstX; tx < lastX; tx++)
            {
                int cell = board.CellIndex(tx, ty);
                var e = info.Entries[cell % InfoTable.Count];
                shapes.DrawShape(screen, vp, e.Sh, tx << 4, ty << 4);
            }
    }
}
