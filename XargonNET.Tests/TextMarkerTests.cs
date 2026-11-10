using Xunit;
using XargonNET.Objects;
using XargonNET.Text;

namespace XargonNET.Tests;

/// <summary>Task 9: every remaining status-line txt() call site (X_OBJ.C / X_PLAYER.C), with its colour and condition.</summary>
public class TextMarkerTests
{
    private static ObjectManager World(string board = "BOARD_01")
    {
        var w = TestWorld.Load(1, board);
        w.Pl.NumInv = 0;
        return w;
    }

    private static void Clear(ObjectManager w) => w.Status.Txt("<none>", 1, 1);

    private static void AssertMsg(ObjectManager w, string msg, int col)
    {
        Assert.Equal(msg, w.Status.Message);
        Assert.Equal(col, w.Status.Color);
        Assert.Equal(0, w.Status.Flag);
        Assert.Equal(100, w.Status.Time);
    }

    private static void AssertNoMsg(ObjectManager w) => Assert.Equal("<none>", w.Status.Message);

    private static int Add(ObjectManager w, int kind, int state, int x = 100, int y = 100)
    {
        int n = w.AddObj(kind, x, y, 0, 0);
        w.Obj(n).State = state;
        return n;
    }

    // ---- Bonus ----------------------------------------------------------------------------

    [Fact]
    public void RapidFireBonus()
    {
        var w = World(); Clear(w);
        w.Dispatch(Add(w, ObjectKind.Bonus, 1), Msg.Touch, 0);
        AssertMsg(w, "You now have RAPID FIRE", 3);
    }

    [Fact]
    public void EpicHintOnFirstPiece()
    {
        var w = World(); Clear(w);
        w.Dispatch(Add(w, ObjectKind.Bonus, 9), Msg.Touch, 0);
        AssertMsg(w, "HINT: Collect EPIC poolballs in order for bonus", 3);
    }

    [Fact]
    public void FinalEpicMessageOnlyAtCountThree()
    {
        var w = World(); Clear(w);
        w.Dispatch(Add(w, ObjectKind.Bonus, 12), Msg.Touch, 0);
        AssertNoMsg(w);

        w = World(); Clear(w);
        for (int i = 0; i < 3; i++) w.AddInv(Inv.Epic);
        w.Dispatch(Add(w, ObjectKind.Bonus, 12), Msg.Touch, 0);
        AssertMsg(w, "Special EPIC bonus: 4,000 Points!", 3);
    }

    [Fact]
    public void FiveFireballs()
    {
        var w = World(); Clear(w);
        w.Dispatch(Add(w, ObjectKind.Bonus, 28), Msg.Touch, 0);
        AssertMsg(w, "You now have 5 FIREBALLS--Use them wisely", 3);
    }

    // ---- Token / Key / Heart --------------------------------------------------------------

    [Fact]
    public void TokenPostsInvGetMsg()
    {
        var w = World(); Clear(w);
        w.Dispatch(Add(w, ObjectKind.Token, 7), Msg.Touch, 0);
        AssertMsg(w, GameStrings.InvGetMsg[7], 3);
    }

    [Fact]
    public void KeyPickupThenDuplicate()
    {
        var w = World(); Clear(w);
        w.Dispatch(Add(w, ObjectKind.Key, 0), Msg.Touch, 0);
        AssertMsg(w, "You found a YELLOW key!", 3);

        Clear(w);
        int k2 = Add(w, ObjectKind.Key, 0);
        Assert.Equal(0, w.Dispatch(k2, Msg.Touch, 0));
        AssertMsg(w, "You already have a key of that COLOR!", 5);
        Assert.NotEqual(ObjectKind.Killme, (int)w.Obj(k2).Kind);
    }

    [Fact]
    public void FirstHeartOnlyOnce()
    {
        var w = World(); Clear(w);
        Assert.True(w.Hints.Heart);
        w.Dispatch(Add(w, ObjectKind.Heart, 0), Msg.Touch, 0);
        Assert.Equal("HEARTS GIVE YOU HEALTH", w.Status.Message);
        Assert.Equal(7, w.Status.Color);
        Assert.False(w.Hints.Heart);

        Clear(w);
        w.Dispatch(Add(w, ObjectKind.Heart, 0), Msg.Touch, 0);
        AssertNoMsg(w);
    }

    [Fact]
    public void PlayerXfmPostsInvGetMsg()
    {
        var w = World(); Clear(w);
        w.PlayerXfm(Inv.Bird);
        AssertMsg(w, GameStrings.InvGetMsg[Inv.Bird], 7);
    }

    // ---- Doors ----------------------------------------------------------------------------

    [Fact]
    public void LockedDoorPostsDoorMsg()
    {
        var w = World(); Clear(w);
        w.Dispatch(Add(w, ObjectKind.Door, 0), Msg.Trigger, 0);
        Assert.Contains(w.Status.Message, GameStrings.DoorMsg);
        Assert.Equal(5, w.Status.Color);
        Assert.Equal(0, w.Status.Flag);
    }

