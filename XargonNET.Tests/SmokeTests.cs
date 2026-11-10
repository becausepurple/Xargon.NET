using Xunit;

namespace XargonNET.Tests;

public class SmokeTests
{
    [Fact]
    public void Level1BoardLoadsAndSteps()
    {
        var w = TestWorld.Load(1, "BOARD_01");
        TestWorld.Step(w, 30);
        Assert.Equal(0, w.Obj(0).Kind);
    }
}
