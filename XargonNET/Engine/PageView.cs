using XargonNET.Assets;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Engine;

/// <summary>Logical key presses the page viewer reacts to (the key wait loop of pageview()).</summary>
public enum PageKey
{
    /// <summary>Up arrow: previous page.</summary>
    Back,
    /// <summary>Down arrow, Space, Enter, or a fire button: next page.</summary>
    Forward,
    /// <summary>Escape: leave the viewer.</summary>
    Escape,
}

/// <summary>Result of a navigation step: stay on the current page, show another page, or exit.</summary>
public readonly record struct PageNav(PageNavKind Kind, int Page)
{
    public static readonly PageNav Stay = new(PageNavKind.Stay, 0);
    public static readonly PageNav Exit = new(PageNavKind.Exit, 0);
    public static PageNav GoTo(int page) => new(PageNavKind.GoTo, page);
}

public enum PageNavKind { Stay, GoTo, Exit }

/// <summary>
/// pageview() (xargon.c): the story/credits viewer. Each page is a full-screen (320x200) window of a board
/// whose top-left is a checkpoint object (counter = page number). This class holds the pure parts (anchor
/// lookup, key navigation) and the page renderer shared by the game loop and the debug dump.
/// </summary>
public static class PageView
{
    /// <summary>Checkpoint <c>xd</c> flag: first page (cannot go back).</summary>
    public const int FlagFirst = 1;
    /// <summary>Checkpoint <c>xd</c> flag: last page (advancing exits).</summary>
    public const int FlagLast = 2;
    /// <summary>Checkpoint <c>xd</c> flag: overlay the 10x10 picture (table 0x39).</summary>
    public const int FlagPicture = 3;

    /// <summary>The LAST checkpoint object whose counter equals <paramref name="page"/>, or null.</summary>
    public static Obj? FindAnchor(Board b, int page)
    {
        Obj? found = null;
        foreach (var o in b.Objects)
            if (o.Kind == ObjectKind.Checkpt && o.Counter == page) found = o;
        return found;
    }

    /// <summary>Exactly the key logic of pageview(): Up goes back unless xd==1 or yd!=0; the advance keys go
    /// forward unless xd==2 or yd!=0, in which case xd==2 or yd!=0 exits.</summary>
    public static PageNav Navigate(Obj anchor, int page, PageKey key)
    {
        switch (key)
        {
            case PageKey.Back:
                return anchor.Xd != FlagFirst && anchor.Yd == 0 ? PageNav.GoTo(page - 1) : PageNav.Stay;
            case PageKey.Forward:
                if (anchor.Xd != FlagLast && anchor.Yd == 0) return PageNav.GoTo(page + 1);
                return anchor.Xd == FlagLast || anchor.Yd != 0 ? PageNav.Exit : PageNav.Stay;
            default:
                return PageNav.Exit;
        }
    }

    /// <summary>Draw one page: clear to 248, then the board (back objects, cells, normal+front objects, which
    /// includes the text objects) through a full-screen viewport whose origin is the anchor, plus the optional
    /// xd==3 picture overlay. No status bar and no hero.</summary>
    public static void Render(VgaScreen screen, ObjectManager objects, Board board, InfoTable info,
        ShapeManager shapes, Obj anchor)
    {
        var vp = new Viewport(0, 0, VgaScreen.Width, VgaScreen.Height)
        {
            OriginX = Math.Clamp(anchor.X, 0, Math.Max(0, Board.W * 16 - VgaScreen.Width)),
            OriginY = Math.Clamp(anchor.Y, 0, Math.Max(0, Board.H * 16 - VgaScreen.Height)),
        };
        screen.Clear(248);
        objects.DrawBack(screen, vp);
        BoardRenderer.DrawBoard(screen, vp, board, info, shapes);
        objects.DrawFore(screen, vp);
        if (anchor.Xd == FlagPicture)
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 10; y++)
                    shapes.DrawShape(screen, vp, 0x7900 + x + y * 10, x * 16 + 48, y * 16 + anchor.Y + 16);
    }
}
