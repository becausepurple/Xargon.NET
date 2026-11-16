using SDL3;
using XargonNET.Ui;
using XargonNET.Video;

namespace XargonNET.Engine;

public enum AskQuitAnswer { Pending, Yes, No }

/// <summary>
/// askquit() (xargon.c:698-707): a 160x32 box with a pre-rendered "quit?" picture; ANY key, fire button or
/// direction dismisses it and only 'Y' (after toupper) means yes. Decisions and drawing are pure/SDL-free; the
/// modal loop lives in <see cref="PopupWindows.AskQuit"/>.
/// </summary>
public static class AskQuit
{
    /// <summary>defwin(&amp;quitwin,10,64,9,1,0,0,textbox): border 80,64 160x32, inside 88,72 144x16.</summary>
    public static Window Frame() => Window.DefTextbox(10, 64, 9, 1);

    /// <summary>The key's verdict: Y = Yes; any other keystroke, arrow or Shift/Ctrl/Alt = No; keys that
    /// produce no keystroke in the C (lock keys, bare OS keys) = still waiting.</summary>
    public static AskQuitAnswer Answer(SDL.Keycode key)
    {
        if (Hotkeys.Upper(key) == (int)SDL.Keycode.Y - 32) return AskQuitAnswer.Yes;
        bool arrowOrFire = key is SDL.Keycode.Up or SDL.Keycode.Down or SDL.Keycode.Left or SDL.Keycode.Right
            or SDL.Keycode.LShift or SDL.Keycode.RShift or SDL.Keycode.LCtrl or SDL.Keycode.RCtrl
            or SDL.Keycode.LAlt or SDL.Keycode.RAlt;
        return arrowOrFire || !Hotkeys.IsNeutral(key) ? AskQuitAnswer.No : AskQuitAnswer.Pending;
    }

    /// <summary>drawwin + drawshape(inside, 0x210b, 10, 3). If the episode lacks the picture (they all have it),
    /// a "QUIT? (Y/N)" text line stands in.</summary>
    public static void Render(VgaScreen screen, ShapeManager shapes, TextRenderer text)
    {
        var win = Frame();
        win.Draw(screen, shapes);
        if (shapes.GetShape(0x21, 0x0b) is not null) shapes.DrawShape(screen, win.Inside, 0x210b, 10, 3);
        else text.Draw(screen, win.Inside, 30, 5, TextRenderer.Font8, 7, -1, "QUIT? (Y/N)");
    }
}
