using Xunit;

namespace XargonNET.Tests;

/// <summary>The "press ESC to continue" request an object update leaves for the play loop to service.</summary>
public sealed class EscWaitTests
{
    [Fact]
    public void RequestIsTakenOnce()
    {
        var w = TestWorld.Load(1, "MAP");
        int ran = 0;
        Assert.True(w.RequestEscWait(() => ran++));

        var k = w.TakeEscWait();
        Assert.NotNull(k);
        k!();
        Assert.Equal(1, ran);
        Assert.Null(w.TakeEscWait());
    }

    [Fact]
    public void SecondRequestWhilePendingIsIgnored()
    {
        var w = TestWorld.Load(1, "MAP");
        int first = 0, second = 0;
        Assert.True(w.RequestEscWait(() => first++));
        Assert.False(w.RequestEscWait(() => second++));

        w.TakeEscWait()!();
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Null(w.TakeEscWait());
    }

    [Fact]
    public void NoRequestMeansNull() => Assert.Null(TestWorld.Load(1, "MAP").TakeEscWait());
}
