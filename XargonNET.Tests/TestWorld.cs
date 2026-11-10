using XargonNET.Assets;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>Headless world builder mirroring GameApp.LoadAdvBoard (no SDL, no player controller).</summary>
public static class TestWorld
{
    /// <summary>Fresh viewport per access so tests that scroll it cannot affect each other.</summary>
    public static Viewport Vp => new Viewport(0, 2, VgaScreen.Width, 158);

    public static ObjectManager Load(int episode, string board)
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), episode);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        var b = set.LoadBoard(board.ToUpperInvariant());
        Effects.ApplyForBoard(screen, b);
        var pl = new PlayerState { Health = 5, Level = 1 };
        var w = new ObjectManager(b, shapes, set.Info, screen, pl, episode) { Text = new Ui.TextRenderer(shapes) };
        _ = new Behaviors(w);
        w.ReenterPlayer(pl.Level);
        return w;
    }

    public static void Step(ObjectManager w, int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            w.Gamecount++;
            w.UpdateObjects(Vp);
        }
    }
}
