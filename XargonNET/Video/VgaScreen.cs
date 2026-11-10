namespace XargonNET.Video;

/// <summary>
/// A software VGA mode 13h screen: a 320x200 indexed (1 byte/pixel) framebuffer plus a
/// 256-entry ARGB palette. This mirrors how the original Xargon drew everything — into a
/// linear byte buffer of colour indices, with a separate DAC palette — so the drawing code
/// (drawshape, plot, clrvp, scroll, palette fades/cycling) ports almost verbatim from
/// GRAPHICS.C. Colour index 0 is transparent for shapes.
///
/// The buffer is expanded to real ARGB pixels once per frame by <see cref="SdlPresenter"/>.
/// </summary>
public sealed class VgaScreen
{
    public const int Width = 320;
    public const int Height = 200;

    /// <summary>Indexed framebuffer, row-major (y * Width + x).</summary>
    public readonly byte[] Pixels = new byte[Width * Height];

    /// <summary>Palette as packed ARGB8888 (0xAARRGGBB). Alpha is always opaque here.</summary>
    public readonly uint[] Palette = new uint[256];

    /// <summary>
    /// The source 6-bit DAC palette (the loaded vgapal). This is the immutable colour source used
    /// by palette cycling and sky presets, matching the original where setcolor() writes the live
    /// DAC hardware but leaves vgapal untouched. Only <see cref="LoadPalette6"/> writes it.
    /// </summary>
    public readonly byte[] Dac = new byte[768];

    /// <summary>The full-screen viewport (mainvp in the original).</summary>
    public readonly Viewport Main = new(0, 0, Width, Height);

    public VgaScreen()
    {
        // Start with the game's compiled-in default palette (GRAPHICS.C vgapal).
        LoadPalette6(Assets.DefaultPalette.Dac);
    }

    /// <summary>
    /// Set one palette entry from 6-bit VGA DAC components (each 0..63), matching the
    /// original setcolor(). Values are scaled to 8-bit for display.
    /// </summary>
    public void SetColor6(int index, int r6, int g6, int b6)
    {
        uint r = (uint)((r6 & 0x3f) * 255 / 63);
        uint g = (uint)((g6 & 0x3f) * 255 / 63);
        uint b = (uint)((b6 & 0x3f) * 255 / 63);
        Palette[index & 0xff] = 0xFF000000u | (r << 16) | (g << 8) | b;
    }

    /// <summary>Load a full 256-colour palette from 768 6-bit DAC bytes (R,G,B triplets). Sets both
    /// the immutable source <see cref="Dac"/> and the live display palette.</summary>
    public void LoadPalette6(ReadOnlySpan<byte> dac)
    {
        int n = Math.Min(256, dac.Length / 3);
        for (int i = 0; i < n; i++)
        {
            Dac[i * 3] = (byte)(dac[i * 3] & 0x3f);
            Dac[i * 3 + 1] = (byte)(dac[i * 3 + 1] & 0x3f);
            Dac[i * 3 + 2] = (byte)(dac[i * 3 + 2] & 0x3f);
            SetColor6(i, dac[i * 3], dac[i * 3 + 1], dac[i * 3 + 2]);
        }
    }

    /// <summary>Set live colour <paramref name="c"/> from source DAC entry <paramref name="q"/>
    /// (setcolor(c, vgapal[q]) — used by palette cycling).</summary>
    public void SetColorFromDac(int c, int q) =>
        SetColor6(c, Dac[q * 3], Dac[q * 3 + 1], Dac[q * 3 + 2]);

    /// <summary>Clear the whole framebuffer to a colour index (clrvp on mainvp).</summary>
    public void Clear(byte color) => Array.Fill(Pixels, color);

    /// <summary>Plot a single pixel (bounds-checked).</summary>
    public void Plot(int x, int y, byte color)
    {
        if ((uint)x < Width && (uint)y < Height)
            Pixels[y * Width + x] = color;
    }
}
