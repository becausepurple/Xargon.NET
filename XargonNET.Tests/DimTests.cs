using Xunit;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>VgaScreen.Dim / Undim (graphics.c dim/undim, final step only).</summary>
public class DimTests
{
    private static uint Rgb6(int r, int g, int b)
    {
        var s = new VgaScreen();
        s.SetColor6(0, r, g, b);
        return s.Palette[0];
    }

    private static void AssertScaled(VgaScreen s, int cycle)
    {
        for (int i = 0; i < 256; i++)
        {
            uint expected = Rgb6((s.Dac[i * 3] * cycle) >> 6, (s.Dac[i * 3 + 1] * cycle) >> 6, (s.Dac[i * 3 + 2] * cycle) >> 6);
            Assert.Equal(expected, s.Palette[i]);
        }
    }

    [Fact]
    public void DimScalesEveryEntryFromSourcePalette()
    {
        var s = new VgaScreen();
        s.Dim();
        AssertScaled(s, 33);
    }

    [Fact]
    public void UndimUsesCycle62()
    {
        var s = new VgaScreen();
        s.Dim();
        s.Undim();
        AssertScaled(s, 62);
    }

    [Fact]
    public void DimDiscardsLiveColourEffects()
    {
        var s = new VgaScreen();
        s.SetColor6(250, 20, 20, 23);
        s.Dim();
        AssertScaled(s, 33);
    }
}
