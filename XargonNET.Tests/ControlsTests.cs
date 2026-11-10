using SDL3;
using Xunit;
using XargonNET.Assets;
using XargonNET.Objects;

namespace XargonNET.Tests;

public class ControlsTests
{
    private static Controls PollKeys(params SDL.Keycode[] keys)
    {
        var c = new Controls();
        c.Poll(new HashSet<SDL.Keycode>(keys));
        return c;
    }

    [Fact]
    public void SpaceFiresWeaponNotJump()
    {
        var c = PollKeys(SDL.Keycode.Space);
        Assert.Equal(1, c.Fire1);
        Assert.Equal(0, c.Fire2);
    }

    [Theory]
    [InlineData(SDL.Keycode.LShift)]
    [InlineData(SDL.Keycode.RShift)]
    public void ShiftFiresWeapon(SDL.Keycode k) => Assert.Equal(1, PollKeys(k).Fire1);

    [Theory]
    [InlineData(SDL.Keycode.LAlt)]
    [InlineData(SDL.Keycode.RAlt)]
    [InlineData(SDL.Keycode.LCtrl)]
    [InlineData(SDL.Keycode.RCtrl)]
    public void AltCtrlAreFire2(SDL.Keycode k)
    {
        var c = PollKeys(k);
        Assert.Equal(1, c.Fire2);
        Assert.Equal(0, c.Fire1);
    }

    [Fact]
    public void Fire1IsEdgeTriggered()
    {
        var c = new Controls();
        var keys = new HashSet<SDL.Keycode> { SDL.Keycode.Space };
        c.Poll(keys);
        Assert.Equal(1, c.Fire1);
        c.Fire1Off = 1;
        c.Poll(keys);
        Assert.Equal(0, c.Fire1);
        keys.Clear();
        c.Poll(keys);
        Assert.Equal(0, c.Fire1Off);
    }

    [Fact]
    public void Fire1ReleaseAndRepressFiresAgain()
    {
        var c = new Controls();
        var keys = new HashSet<SDL.Keycode> { SDL.Keycode.Space };
        c.Poll(keys);
        Assert.Equal(1, c.Fire1);
        c.Fire1Off = 1;
        c.Poll(keys);
        Assert.Equal(0, c.Fire1);
        keys.Clear();
        c.Poll(keys);
        Assert.Equal(0, c.Fire1Off);
        keys.Add(SDL.Keycode.Space);
        c.Poll(keys);
        Assert.Equal(1, c.Fire1);
    }

    [Fact]
    public void Fire2IsEdgeTriggered()
    {
        var c = new Controls();
        var keys = new HashSet<SDL.Keycode> { SDL.Keycode.LCtrl };
        c.Poll(keys);
        Assert.Equal(1, c.Fire2);
        c.Fire2Off = 1;
        c.Poll(keys);
        Assert.Equal(0, c.Fire2);
        Assert.Equal(1, c.Fire2Off);   // held: Off mask stays set
        keys.Clear();
        c.Poll(keys);
        Assert.Equal(0, c.Fire2Off);
        keys.Add(SDL.Keycode.LCtrl);
        c.Poll(keys);
        Assert.Equal(1, c.Fire2);
    }

    [Fact]
    public void NewGameHasOneLaser()
    {
        var p = PlayerState.NewGame();
        Assert.Equal(5, p.Health);
        Assert.Equal(0, p.Emeralds);
        Assert.Equal(1, p.NumInv);
        Assert.Equal(Inv.Laser, p.Inv[0]);
    }
}
