using SDL3;

namespace XargonNET.Objects;

/// <summary>
/// Player input state (checkctrl0 in GAMECTRL.C). Dx1/Dy1 are -1/0/1 direction; Fire1 = shoot
/// (Shift/Space), Fire2 = jump (Ctrl/Alt). Fire1/Fire2 are edge-triggered via their Off masks:
/// once consumed (the handler sets the Off flag) the button reads 0 until physically released.
/// </summary>
public sealed class Controls
{
    public int Dx1, Dy1, Fire1, Fire2;
    public int Fire1Off, Fire2Off;

    public void Poll(HashSet<SDL.Keycode> keys)
    {
        Dx1 = 0;
        Dy1 = 0;
        if (keys.Contains(SDL.Keycode.Left)) Dx1--;
        if (keys.Contains(SDL.Keycode.Right)) Dx1++;
        if (keys.Contains(SDL.Keycode.Up)) Dy1--;
        if (keys.Contains(SDL.Keycode.Down)) Dy1++;

        bool shoot = keys.Contains(SDL.Keycode.LShift) || keys.Contains(SDL.Keycode.RShift)
                  || keys.Contains(SDL.Keycode.Space);
        bool jump = keys.Contains(SDL.Keycode.LCtrl) || keys.Contains(SDL.Keycode.RCtrl)
                 || keys.Contains(SDL.Keycode.LAlt) || keys.Contains(SDL.Keycode.RAlt);

        Fire1 = shoot ? 1 : 0;
        Fire2 = jump ? 1 : 0;

        // Edge-trigger masking (matches checkctrl0).
        if (Fire1 != 0) Fire1 ^= Fire1Off; else Fire1Off = 0;
        if (Fire2 != 0) Fire2 ^= Fire2Off; else Fire2Off = 0;
    }
}
