using Xunit;
using XargonNET.Objects;

namespace XargonNET.Tests;

public class MapTests
{
    private static ObjectManager MapWorld()
    {
        var w = TestWorld.Load(1, "MAP");
        w.Pl.NumInv = 0;
        return w;
    }

    private static int FindGateDoor(ObjectManager w)
    {
        for (int i = 0; i < w.Objects.Count; i++)
        {
            var o = w.Obj(i);
            if (o.Kind == ObjectKind.Door && w.BoardCell(o.X / 16, o.Y / 16) == Tiles.MpGate) return i;
        }
        return -1;
    }

    [Fact]
    public void MapGateOpensWithMapKey()
    {
        var w = MapWorld();
        int d = FindGateDoor(w);
        Assert.True(d >= 0, "MAP has a gate door");
        var o = w.Obj(d);
        int xc = o.X / 16, yc = o.Y / 16;
        int expected = w.BoardCell(xc + o.Xd, yc + o.Yd);
        w.AddInv(Inv.MapKey);
        int r = w.Dispatch(d, Msg.Trigger, 0);
        Assert.Equal(1, r);
        Assert.Equal(expected, w.BoardCell(xc, yc));
        Assert.Equal(0, w.InvCount(Inv.MapKey));
        Assert.Equal(ObjectKind.Killme, (int)o.Kind);
    }

    [Fact]
    public void MapGateStaysWithoutKey()
    {
        var w = MapWorld();
        int d = FindGateDoor(w);
        var o = w.Obj(d);
        int xc = o.X / 16, yc = o.Y / 16;
        w.Dispatch(d, Msg.Trigger, 0);
        Assert.Equal(Tiles.MpGate, w.BoardCell(xc, yc));
        Assert.Equal(ObjectKind.Door, (int)o.Kind);
    }

    private static int Checkpoint(ObjectManager w, int state)
    {
        int c = w.AddObj(ObjectKind.Checkpt, 100, 100, 0, 0);
        var o = w.Obj(c);
        o.State = state; o.Counter = 99; o.Inside = "board_01";
        return c;
    }

    [Fact]
    public void GateKeyCheckpointBlocksWithoutKeyOpensWithKey()
    {
        var w = MapWorld();
        int c = Checkpoint(w, 2);
        int t = w.AddObj(ObjectKind.Token, 200, 200, 0, 0);
        w.Obj(t).State = 1; // gate key token still on the level
        Assert.Equal(1, w.Dispatch(c, Msg.Touch, 0));
        Assert.Null(w.NewLevel);
        w.KillObj(t);
        w.Dispatch(c, Msg.Touch, 0);
        Assert.Equal("board_01", w.NewLevel);
    }

    [Fact]
    public void IconsCheckpointBlocksWhileIconsRemain()
    {
        var w = MapWorld();
        int c = Checkpoint(w, 3);
        int i = w.AddObj(ObjectKind.Icons, 200, 200, 0, 0);
        Assert.Equal(1, w.Dispatch(c, Msg.Touch, 0));
        Assert.Null(w.NewLevel);
        w.KillObj(i);
        w.Dispatch(c, Msg.Touch, 0);
        Assert.Equal("board_01", w.NewLevel);
    }

    [Fact]
    public void EndCheckpointSetsGameWon()
    {
        var w = MapWorld();
        int c = Checkpoint(w, 4);
        Assert.False(w.GameWon);
        Assert.Equal(0, w.Dispatch(c, Msg.Touch, 0));
        Assert.True(w.GameWon);
        Assert.Null(w.NewLevel);
    }

    [Fact]
    public void MapHeroIsRepositionedToSavedCoordinates()
    {
        var w = MapWorld();
        Assert.Equal(ObjectKind.Tiny, (int)w.Obj(0).Kind);
        int c = Checkpoint(w, 0);
        var o = w.Obj(c);
        o.X = w.Obj(0).X; o.Y = w.Obj(0).Y; // overlap the hero
        w.Dispatch(c, Msg.Update, 0);
        int sx = w.Obj(0).X, sy = w.Obj(0).Y;
        Assert.Equal(sx, o.Xd);
        Assert.Equal(sy, o.Yd);
        w.Obj(0).X = 400; w.Obj(0).Y = 400;
        w.Dispatch(c, Msg.Touch, 0);
        Assert.Equal(sx, w.Obj(0).X);
        Assert.Equal(sy, w.Obj(0).Y);
    }
}
