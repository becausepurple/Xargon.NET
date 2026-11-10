using XargonNET.Assets;

namespace XargonNET.Objects;

/// <summary>
/// Ported object behaviours (kindmsg handlers from X_OBJ.C). This first batch covers the
/// per-frame animation/interaction for pickups (emerald, heart, gems, key, epic), decorations
/// (torch, cloud), transient effects (flash, fragments, hit-hero), and the level "effects" object
/// (palette colour-cycling + sky). Enemies and weapons come in later batches; kinds without a
/// handler still render at their resting shape. Register on an <see cref="ObjectManager"/>.
/// </summary>
public sealed partial class Behaviors
{
    private readonly ObjectManager _w;

    public Behaviors(ObjectManager world)
    {
        _w = world;
        // Player forms: object 0's kind switches to these at runtime (transform tokens). They are
        // dispatched for the hero by Player.Update/Draw; registering here also mirrors the C kindmsg
        // table and reports them handled in the census.
        _w.Register(ObjectKind.HeroSwim, HeroSwim);
        _w.Register(ObjectKind.HeroBee, HeroBee);
        _w.Register(ObjectKind.Effects, EffectsObj);
        _w.Register(ObjectKind.Emerald, Emerald);
        _w.Register(ObjectKind.Heart, Heart);
        _w.Register(ObjectKind.Gems, Gems);
        _w.Register(ObjectKind.Key, Key);
        _w.Register(ObjectKind.Epic, Epic);
        _w.Register(ObjectKind.Torch, Torch);
        _w.Register(ObjectKind.Cloud, Cloud);
        _w.Register(ObjectKind.Flash, Flash);
        _w.Register(ObjectKind.FragExpl, FragExpl);
        _w.Register(ObjectKind.HitHero, HitHero);
        _w.Register(ObjectKind.Bonus, Bonus);
        _w.Register(ObjectKind.Laser, Laser);
        _w.Register(ObjectKind.Fireball, Fireball);
        _w.Register(ObjectKind.HitWall, HitWall);
        _w.Register(ObjectKind.FireExpl, FireExpl);
        _w.Register(ObjectKind.Grunt, Grunt);
        _w.Register(ObjectKind.Score, Score);
        _w.Register(ObjectKind.Text6, Text6);
        _w.Register(ObjectKind.Text8, Text8);
        _w.Register(ObjectKind.Pad, Pad);
        _w.Register(ObjectKind.Button, Button);
        _w.Register(ObjectKind.Switch, Switch);
        _w.Register(ObjectKind.Door, Door);
        _w.Register(ObjectKind.Checkpt, Checkpt);
        _w.Register(ObjectKind.Poker, Poker);
        _w.Register(ObjectKind.Token, Token);
        _w.Register(ObjectKind.Spring, Spring);
        _w.Register(ObjectKind.Rock, Rock);
        _w.Register(ObjectKind.Bat, Bat);
        _w.Register(ObjectKind.Hopper, Hopper);
        _w.Register(ObjectKind.Bee, Bee);
        _w.Register(ObjectKind.Skull, Skull);
        _w.Register(ObjectKind.Transport, Transport);
        _w.Register(ObjectKind.TransPad, Transpad);
        _w.Register(ObjectKind.Front, Front);
        _w.Register(ObjectKind.SeaRock, Searock);
        _w.Register(ObjectKind.Fire, Fire);
        _w.Register(ObjectKind.Spikes, Spikes);
        _w.Register(ObjectKind.Story1, Story1);
        _w.Register(ObjectKind.Story2, Story2);
        _w.Register(ObjectKind.Map, MapIcon);
        _w.Register(ObjectKind.MapDemo, MapDemo);
        _w.Register(ObjectKind.TxtMsg, Txtmsg);
        _w.Register(ObjectKind.Icons, Icons);
        _w.Register(ObjectKind.Eagle, Eagle);
        // Wave 2: enemy projectile, land enemies, decorative bubble, breakable box.
        _w.Register(ObjectKind.Bullet, Bullet);
        _w.Register(ObjectKind.Bubble, Bubble);
        _w.Register(ObjectKind.Biter, Biter);
        _w.Register(ObjectKind.Troll, Troll);
        _w.Register(ObjectKind.Blob, Blob);
        _w.Register(ObjectKind.Lizard, Lizard);
        _w.Register(ObjectKind.Xargbot, Xargbot);
        _w.Register(ObjectKind.Krusty, Krusty);
        _w.Register(ObjectKind.Ghoul, Ghoul);
        _w.Register(ObjectKind.Creeper, Creeper);
        _w.Register(ObjectKind.Spider, Spider);
        _w.Register(ObjectKind.Robot, Robot);
        _w.Register(ObjectKind.Box, Box);
        // Wave 3: falling / periodic hazards.
        _w.Register(ObjectKind.Stalag, Stalag);
        _w.Register(ObjectKind.Boulder, Boulder);
        _w.Register(ObjectKind.Spear, Spear);
        _w.Register(ObjectKind.Masher, Masher);
        _w.Register(ObjectKind.Ball, Ball);
        // Wave 4: moving platforms & board-builders.
        _w.Register(ObjectKind.Platform, Platform);
        _w.Register(ObjectKind.Elev, Elev);
        _w.Register(ObjectKind.Bridger, Bridger);
        _w.Register(ObjectKind.AddStep, Addstep);

        // Wave 5 (land/air enemies + timed spawner pad).
        _w.Register(ObjectKind.TimerPad, Timerpad);
        _w.Register(ObjectKind.Centipede, Centipede);
        _w.Register(ObjectKind.CentExpl, Centexpl);
        _w.Register(ObjectKind.Alien, Alien);
        _w.Register(ObjectKind.Leech, Leech);
        _w.Register(ObjectKind.Climber, Climber);
        _w.Register(ObjectKind.Turret, Turret);

        // Wave 6 (water enemies — movement constrained to water via fishdo; player-swim deferred).
        _w.Register(ObjectKind.Mine, Mine);
        _w.Register(ObjectKind.Eel, Eel);
        _w.Register(ObjectKind.BadFish, Badfish);
        _w.Register(ObjectKind.RedFish, Redfish);
        _w.Register(ObjectKind.SeaMonster, Seamonster);
    }

