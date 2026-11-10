using Xunit;
using XargonNET.Objects;

namespace XargonNET.Tests;

/// <summary>msg_epic (explode1 before the final kill) and msg_spring (no jump-boots rule in the C).</summary>
public class EpicSpringTests
{
    [Fact]
    public void LastEpicPieceExplodesBeforeDying()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        int e = w.AddObj(ObjectKind.Epic, 100, 100, 0, 0);
        w.Obj(e).State = 12;
        int before = w.CountKind(ObjectKind.FragExpl);
        Assert.Equal(1, w.Dispatch(e, Msg.Touch, 0));
        Assert.Equal(before + 5, w.CountKind(ObjectKind.FragExpl));
        Assert.Equal(ObjectKind.Killme, (int)w.Obj(e).Kind);
    }

    [Fact]
    public void EarlierEpicPieceDoesNotExplode()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        int e = w.AddObj(ObjectKind.Epic, 100, 100, 0, 0);
        w.Obj(e).State = 3;
        int before = w.CountKind(ObjectKind.FragExpl);
        w.Dispatch(e, Msg.Touch, 0);
        Assert.Equal(before, w.CountKind(ObjectKind.FragExpl));
        Assert.Equal(ObjectKind.Epic, (int)w.Obj(e).Kind);
    }

    [Fact]
    public void SpringBouncesWithoutJumpBoots()
    {
        // msg_spring never consults invcount(inv_jump): a falling hero with an empty inventory bounces.
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.NumInv = 0;
        int s = w.AddObj(ObjectKind.Spring, 100, 100, 0, 0);
        var pl = w.Obj(0);
        pl.X = 100; pl.Y = 100 - pl.Yl + 8; pl.Yd = 3;
        Assert.Equal(0, w.InvCount(Inv.Jump));
        Assert.Equal(1, w.Dispatch(s, Msg.Touch, 0));
        Assert.Equal(-16, pl.Yd);
        Assert.Equal(10, w.Obj(s).StateCount);
    }
}
