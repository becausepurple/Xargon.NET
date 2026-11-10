using Xunit;
using XargonNET.Objects;

namespace XargonNET.Tests;

public class WeaponTests
{
    private const int BreakWall = 148;

    /// <summary>Level 1 world with an empty 7x5-cell arena at cells (4..10, 3..7) and a breakable wall at (7,6).</summary>
    private static ObjectManager Arena()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        for (int y = 3; y < 8; y++)
            for (int x = 4; x < 11; x++) w.SetBoard(x, y, 0);
        w.SetBoard(7, 6, BreakWall);
        return w;
    }

    private static int Count(ObjectManager w, int kind) => w.CountKind(kind);

    [Fact]
    public void FireballBreaksWallAndSurvives()
    {
        var w = Arena();
        int n = w.AddObj(ObjectKind.Fireball, 100, 100, 4, 0);
        uint score = w.Pl.Score;
        TestWorld.Step(w, 1);
        Assert.Equal(0, w.BoardCell(7, 6));
        Assert.Equal(score + 10, w.Pl.Score);
        Assert.Equal(1, Count(w, ObjectKind.Fireball));
        Assert.Equal(1, Count(w, ObjectKind.FireExpl));
    }

    [Fact]
    public void LaserBreaksWallAndIsConsumed()
    {
        var w = Arena();
        // JustMove tests x+xd first (a solid wall there just kills the laser); the wall is only
        // touched by the post-acceleration box, so position the laser one pixel short of it.
        int xl = KindDefs.Get(ObjectKind.Laser).Xl;
        w.AddObj(ObjectKind.Laser, 111 - 4 - xl, 100, 4, 0);
        TestWorld.Step(w, 1);
        Assert.Equal(0, w.BoardCell(7, 6));
        Assert.Equal(0, Count(w, ObjectKind.Laser));
    }

    [Fact]
    public void EyeOpenIsReplacedByNoEyeAndConsumesWeapon()
    {
        var w = Arena();
        w.SetBoard(7, 6, Tiles.EyeOp);
        w.AddObj(ObjectKind.Fireball, 100, 100, 4, 0);
        uint score = w.Pl.Score;
        TestWorld.Step(w, 1);
        Assert.Equal(265, w.BoardCell(7, 6));
        Assert.Equal(score + 100, w.Pl.Score);
        Assert.Equal(0, Count(w, ObjectKind.Fireball));
        Assert.Equal(1, Count(w, ObjectKind.Flash));
    }

    [Fact]
    public void FireExplFramesFollowExptabAndDieAtNine()
    {
        int[] expected = { 0, 1, 0, 1, 0, 1, 2, 3, 4 };
        for (int c = 0; c < 9; c++) Assert.Equal(expected[c], Behaviors.FireExplFrame(c));

        var w = Arena();
        int n = w.AddObj(ObjectKind.FireExpl, 100, 100, 0, 0);
        w.Obj(n).Counter = 7;
        TestWorld.Step(w, 1);
        Assert.Equal(1, Count(w, ObjectKind.FireExpl)); // counter 8 still alive
        TestWorld.Step(w, 1);
        Assert.Equal(0, Count(w, ObjectKind.FireExpl)); // counter 9 dies
    }

    [Fact]
    public void HitWallFrameIsHalfCounterAndDiesAtSeven()
    {
        for (int c = 0; c < 7; c++) Assert.Equal(c / 2, Behaviors.HitWallFrame(c));

        var w = Arena();
        int n = w.AddObj(ObjectKind.HitWall, 100, 100, 0, 0);
        w.Obj(n).Counter = 5;
        TestWorld.Step(w, 1);
        Assert.Equal(1, Count(w, ObjectKind.HitWall)); // counter 6 alive
        TestWorld.Step(w, 1);
        Assert.Equal(0, Count(w, ObjectKind.HitWall)); // counter 7 dies
    }
}