    private static readonly int[] Ydtab4 = { 1, 0, -1, 0 };

    /// <summary>Rock/laser touch effect by victim kind: bat/hopper/bee burst pebbles at the weapon,
    /// skulls pebbles at the victim, everything else coloured chunks at the victim.</summary>
    private void ExplodeVictim(int n, int z)
    {
        var p = _w.Obj(n);
        var v = _w.Obj(z);
        switch (v.Kind)
        {
            case ObjectKind.Bat: case ObjectKind.Hopper: case ObjectKind.Bee: _w.Explode1(p.X, p.Y, 4, 1); break;
            case ObjectKind.Skull: _w.Explode1(v.X, v.Y, 5, 1); break;
            default: _w.Explode1(v.X, v.Y, 5, 0); break;
        }
    }

    /// <summary>Kills weapon <paramref name="z"/> that struck a target; a fireball is spared when
    /// <paramref name="fireballPasses"/> is set (the C's `if (objkind!=obj_fireball) killobj(z)`).
    /// Returns true if the weapon was killed.</summary>
    internal bool ConsumeWeapon(int z, bool fireballPasses)
    {
        if (fireballPasses && _w.Obj(z).Kind == ObjectKind.Fireball) return false;
        _w.KillObj(z);
        return true;
    }

    private const int KFireball = GameFlags.Fireball, KKillable = GameFlags.Killable, KWeapon = GameFlags.Weapon;

    internal static int Floor16(int y) => y & ~15;

    private static int Table(int kind) => KindDefs.Get(kind).ShapeTable * 256;

    /// <summary>The common touch reaction: only the player (z == 0) is hurt (hitplayer(n, flg)).
    /// Returns true if the player was hurt, so callers can `return 1` exactly as the C does.</summary>
    private bool TouchHurtPlayer(int n, int z, int flg)
    {
        if (z != 0) return false;
        _w.Hitplayer(n, flg);
        return true;
    }

    /// <summary>Shared crawler movement: with a 1-in-<paramref name="seekRate"/> chance, turn toward the
    /// player (Xd = seek-x * <paramref name="xScale"/>, and Yd = seek-y unless <paramref name="setYd"/> is
    /// false: grunt and centipede discard it), then crawl horizontally, reversing at a ledge or wall.
    /// Draws exactly one XrRandom, as the inlined C did.</summary>
    private void CrawlerAi(Obj p, int n, int seekRate, int xScale, bool setYd = true)
    {
        if (_w.XrRandom(seekRate) == 0)
        {
            _w.SeekPlayer(n, out int dx, out int dy);
            p.Xd = dx * xScale;
            if (setYd) p.Yd = dy;
        }
        if (_w.Crawl(n, p.Xd, 0) == 0) p.Xd = -p.Xd;
    }

    /// <summary>One Update tick of a dying enemy's death animation: award the kind's score on the first
    /// tick (StateCount == 0), advance StateCount, and remove the object once it reaches
    /// <paramref name="frames"/>. Always returns 1 (the object was updated).</summary>
    private int DyingTick(Obj p, int n, int kind, int frames)
    {
        if (p.StateCount == 0) _w.AddScore(KindDefs.Get(kind).Score, p.X, p.Y);
        if (++p.StateCount >= frames) _w.KillObj(n);
        return 1;
    }

    /// <summary>Common pickup tail: spawn the collection Flash at (p.X + flashDx, p.Y + flashDy) with
    /// yd = <paramref name="flashYd"/>, then remove the pickup (PlayerKill(n)). Returns 1.</summary>
    private int Collect(int n, int flashDx, int flashDy, int flashYd)
    {
        var p = _w.Obj(n);
        _w.AddObj(ObjectKind.Flash, p.X + flashDx, p.Y + flashDy, 0, flashYd);
        _w.PlayerKill(n);
        return 1;
    }

    /// <summary>A trigger message from a pad consumes the pad: kills object <paramref name="z"/> only if
    /// it is a Pad (other senders, e.g. a switch or the player, are left alone).</summary>
    private void ConsumePad(int z)
    {
        if (_w.Obj(z).Kind == ObjectKind.Pad) _w.KillObj(z);
    }
}
