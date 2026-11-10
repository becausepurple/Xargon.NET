using Xunit;
using XargonNET.Objects;
using XargonNET.Text;

namespace XargonNET.Tests;

/// <summary>msg_door key path (x_obj.c): takeinv success sets substate=1, then substate==1 opens the door.</summary>
public class DoorTests
{
    private static ObjectManager World()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.NumInv = 0;
        w.Status.Txt("<none>", 1, 1);
        return w;
    }

    private static int AddDoor(ObjectManager w, int state, int subState)
    {
        int n = w.AddObj(ObjectKind.Door, 100, 100, 0, 0);
        w.Obj(n).State = state;
        w.Obj(n).SubState = subState;
        return n;
    }

    [Fact]
    public void KeyDoorOpensOnlyWithKeyAndConsumesIt()
    {
        var w = World();
        int d = AddDoor(w, 2, 0);
        w.Dispatch(d, Msg.Trigger, 0);
        Assert.Equal(0, w.Obj(d).StateCount);
        Assert.Contains(w.Status.Message, GameStrings.DoorMsg);

        w.AddInv(Inv.Key(2));
        Assert.Equal(1, w.Dispatch(d, Msg.Trigger, 0));
        Assert.Equal(1, w.Obj(d).StateCount);
        Assert.Equal(0, w.InvCount(Inv.Key(2)));
        Assert.Contains(w.Status.Message, GameStrings.OpenMsg);
    }

    [Fact]
    public void WrongKeyDoesNotOpen()
    {
        var w = World();
        w.AddInv(Inv.Key(1));
        int d = AddDoor(w, 0, 0);
        w.Dispatch(d, Msg.Trigger, 0);
        Assert.Equal(0, w.Obj(d).StateCount);
        Assert.Equal(1, w.InvCount(Inv.Key(1)));
    }

    [Fact]
    public void PresetSubStateOpensWithoutAKeyInInventory()
    {
        var w = World();
        int d = AddDoor(w, 0, 1);
        Assert.Equal(1, w.Dispatch(d, Msg.Trigger, 0));
        Assert.Equal(1, w.Obj(d).StateCount);
        Assert.Equal(0, w.Pl.NumInv);
        Assert.Contains(w.Status.Message, GameStrings.OpenMsg);
    }

    [Fact]
    public void PresetSubStateDoorStillTakesAMatchingKeyPerTheC()
    {
        // The C runs takeinv for states 0..3 before testing substate, so a held matching key is consumed;
        // a non-matching item is not.
        var w = World();
        w.AddInv(Inv.Key(0)); w.AddInv(Inv.Key(1));
        int d = AddDoor(w, 0, 1);
        Assert.Equal(1, w.Dispatch(d, Msg.Trigger, 0));
        Assert.Equal(0, w.InvCount(Inv.Key(0)));
        Assert.Equal(1, w.InvCount(Inv.Key(1)));
    }

    [Fact]
    public void StateOutsideKeyRangeFollowsSubStateOnly()
    {
        var w = World();
        int locked = AddDoor(w, 5, 0);
        w.Dispatch(locked, Msg.Trigger, 0);
        Assert.Equal(0, w.Obj(locked).StateCount);
        Assert.Contains(w.Status.Message, GameStrings.DoorMsg);

        int open = AddDoor(w, 5, 1);
        Assert.Equal(1, w.Dispatch(open, Msg.Trigger, 0));
        Assert.Equal(1, w.Obj(open).StateCount);
        Assert.Contains(w.Status.Message, GameStrings.OpenMsg);
    }
}
