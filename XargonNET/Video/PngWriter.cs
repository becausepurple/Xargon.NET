using System.IO.Compression;

namespace XargonNET.Video;

/// <summary>
/// Minimal 24-bit PNG writer, used for headless visual verification (dump the framebuffer to a
/// file that can be inspected). Not used by the game itself.
/// </summary>
public static class PngWriter
{
    /// <summary>Save an RGB buffer (length w*h*3, row-major) as a PNG.</summary>
    public static void SaveRgb(string path, int w, int h, byte[] rgb)
    {
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var ihdr = new byte[13];
        WriteBE(ihdr, 0, (uint)w);
        WriteBE(ihdr, 4, (uint)h);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 2;  // colour type: truecolour RGB
        WriteChunk(fs, "IHDR", ihdr);

        // Filtered scanlines (filter byte 0 = None, then RGB triplets).
        var raw = new byte[(w * 3 + 1) * h];
        int o = 0;
        for (int y = 0; y < h; y++)
        {
            raw[o++] = 0;
            Array.Copy(rgb, y * w * 3, raw, o, w * 3);
            o += w * 3;
        }

        byte[] comp;
        using (var ms = new MemoryStream())
        {
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true))
                z.Write(raw, 0, raw.Length);
            comp = ms.ToArray();
        }
        WriteChunk(fs, "IDAT", comp);
        WriteChunk(fs, "IEND", Array.Empty<byte>());
    }

    /// <summary>Expand an indexed framebuffer through its palette and save as PNG.</summary>
    public static void SaveScreen(string path, VgaScreen screen)
    {
        var rgb = new byte[VgaScreen.Width * VgaScreen.Height * 3];
        var pal = screen.Palette;
        var px = screen.Pixels;
        for (int i = 0; i < px.Length; i++)
        {
            uint argb = pal[px[i]];
            rgb[i * 3] = (byte)(argb >> 16);
            rgb[i * 3 + 1] = (byte)(argb >> 8);
            rgb[i * 3 + 2] = (byte)argb;
        }
        SaveRgb(path, VgaScreen.Width, VgaScreen.Height, rgb);
    }

    private static void WriteBE(byte[] buf, int offset, uint value)
    {
        buf[offset] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBE(len, 0, (uint)data.Length);
        s.Write(len);

        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);

        uint crc = Crc32(typeBytes, data);
        var crcb = new byte[4];
        WriteBE(crcb, 0, crc);
        s.Write(crcb);
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }

    private static uint Crc32(byte[] a, byte[] b)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte x in a) c = CrcTable[(c ^ x) & 0xff] ^ (c >> 8);
        foreach (byte x in b) c = CrcTable[(c ^ x) & 0xff] ^ (c >> 8);
        return c ^ 0xFFFFFFFF;
    }
}
