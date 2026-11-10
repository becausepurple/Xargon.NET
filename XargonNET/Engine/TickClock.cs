using System.Diagnostics;
using SDL3;

namespace XargonNET.Engine;

/// <summary>
/// Paces the game loop like the DOS original, which waited on the BIOS timer tick (0040:006C,
/// 1193182/65536 = 18.2065 Hz) once per frame (<c>while (*myclock - begclock &lt; granny+1)</c>).
/// Granny mode waits two ticks per frame. Logic and presentation both run once per tick, so a
/// repeated frame at a higher display rate would look identical.
/// </summary>
public sealed class TickClock
{
    private const double TickSeconds = 65536.0 / 1193182.0;

    private readonly long _tickTicks = (long)(TickSeconds * Stopwatch.Frequency);
    private long _last = Stopwatch.GetTimestamp();
    private readonly long _epoch = Stopwatch.GetTimestamp();

    /// <summary>Free-running 18.2 Hz BIOS-style tick count (the C's *myclock), never reset.</summary>
    public long NowTicks => (Stopwatch.GetTimestamp() - _epoch) / _tickTicks;

    /// <summary>Ticks per frame: 1 normally, 2 in granny mode.</summary>
    public int TicksPerFrame { get; set; } = 1;

    /// <summary>Restart pacing (after a fade, load, or menu pause) so no catch-up burst follows.</summary>
    public void Reset() => _last = Stopwatch.GetTimestamp();

    /// <summary>Block until the current frame's tick budget has elapsed.</summary>
    public void Wait()
    {
        long deadline = _last + _tickTicks * TicksPerFrame;
        long now = Stopwatch.GetTimestamp();
        while (now < deadline)
        {
            long remainMs = (deadline - now) * 1000 / Stopwatch.Frequency;
            SDL.Delay(remainMs > 1 ? (uint)(remainMs - 1) : 1u);
            now = Stopwatch.GetTimestamp();
        }
        // A slow frame (like a slow 386) just runs late; never try to catch up.
        _last = now > deadline + _tickTicks ? now : deadline;
    }
}
