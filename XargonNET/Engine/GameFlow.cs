using XargonNET.Assets;
using XargonNET.Objects;

namespace XargonNET.Engine;

/// <summary>Why a play session ended (the C's <c>gameover</c>: 0 none, 1 quit/dead, 2 won).</summary>
public enum GameOver { None, Quit, Won }

/// <summary>What happens after the play loop ends: the win pages (pageview(200)) and/or printhi(1).</summary>
public readonly record struct EndSteps(bool ShowWinPages, bool ShowHighScores);

/// <summary>A parsed XARGON_DEBUG_GAMEOVER request: raise <see cref="Reason"/> after <see cref="AfterTicks"/> ticks.</summary>
public readonly record struct DebugGameOver(GameOver Reason, int AfterTicks);

/// <summary>SDL-free decisions for the end-of-game flow (play() tail, xargon.c).</summary>
public static class GameFlow
{
    public const int WinPage = 200;
    public const string DebugEnvVar = "XARGON_DEBUG_GAMEOVER";
    public const int DebugDefaultTicks = 90;

    /// <summary>play() tail: <c>if (gameover==2) pageview(200); if (!demoflg) printhi(1);</c>.
    /// A demo shows neither (its loop ends via gameover=1 and printhi is skipped).</summary>
    public static EndSteps Plan(GameOver reason, bool demo) =>
        demo || reason == GameOver.None
            ? new EndSteps(false, false)
            : new EndSteps(reason == GameOver.Won, true);

    /// <summary>Consume <see cref="ObjectManager.GameWon"/> (the checkpoint state 4 "gameover=2"): returns
    /// <see cref="GameOver.Won"/> once, then <see cref="GameOver.None"/> again.</summary>
    public static GameOver PollGameWon(ObjectManager objects)
    {
        if (!objects.GameWon) return GameOver.None;
        objects.GameWon = false;
        return GameOver.Won;
    }

    /// <summary>The C's map test <c>objs[findcheckpt(0)].state==5</c> (gamemenu save/buy, xargon.c:1078/1105): true on
    /// the overworld map. Quirk kept: <c>findcheckpt</c> returns 0 when the board has no checkpoint 0, so the test
    /// then reads the HERO's state (objs[0]).</summary>
    public static bool IsOnMap(ObjectManager objects) => objects.Obj(objects.FindCheckpt(0)).State == 5;

    /// <summary>Parses XARGON_DEBUG_GAMEOVER: <c>won</c> | <c>quit</c>, optionally <c>@TICKS</c>
    /// (default <see cref="DebugDefaultTicks"/>). Anything else yields null (no trigger).</summary>
    public static DebugGameOver? ParseDebug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string[] parts = value.Trim().ToLowerInvariant().Split('@');
        if (parts.Length > 2) return null;
        GameOver reason = parts[0] switch { "won" => GameOver.Won, "quit" => GameOver.Quit, _ => GameOver.None };
        if (reason == GameOver.None) return null;
        int ticks = DebugDefaultTicks;
        if (parts.Length == 2 && (!int.TryParse(parts[1], out ticks) || ticks < 0)) return null;
        return new DebugGameOver(reason, ticks);
    }

    /// <summary>pageview(200): the checkpoint for the first win page on the board loaded at game over, or null
    /// (the caller then skips the win pages; the C would just fall through to the Esc exit).</summary>
    public static Obj? WinAnchor(Board live) => PageView.FindAnchor(live, WinPage);

    /// <summary>
    /// The post-update stage of a play tick: <see cref="PollGameWon"/> FIRST, then (only if the game did not end)
    /// the death reload or the pending level transition. Both of those swap the world's <c>ObjectManager</c>, which
    /// would lose a <c>GameWon</c> set on the old one; in the C <c>gameover</c> ends the loop before
    /// <c>newlevel</c> is processed on the next iteration.
    /// </summary>
    public static GameOver PostUpdate(ObjectManager objects, bool died, Action die, Action transition)
    {
        var over = PollGameWon(objects);
        if (over != GameOver.None) return over;
        if (died) die();
        else if (objects.NewLevel is not null) transition();
        return GameOver.None;
    }
}
