using System.Globalization;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Ui;

/// <summary>The inventory numbers inv_win prints: invcount() of laser/fire/rock/mapkey and the three power icons.</summary>
public readonly record struct InventoryCounts(int Laser, int Fire, int Rock, int MapKey, int Icon1, int Icon2, int Icon3)
{
    /// <summary>Read the counts through an invcount() function (e.g. <c>ObjectManager.InvCount</c>).</summary>
    public static InventoryCounts From(Func<int, int> invCount) => new(
        invCount(Inv.Laser), invCount(Inv.Fire), invCount(Inv.Rock), invCount(Inv.MapKey),
        invCount(Inv.Icon1), invCount(Inv.Icon2), invCount(Inv.Icon3));
}

/// <summary>
/// The INVENTORY STATUS window (xargon.c inv_win, 1001-1060). <see cref="Render"/> is pure drawing (no SDL);
/// <see cref="Show"/> is the blocking modal: draw over the last frame, then rest().
/// </summary>
public sealed class InventoryWindow
{
    /// <summary>One text item: inside-relative position, font, colour digit and text.</summary>
    public const int TitleX = 20, TitleY = 4, SeparatorY = 12;
    public const int SlotX = 68, SlotY = 72, SlotPitch = 35;

    /// <summary>defwin(&amp;invwin,5,16,14,7,0,0,textbox): border 40,16 240x128; inside 48,24 224x112.</summary>
    public static Window Frame() => Window.DefTextbox(5, 16, 14, 7);

    /// <summary>drawwin + every item of inv_win (without the sound or the rest()). Returns the window.</summary>
    public static Window Render(VgaScreen screen, ShapeManager shapes, TextRenderer text, InventoryCounts n)
    {
        var win = Frame();
        win.Draw(screen, shapes);
        var vp = win.Inside;
        int sh = KindDefs.Get(ObjectKind.Icons).ShapeTable * 256;
        const int f1 = TextRenderer.Font8, f2 = TextRenderer.Font6;

        text.Draw(screen, vp, TitleX, TitleY, f1, 7, -1, "INVENTORY STATUS WINDOW");
        text.Draw(screen, vp, TitleX, SeparatorY, f1, 7, -1, "-----------------------");

        Item(0x1e26, 5, 26, 24, 25, 34, n.Laser, "LASER BULLETS");
        Item(0x251c, 132, 20, 152, 25, 162, n.Fire, "FIREBALLS");
        Item(0x1e14, 4, 40, 24, 45, 34, n.Rock, "ROCKS to throw");
        Item(0x1e10, 132, 40, 152, 45, 162, n.MapKey, "GATE KEYS");

        text.Draw(screen, vp, 60, 60, f1, 7, -1, "POWER OBJECTS");
        for (int c = 0; c < 3; c++) text.Draw(screen, vp, SlotX + c * SlotPitch, 72, f2, 7, 1, "   ");
        for (int c = 0; c < 3; c++) text.Draw(screen, vp, SlotX + c * SlotPitch, 78, f2, 7, 1, "   ");
        for (int c = 0; c < 3; c++) text.Draw(screen, vp, SlotX + c * SlotPitch, 84, f2, 7, 1, "   ");
        for (int c = 0; c < 3; c++) text.Draw(screen, vp, 86 + c * SlotPitch, 78, f2, 7, 0, " ");   // shadow
        for (int c = 0; c < 3; c++) text.Draw(screen, vp, 86 + c * SlotPitch, 84, f2, 7, 0, " ");
        for (int c = 0; c < 3; c++) text.Draw(screen, vp, 74 + c * SlotPitch, 90, f2, 7, 0, "   ");
        if (n.Icon1 != 0) shapes.DrawShape(screen, vp, sh + 31, 68, 72);
        if (n.Icon2 != 0) shapes.DrawShape(screen, vp, sh + 32, 103, 72);
        if (n.Icon3 != 0) shapes.DrawShape(screen, vp, sh + 33, 138, 72);

        shapes.DrawShape(screen, vp, 0x1e40, 26, 61);
        shapes.DrawShape(screen, vp, 0x1e40, 174, 61);

        text.Draw(screen, vp, 69, 100, f1, 7, -1, "#1");
        text.Draw(screen, vp, 104, 100, f1, 7, -1, "#2");
        text.Draw(screen, vp, 139, 100, f1, 7, -1, "#3");
        return win;

        // icon at (sx,sy); count (colour 6) at (cx,cy); label (colour 4) at (lx,cy); counts are itoa(): decimal text.
        void Item(int shape, int sx, int sy, int cx, int cy, int lx, int count, string label)
        {
            shapes.DrawShape(screen, vp, shape, sx, sy);
            text.Draw(screen, vp, cx, cy, f2, 6, -1, count.ToString(CultureInfo.InvariantCulture));
            text.Draw(screen, vp, lx, cy, f2, 4, -1, label);
        }
    }

    // ---- modal (SDL; not headlessly testable beyond the pure part above) ----

    private readonly VgaScreen _screen;
    private readonly Func<ShapeManager> _shapes;
    private readonly Func<TextRenderer> _text;
    private readonly SdlPresenter? _presenter;
    private readonly PopupWindows _popups;

    public InventoryWindow(VgaScreen screen, Func<ShapeManager> shapes, Func<TextRenderer> text,
        SdlPresenter? presenter, PopupWindows popups)
    {
        _screen = screen;
        _shapes = shapes;
        _text = text;
        _presenter = presenter;
        _popups = popups;
    }

    /// <summary>inv_win(): the window over the current screen, then rest(). Does nothing if the window was already
    /// closed (a modal has no auto-dismiss). The caller's next level frame repaints the area (moddrawboard).</summary>
    public void Show(InventoryCounts counts)
    {
        if (_popups.QuitLatched) return;
        Render(_screen, _shapes(), _text(), counts);
        _presenter?.Present(_screen);
        _popups.Rest();
    }
}
