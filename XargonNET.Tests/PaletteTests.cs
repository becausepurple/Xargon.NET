using Xunit;
using XargonNET.Objects;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>Task 11: upd_colors / msg_effects pulse + lightning / the ouched hero-colour flash.</summary>
public class PaletteTests
{
    private static uint Rgb6(int r, int g, int b)
    {
        var s = new VgaScreen();
        s.SetColor6(0, r, g, b);
        return s.Palette[0];
    }

    private static uint FromDac(VgaScreen s, int q) => Rgb6(s.Dac[q * 3], s.Dac[q * 3 + 1], s.Dac[q * 3 + 2]);

    private static VgaScreen Distinct()
    {
        var s = new VgaScreen();
        var dac = new byte[768];
        for (int c = 0; c < 256; c++) { dac[c * 3] = (byte)(c % 64); dac[c * 3 + 1] = (byte)((c * 7) % 64); dac[c * 3 + 2] = (byte)((c * 13) % 64); }
        s.LoadPalette6(dac);
        return s;
    }

    [Fact]
    public void UpdColorsCase3CyclesAllFourRanges()
    {
        var s = Distinct();
        Effects.UpdColors(s, 3, 1);
        for (int c = 236; c < 240; c++) Assert.Equal(FromDac(s, 236 + (((c + 1) & 7) / 2)), s.Palette[c]);
        for (int c = 234; c < 236; c++) Assert.Equal(FromDac(s, 234 + (((c + 1) & 3) / 2)), s.Palette[c]);
        for (int c = 240; c < 248; c++) Assert.Equal(FromDac(s, 240 + ((c + 1) & 7)), s.Palette[c]);
        for (int c = 192; c < 200; c++) Assert.Equal(FromDac(s, 192 + ((c + 1) & 7)), s.Palette[c]);
        Assert.NotEqual(FromDac(s, 236), s.Palette[236]); // 236 really moved (took entry 238)
    }

    [Fact]
    public void UpdColorsCase0LeavesLavaAndWaterAlone()
    {
        var s = Distinct();
        var before = (uint[])s.Palette.Clone();
        Effects.UpdColors(s, 0, 1);
        for (int c = 192; c < 200; c++) Assert.Equal(before[c], s.Palette[c]);
        for (int c = 240; c < 248; c++) Assert.Equal(before[c], s.Palette[c]);
    }

    private static (ObjectManager w, int n) WithEffects(int state)
    {
        var w = TestWorld.Load(1, "BOARD_01");
        w.Pl.NumInv = 0;
        int n = w.AddObj(ObjectKind.Effects, 0, 0, 0, 0);
        w.Obj(n).State = state; w.Obj(n).Counter = 9; // counter 9: no cycling
        return (w, n);
    }

    [Fact]
    public void PulseTogglesColor249EveryFifthUpdate()
    {
        var (w, n) = WithEffects(-1);
        var e = w.Obj(n);
        e.StateCount = 4; e.SubState = 0;
        w.Dispatch(n, Msg.Update, 0);
        Assert.Equal(1, e.SubState);
        Assert.Equal(0, e.StateCount);
        Assert.Equal(Rgb6(32, 0, 0), w.Screen.Palette[249]);
        e.StateCount = 4;
        w.Dispatch(n, Msg.Update, 0);
        Assert.Equal(0, e.SubState);
        Assert.Equal(Rgb6(60, 0, 0), w.Screen.Palette[249]);
    }

    [Fact]
    public void NoPulseBeforeStatecountReachesFour()
    {
        var (w, n) = WithEffects(-1);
        var e = w.Obj(n);
        e.StateCount = 3; e.SubState = 0;
        w.Dispatch(n, Msg.Update, 0);
        Assert.Equal(0, e.SubState);
        Assert.Equal(4, e.StateCount);
    }