    [Fact]
    public void OpenedDoorPostsOpenMsg()
    {
        var w = World(); Clear(w);
        w.AddInv(Inv.Key(0));
        w.Dispatch(Add(w, ObjectKind.Door, 0), Msg.Trigger, 0);
        Assert.Contains(w.Status.Message, GameStrings.OpenMsg);
        Assert.Equal(2, w.Status.Color);
        Assert.Equal(0, w.Status.Flag);
    }

    [Fact]
    public void DoorMessageConsumesExactlyOneRandom()
    {
        // The pick is the only XrRandom the door draws: a mirrored world stays in lock-step after it.
        var a = World(); var b = World();
        a.Dispatch(Add(a, ObjectKind.Door, 0), Msg.Trigger, 0);
        b.XrRandom(4);
        Assert.Equal(b.XrRandom(1000), a.XrRandom(1000));
    }

    private static int GateDoor(ObjectManager w)
    {
        for (int i = 0; i < w.Objects.Count; i++)
        {
            var o = w.Obj(i);
            if (o.Kind == ObjectKind.Door && w.BoardCell(o.X / 16, o.Y / 16) == Tiles.MpGate) return i;
        }
        return -1;
    }

    [Fact]
    public void MapGateMessages()
    {
        var w = World("MAP"); Clear(w);
        int d = GateDoor(w);
        w.Dispatch(d, Msg.Trigger, 0);
        AssertMsg(w, "YOU NEED A GATE KEY TO PASS", 5);

        Clear(w);
        w.AddInv(Inv.MapKey);
        w.Dispatch(d, Msg.Trigger, 0);
        AssertMsg(w, "THE GATE OPENS", 2);
    }

    // ---- Checkpt --------------------------------------------------------------------------

    [Fact]
    public void CheckptState2NeedsGateKey()
    {
        var w = World(); Clear(w);
        Add(w, ObjectKind.Token, 1, 300, 100);
        int c = Add(w, ObjectKind.Checkpt, 2);
        Assert.Equal(1, w.Dispatch(c, Msg.Touch, 0));
        AssertMsg(w, "FIND THE GATE KEY ON THIS LEVEL BEFORE YOU LEAVE", 5);
    }

    [Fact]
    public void CheckptState3NeedsPowerObject()
    {
        var w = World(); Clear(w);
        Add(w, ObjectKind.Icons, 0, 300, 100);
        int c = Add(w, ObjectKind.Checkpt, 3);
        Assert.Equal(1, w.Dispatch(c, Msg.Touch, 0));
        AssertMsg(w, "YOU CAN'T LEAVE WITHOUT THE POWER OBJECT", 5);
    }

    // ---- Elevator / Switch / Transpad -----------------------------------------------------

    [Fact]
    public void ElevatorHintOnce()
    {
        var w = World(); Clear(w);
        w.InputDy = 0;
        int e = Add(w, ObjectKind.Elev, 0);
        w.Dispatch(e, Msg.Touch, 0);
        AssertMsg(w, "Press UP/DOWN to activate TurboLift", 7);
        Assert.False(w.Hints.Elev);

        Clear(w);
        w.Dispatch(e, Msg.Touch, 0);
        AssertNoMsg(w);
    }

    [Fact]
    public void SwitchHintSetsZapHoldAndDoesNotRepost()
    {
        var w = World(); Clear(w);
        w.InputDy = 0;
        int s = Add(w, ObjectKind.Switch, 0);
        w.Dispatch(s, Msg.Touch, 0);
        AssertMsg(w, "Press UP/DOWN to toggle switch", 7);
        Assert.Equal(25, w.Obj(s).ZapHold);

        Clear(w);
        w.Dispatch(s, Msg.Touch, 0);
        AssertNoMsg(w);
    }

    [Fact]
    public void TranspadOneWay()
    {
        var w = World(); Clear(w);
        int t = Add(w, ObjectKind.TransPad, 0);
        w.Obj(t).Counter = -1;
        w.Dispatch(t, Msg.Touch, 0);
        AssertMsg(w, "This is a ONE-WAY door and cannot be entered", 7);
        Assert.Equal(25, w.Obj(t).ZapHold);

        Clear(w);
        w.Dispatch(t, Msg.Touch, 0);
        AssertNoMsg(w);
    }

    [Fact]
    public void TranspadWalkThroughDoors()
    {
        var w = World(); Clear(w);
        w.InputDy = 0;
        int t = Add(w, ObjectKind.TransPad, 0);
        w.Obj(t).Xd = 0;
        w.Dispatch(t, Msg.Touch, 0);
        AssertMsg(w, "Press the UP arrow key to walk through doors", 7);
    }

    [Fact]
    public void TranspadOtherSide()
    {
        var w = World(); Clear(w);
        w.InputDy = 0;
        int t = Add(w, ObjectKind.TransPad, 0);
        w.Obj(t).Xd = 2;
        w.Dispatch(t, Msg.Touch, 0);
        AssertMsg(w, "Press the UP arrow key to go to the other side", 3);
    }
}
