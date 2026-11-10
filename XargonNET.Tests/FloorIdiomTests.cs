using Xunit;
using XargonNET.Objects;

namespace XargonNET.Tests;

public class FloorIdiomTests
{
    [Theory]
    [InlineData(-5, -16)]
    [InlineData(20, 16)]
    [InlineData(16, 16)]
    [InlineData(0, 0)]
    [InlineData(15, 0)]
    public void Floor16RoundsDownToMultipleOf16(int y, int expected) =>
        Assert.Equal(expected, Behaviors.Floor16(y));
}
