using System;
using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>
/// Headless driver mirroring GameApp's adventure loop (no SDL): it owns a <see cref="LevelSession"/>,
/// adopts the worlds it builds, drives the correct hero controller each tick, and runs the
/// death/level transitions. Used to exercise Task 9 (death, respawn, level transitions) end to end.
/// </summary>
internal sealed class LevelHarness
{
    private readonly Viewport _vp = new(0, 2, VgaScreen.Width, 158);
    private readonly Controls _idle = new();

    public PlayerState Pl { get; }
    public LevelSession Session { get; }
    public Board Board { get; private set; } = null!;
    public ObjectManager Objects { get; private set; } = null!;
    public Player? Player { get; private set; }
    public MapHero? MapHero { get; private set; }

    public LevelHarness(Func<string, Board> loader, ShapeManager shapes, InfoTable info,
        VgaScreen screen, PlayerState pl, string startBoard, int episode = 1)
    {
        Pl = pl;
        Session = new LevelSession(loader, shapes, info, screen, _vp, pl, episode: episode);
        Adopt(Session.Load(startBoard));
    }

    public Viewport Viewport => _vp;

    private void Adopt(LevelSession.World w)
    {
        Board = w.Board;
        Objects = w.Objects;
        if (Board.Objects[0].Kind == ObjectKind.Tiny) { MapHero = new MapHero(Objects, _vp); Player = null; }
        else { Player = new Player(Objects, _vp); MapHero = null; }
    }

    public void Enter(string board) => Adopt(Session.EnterLevel(board));
    public void ExitToMap() => Adopt(Session.ExitToMap());

    /// <summary>One game tick: update the hero, then run death/transition handling exactly as GameApp does.</summary>
    public void Step() => Step(_idle);

    public void Step(Controls c)
    {
        Objects.Gamecount++;
        Objects.InputDx = c.Dx1;
        Objects.InputDy = c.Dy1;
        Objects.CurControls = c;

        MapHero?.Update(c);
        Player?.Update(c);

        if (Player?.Died == true)
        {
            Adopt(Session.Die());
            return;
        }

        Objects.UpdateObjects(_vp);
        Objects.UpdBotMsg();
        if (Objects.NewLevel is not null)
        {
            string nl = Objects.NewLevel;
            Objects.NewLevel = null;
            char c0 = nl.Length > 0 ? nl[0] : '\0';
            if (nl == "!") ExitToMap();
            else if (c0 is not ('#' or '*' or '&')) Enter(nl);
        }
    }

    /// <summary>Step until the hero reaches a state, or fail after a bound. Returns the tick count (-1 if unreached).</summary>
    public int StepUntilState(int state, int maxSteps = 200)
    {
        for (int i = 0; i < maxSteps; i++)
        {
            if (HeroState == state) return i;
            Step();
        }
        return -1;
    }

    // Helpers so tests can read the hero's current state without exposing object internals.
    public int HeroState => Objects.Obj(0).State;
    public int HeroSubState => Objects.Obj(0).SubState;
}
