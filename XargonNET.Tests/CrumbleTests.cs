using Xunit;
using XargonNET.Objects;

namespace XargonNET.Tests;

/// <summary>Task 11: touchbkgnd crumble blocks (X_OBJMAN.C).</summary>
public class CrumbleTests
{
    // Hero with its footprint cleared and a crumble cell directly below its feet.
    private static (ObjectManager w, int x, int y) Rig(int tile)
    {
        var w = TestWorld.Load(1, "BOARD_01");
        var h = w.Obj(0);
        h.State = 0; h.X = 160; h.Y = 160;
        int sx = h.X / 16, sy = h.Y / 16, ex = (h.X + h.Xl + 15) / 16, ey = (h.Y + h.Yl + 15) / 16;
        for (int y = sy; y <= ey; y++)
            for (int x = sx; x < ex; x++) w.SetBoard(x, y, 0);
        w.SetBoard(sx, ey, tile);
        return (w, sx, ey);
    }

    [Fact]
    public void StandingOnCrumbleAdvancesItOnGamecountPhase2()
    {
        var (w, x, y) = Rig(Tiles.Crum1);
        int before = w.Objects.Count;
        w.Gamecount = 2;
        w.TouchBkgnd(0);
        Assert.Equal(Tiles.Crum1 + 1, w.BoardCell(x, y));
        Assert.Equal(before + 1, w.Objects.Count);
        Assert.Equal(ObjectKind.FragExpl, w.Obj(w.Objects.Count - 1).Kind);
        Assert.Equal(-1, w.Obj(w.Objects.Count - 1).State);
        Assert.Equal(2, w.Obj(w.Objects.Count - 1).Yd);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void CrumbleIgnoresOtherGamecountPhases(int gc)
    {
        var (w, x, y) = Rig(Tiles.Crum1);
        w.Gamecount = gc;
        w.TouchBkgnd(0);
        Assert.Equal(Tiles.Crum1, w.BoardCell(x, y));
    }

    [Fact]
    public void CrumbleDisappearsAfterPastCrumLast()
    {
        var (w, x, y) = Rig(Tiles.Crum1);
        for (int i = 0; i < Tiles.CrumLast - Tiles.Crum1; i++) { w.Gamecount = 2; w.TouchBkgnd(0); }
        Assert.Equal(Tiles.CrumLast, w.BoardCell(x, y));
        w.Gamecount = 2; w.TouchBkgnd(0);
        Assert.Equal(0, w.BoardCell(x, y));
    }

    [Fact]
    public void InvincibleStatesDoNotCrumble()
    {
        var (w, x, y) = Rig(Tiles.Crum1);
        w.Obj(0).State = 4; // st_begin
        w.Gamecount = 2; w.TouchBkgnd(0);
        Assert.Equal(Tiles.Crum1, w.BoardCell(x, y));
    }
}
