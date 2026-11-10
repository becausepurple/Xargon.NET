using Xunit;
using XargonNET.Assets;
using XargonNET.Objects;

namespace XargonNET.Tests;

public class PickupTests
{
    [Theory]
    [InlineData(18, 0, 75)]
    [InlineData(19, 0, 2000)]
    [InlineData(23, 0, 50)]
    [InlineData(28, 0, 1000)]
    [InlineData(99, 0, 25)]
    [InlineData(12, 3, 4000)]
    [InlineData(12, 1, 100)]
    [InlineData(9, 0, 100)]
    [InlineData(3, 0, 120)]
    [InlineData(7, 0, 200)]
    [InlineData(2, 0, 100)]
    public void BonusScoreTable(int state, int epic, int expected) =>
        Assert.Equal(expected, Behaviors.BonusScore(state, epic));

    [Fact]
    public void InvShapeTable() =>
        Assert.Equal(new[] { 0, 1, 0, 0, 2, 3, 0, 5, 6, 7, 0, 0, 0, 23, 24, 25, 26 }, Behaviors.InvShape);

    private static (ObjectManager w, int n) Touch(int state, int epicCount, int fire = 0)
    {
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.NumInv = 0;
        for (int i = 0; i < epicCount; i++) w.AddInv(Inv.Epic);
        for (int i = 0; i < fire; i++) w.AddInv(Inv.Fire);
        int n = w.AddObj(ObjectKind.Bonus, 100, 100, 0, 0);
        w.Obj(n).State = state;
        w.Dispatch(n, Msg.Touch, 0);
        return (w, n);
    }

    [Fact]
    public void EpicPieceOutOfOrderDoesNotCount()
    {
        var (w, _) = Touch(9, 1);
        Assert.Equal(1, w.InvCount(Inv.Epic));
    }

    [Fact]
    public void EpicPieceInOrderAdvances()
    {
        var (w, _) = Touch(10, 1);
        Assert.Equal(2, w.InvCount(Inv.Epic));
        (w, _) = Touch(9, 0);
        Assert.Equal(1, w.InvCount(Inv.Epic));
    }

    [Fact]
    public void FireBonusCapsAtNine()
    {
        var (w, _) = Touch(28, 0, fire: 7);
        Assert.Equal(9, w.InvCount(Inv.Fire));
    }

    [Fact]
    public void FinalEpicAwardsScoreAndKills()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.NumInv = 0;
        for (int i = 0; i < 3; i++) w.AddInv(Inv.Epic);
        int n = w.AddObj(ObjectKind.Bonus, 100, 100, 0, 0);
        w.Obj(n).State = 12;
        uint s = w.Pl.Score;
        w.Dispatch(n, Msg.Touch, 0);
        Assert.Equal(s + 4000, w.Pl.Score);
        Assert.Equal(ObjectKind.Killme, w.Obj(n).Kind);
    }

    [Fact]
    public void FlashOffsetsDifferByState()
    {
        var (w, _) = Touch(2, 0);
        var f = FindFlash(w, 101, 100);
        Assert.Equal(1, f.Yd);
        (w, _) = Touch(10, 0);
        f = FindFlash(w, 99, 97);
        Assert.Equal(0, f.Yd);
    }

    [Fact]
    public void Bonus12BelowThreeEpicsAwardsDefaultScoreAndKeepsEpicCount()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.NumInv = 0;
        for (int i = 0; i < 2; i++) w.AddInv(Inv.Epic);
        int n = w.AddObj(ObjectKind.Bonus, 100, 100, 0, 0);
        w.Obj(n).State = 12;
        uint s = w.Pl.Score;
        w.Dispatch(n, Msg.Touch, 0);
        Assert.Equal(s + 100, w.Pl.Score);
        Assert.Equal(2, w.InvCount(Inv.Epic));
        Assert.Equal(ObjectKind.Killme, w.Obj(n).Kind);
        FindFlash(w, 99, 97);   // flash is added on both branches of case 12
    }

    private static Obj FindFlash(ObjectManager w, int x, int y)
    {
        var hits = w.Objects.Where(o => o.Kind == ObjectKind.Flash && o.X == x && o.Y == y).ToList();
        Assert.Single(hits);
        return hits[0];
    }
}