    [Fact]
    public void LowHealthPulsesHeroColoursRed()
    {
        var (w, n) = WithEffects(-1);
        w.Pl.Health = 1;
        w.Obj(n).StateCount = 4; w.Obj(n).SubState = 0;
        w.Dispatch(n, Msg.Update, 0);
        for (int c = 207; c < 234; c++)
            Assert.Equal(Rgb6(63, w.Screen.Dac[c * 3 + 1], w.Screen.Dac[c * 3 + 2]), w.Screen.Palette[c]);
        // Next pulse (substate 1 -> 0) requests the ouched restore via pl.ouched=-1.
        w.Obj(n).StateCount = 4;
        w.Dispatch(n, Msg.Update, 0);
        Assert.Equal(-1, w.Pl.Ouched);
    }

    [Fact]
    public void InvincibilityPulsesHeroColoursBlueAndExpires()
    {
        var (w, n) = WithEffects(-1);
        w.AddInv(Inv.Invin);
        var e = w.Obj(n);
        e.StateCount = 4; e.SubState = 0; e.Info1 = 30;
        w.Dispatch(n, Msg.Update, 0);
        for (int c = 207; c < 234; c++)
            Assert.Equal(Rgb6(w.Screen.Dac[c * 3], w.Screen.Dac[c * 3 + 1], 63), w.Screen.Palette[c]);
        Assert.Equal(0, w.InvCount(Inv.Invin)); // info1++>=30 takes the item
        Assert.Equal(-1, w.Pl.Ouched);
        Assert.Equal(0, e.Info1);
    }

    [Fact]
    public void JumpBootsPulseColour233()
    {
        var (w, n) = WithEffects(-1);
        w.AddInv(Inv.Jump);
        var e = w.Obj(n);
        e.StateCount = 4; e.SubState = 0;
        w.Dispatch(n, Msg.Update, 0);
        Assert.Equal(Rgb6(0, 15, 52), w.Screen.Palette[233]);
        e.StateCount = 4;
        w.Dispatch(n, Msg.Update, 0);
        Assert.Equal(Rgb6(8, 8, 8), w.Screen.Palette[233]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(10)]
    public void StaticSkyPresetsLatchState(int state)
    {
        var (w, n) = WithEffects(state);
        w.Dispatch(n, Msg.Update, 0);
        Assert.Equal(-1, w.Obj(n).State);
    }

    [Fact]
    public void LightningFlashesThenReturnsToBlack()
    {
        var (w, n) = WithEffects(1);
        bool flashed = false, blackAfter = false;
        uint flash = Rgb6(60, 60, 63), black = Rgb6(0, 0, 0);
        for (int i = 0; i < 400; i++)
        {
            TestWorld.Step(w, 1);
            if (w.Screen.Palette[250] == flash) flashed = true;
            else if (flashed && w.Screen.Palette[250] == black) blackAfter = true;
        }
        Assert.True(flashed && blackAfter);
        Assert.Equal(1, w.Obj(n).State); // lightning does not latch
    }

    [Fact]
    public void OuchedPositiveFlashesHeroColoursRed()
    {
        var s = new VgaScreen();
        Effects.OuchedColors(s, 3);
        for (int c = 207; c < 234; c++) Assert.Equal(Rgb6(63, s.Dac[c * 3 + 1], s.Dac[c * 3 + 2]), s.Palette[c]);
        Assert.Equal(Rgb6(63, 0, 0), s.Palette[0]);
    }

    [Fact]
    public void OuchedNegativeFlashesHeroColoursBlue()
    {
        var s = new VgaScreen();
        Effects.OuchedColors(s, -3);
        for (int c = 207; c < 234; c++) Assert.Equal(Rgb6(s.Dac[c * 3], s.Dac[c * 3 + 1], 63), s.Palette[c]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void OuchedAtPlusMinusOneRestoresHeroColours(int ouched)
    {
        var s = new VgaScreen();
        Effects.OuchedColors(s, 3);
        Effects.OuchedColors(s, ouched);
        for (int c = 207; c < 234; c++) Assert.Equal(FromDac(s, c), s.Palette[c]);
        Assert.Equal(Rgb6(0, 0, 0), s.Palette[0]);
    }
}
