using Xunit;
using XargonNET.Objects;

namespace XargonNET.Tests;

/// <summary>Task 11 carry-overs: st_begin resets the hero colours every update; landing dust rules.</summary>
public class PlayerBeginDustTests
{
    private const int StStand = 0, StJumping = 2, StBegin = 4, StDie = 5, StPlatform = 7;

    [Fact]
    public void BeginUpdateSetsOuchedMinusOneEveryTick()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        var p = new Player(w, TestWorld.Vp);
        w.Obj(0).State = StBegin; w.Obj(0).StateCount = 0;
        for (int i = 0; i < 3; i++)
        {
            w.Pl.Ouched = 4;
            p.Update(new Controls());
            Assert.Equal(-1, w.Pl.Ouched);
        }
    }

    [Theory]
    [InlineData(5, StStand, 0, 0, 0, 0, true)]
    [InlineData(0, StStand, 0, 0, 0, 0, false)]            // no dust without a counter
    [InlineData(5, StStand, 0, 0, 0, 2, false)]            // ouched>0 early return
    [InlineData(5, StBegin, 0, 0, 0, 0, false)]            // st_begin early return
    [InlineData(5, StDie, 1, 0, 0, 0, false)]              // die_bird early return
    [InlineData(5, StDie, 2, 0, 0, 0, false)]              // die_fish early return
    [InlineData(5, StDie, 0, 0, 0, 0, true)]               // die_ash falls through to the dust
    [InlineData(5, StPlatform, 0, 1, 0, 0, false)]         // moving platform pose returns early
    [InlineData(5, StPlatform, 0, 1, 1, 0, true)]          // squatting on platform: dust drawn
    [InlineData(5, StPlatform, 0, 0, 0, 0, true)]          // standing still on platform: dust drawn
    [InlineData(5, StJumping, 0, 1, 0, 0, true)]
    [InlineData(5, StStand, 0, 0, 0, -1, true)]            // ouched<0 does not suppress
    public void LandingDustRules(int counter, int state, int sub, int xd, int dy1, int ouched, bool expected) =>
        Assert.Equal(expected, Player.DrawsLandingDust(counter, state, sub, xd, dy1, ouched));
}
