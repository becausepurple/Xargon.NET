using SDL3;
using XargonNET.Objects;

namespace XargonNET.Engine;

/// <summary>What a play-loop keystroke asks for (the C's <c>switch(key)</c> in play(), xargon.c:1180-1230).</summary>
public enum HotkeyAction
{
    None, Help, Save, Load, Pause, Buy, Inventory, ToggleGranny, ToggleNoise, OpenGameMenu, Cheat, ToggleDebug,
}

/// <summary>One key event as the hotkey logic sees it. <paramref name="Repeat"/> is the OS auto-repeat flag.</summary>
public readonly record struct KeyPress(SDL.Keycode Key, bool Repeat = false);

/// <summary>
/// Hotkey state: the cheat/debug streak (the C's <c>cheatchar</c>/<c>cheatcount</c>) and the toggles that are
/// process-lifetime globals in the C (<c>granny</c>, <c>soundf</c>, <c>debug</c>). NOISE and DEBUG only store
/// state: there is no audio yet and the C's debug mode only swaps the score field for free DOS memory.
/// </summary>
public sealed class HotkeyState
{
    public bool Granny { get; set; }
    public bool Noise { get; set; } = true; // soundf=1 at start
    public bool Debug { get; set; }

    /// <summary>Upper-cased code of the key the current streak counts (0 = none).</summary>
    public int StreakKey { get; set; }
    public int StreakCount { get; set; }

    /// <summary>The play-loop pace: <c>granny+1</c> ticks per frame.</summary>
    public int TicksPerFrame => Granny ? 2 : 1;
}

/// <summary>
/// Pure, SDL-free play-loop hotkey decisions. The C reads ONE keystroke per tick from the BIOS buffer; the
/// port feeds one non-repeat KeyDown per tick (the caller queues extras). Arrow keys and Shift/Ctrl/Alt (and the
/// lock keys) produce no keystroke in the C, so they neither act nor touch the streak; every other key,
/// including Space, is a keystroke that updates the streak. OS auto-repeat events are ignored entirely.
/// </summary>
public static class Hotkeys
{
    private const int F7 = (int)SDL.Keycode.F7;

    /// <summary>Keys that produce no keystroke in the C's buffer (movement/fire state only) or are lock/OS keys.</summary>
    public static bool IsNeutral(SDL.Keycode k) => k is
        SDL.Keycode.Up or SDL.Keycode.Down or SDL.Keycode.Left or SDL.Keycode.Right
        or SDL.Keycode.LShift or SDL.Keycode.RShift or SDL.Keycode.LCtrl or SDL.Keycode.RCtrl
        or SDL.Keycode.LAlt or SDL.Keycode.RAlt or SDL.Keycode.LGUI or SDL.Keycode.RGUI
        or SDL.Keycode.Capslock or SDL.Keycode.NumLockClear or SDL.Keycode.ScrollLock or SDL.Keycode.Unknown;

    /// <summary>toupper() on the key code (SDL letter codes are lower case).</summary>
    internal static int Upper(SDL.Keycode k) => k >= SDL.Keycode.A && k <= SDL.Keycode.Z ? (int)k - 32 : (int)k;

    public static HotkeyAction Decide(HotkeyState state, KeyPress press)
    {
        if (press.Repeat || IsNeutral(press.Key)) return HotkeyAction.None;
        int key = Upper(press.Key);

        // if (key==cheatchar) cheatcount++; else {cheatcount=1; cheatchar=key;}
        if (key == state.StreakKey) state.StreakCount++;
        else { state.StreakCount = 1; state.StreakKey = key; }

        if (state.StreakKey == F7 && state.StreakCount == 3)
        {
            state.StreakKey = 0; // cheatchar=0
            return HotkeyAction.Cheat;
        }
        if (state.StreakKey == 'Z' && state.StreakCount == 3)
        {
            state.StreakKey = 0;
            state.Debug = !state.Debug;
            return HotkeyAction.ToggleDebug;
        }

        switch (key)
        {
            case (int)SDL.Keycode.F1: return HotkeyAction.Help;
            case 'S': return HotkeyAction.Save;
            case 'L': return HotkeyAction.Load;
            case 'P': return HotkeyAction.Pause;
            case 'B': return HotkeyAction.Buy;
            case (int)SDL.Keycode.Return:
            case (int)SDL.Keycode.KpEnter: return HotkeyAction.Inventory;
            case 'G': state.Granny = !state.Granny; return HotkeyAction.ToggleGranny;
            case 'N': state.Noise = !state.Noise; return HotkeyAction.ToggleNoise;
            case (int)SDL.Keycode.Escape:
            case 'Q': return HotkeyAction.OpenGameMenu;
            default: return HotkeyAction.None;
        }
    }

    /// <summary>
    /// The effects of the F7 x3 cheat (xargon.c:1190-1197), applied AFTER the CHEAT MODE popup: health 5, each
    /// of the four keys and the invincibility item added only when not already held. Nothing else changes.
    /// </summary>
    public static void ApplyCheat(ObjectManager objects)
    {
        objects.Pl.Health = 5;
        for (int k = 0; k < 4; k++)
            if (objects.InvCount(Inv.Key(k)) == 0) objects.AddInv(Inv.Key(k));
        if (objects.InvCount(Inv.Invin) == 0) objects.AddInv(Inv.Invin);
    }
}
