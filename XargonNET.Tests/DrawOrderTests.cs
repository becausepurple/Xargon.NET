using Xunit;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>Task 11: refresh() draw order (back, normal in REVERSE incl. the hero, then front).</summary>
public class DrawOrderTests
{
    private static ObjectManager World() => TestWorld.Load(1, "BOARD_01");

    [Fact]
    public void LowerIndexObjectIsDrawnLastInNormalLayer()
    {
        var w = World();
        int a = w.AddObj(ObjectKind.Bonus, 100, 100, 0, 0);
        int b = w.AddObj(ObjectKind.Bonus, 104, 100, 0, 0);
        var trace = new List<int>();
        w.DrawTrace = trace;
        w.DrawFore(w.Screen, TestWorld.Vp);
        Assert.True(a < b);
        Assert.True(trace.IndexOf(b) >= 0 && trace.IndexOf(a) >= 0);
        Assert.True(trace.IndexOf(b) < trace.IndexOf(a), "higher index drawn first, lower index drawn on top");
    }

    [Fact]
    public void PlayerIsDrawnAfterNormalObjectsButBeforeFrontObjects()
    {
        var w = World();
        int normal = w.AddObj(ObjectKind.Bonus, 100, 100, 0, 0);
        int front = w.AddObj(ObjectKind.FragExpl, 100, 100, 0, 0);
        var trace = new List<int>();
        w.DrawTrace = trace;
        int calls = 0;
        w.DrawFore(w.Screen, TestWorld.Vp, () => calls++);
        Assert.Equal(1, calls);
        Assert.True(trace.IndexOf(normal) < trace.IndexOf(0));
        Assert.True(trace.IndexOf(0) < trace.IndexOf(front));
    }

    [Fact]
    public void FrontObjectsAreDrawnInAscendingOrder()
    {
        var w = World();
        int f1 = w.AddObj(ObjectKind.FragExpl, 100, 100, 0, 0);
        int f2 = w.AddObj(ObjectKind.FragExpl, 100, 100, 0, 0);
        var trace = new List<int>();
        w.DrawTrace = trace;
        w.DrawFore(w.Screen, TestWorld.Vp);
        Assert.True(trace.IndexOf(f1) < trace.IndexOf(f2));
    }
}
