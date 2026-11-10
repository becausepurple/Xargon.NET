using System.IO.Compression;

namespace XargonNET.Video;

/// <summary>
/// Minimal PNG decoder (8-bit truecolour RGB/RGBA, filter types 0-4) for reading reference
/// images during development. Not used by the game.
/// </summary>
public static class PngReader
{
    public static (int width, int height, byte[] rgb) Load(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        int p = 8; // skip signature
        int width = 0, height = 0, channels = 3;
        byte colorType = 2;
        using var idat = new MemoryStream();

        while (p < data.Length)
        {
            int len = (data[p] << 24) | (data[p + 1] << 16) | (data[p + 2] << 8) | data[p + 3];
            string type = System.Text.Encoding.ASCII.GetString(data, p + 4, 4);
            int body = p + 8;
            if (type == "IHDR")
            {
                width = (data[body] << 24) | (data[body + 1] << 16) | (data[body + 2] << 8) | data[body + 3];
                height = (data[body + 4] << 24) | (data[body + 5] << 16) | (data[body + 6] << 8) | data[body + 7];
                colorType = data[body + 9];
                channels = colorType == 6 ? 4 : 3;
            }
            else if (type == "IDAT")
            {
                idat.Write(data, body, len);
            }
            else if (type == "IEND") break;
            p = body + len + 4; // + CRC
        }

        idat.Position = 0;
        using var z = new ZLibStream(idat, CompressionMode.Decompress);
        int stride = width * channels;
        var raw = new byte[(stride + 1) * height];
        int read = 0;
        while (read < raw.Length)
        {
            int n = z.Read(raw, read, raw.Length - read);
            if (n <= 0) break;
            read += n;
        }

        // Unfilter into RGB.
        var rgb = new byte[width * height * 3];
        var prev = new byte[stride];
        var cur = new byte[stride];
        int rp = 0;
        for (int y = 0; y < height; y++)
        {
            byte filter = raw[rp++];
            Array.Copy(raw, rp, cur, 0, stride);
            rp += stride;
            Unfilter(filter, cur, prev, channels);
            for (int x = 0; x < width; x++)
            {
                rgb[(y * width + x) * 3] = cur[x * channels];
                rgb[(y * width + x) * 3 + 1] = cur[x * channels + 1];
                rgb[(y * width + x) * 3 + 2] = cur[x * channels + 2];
            }
            (prev, cur) = (cur, prev);
        }
        return (width, height, rgb);
    }

    private static void Unfilter(byte filter, byte[] cur, byte[] prev, int bpp)
    {
        for (int i = 0; i < cur.Length; i++)
        {
            int a = i >= bpp ? cur[i - bpp] : 0;
            int b = prev[i];
            int c = i >= bpp ? prev[i - bpp] : 0;
            int val = filter switch
            {
                1 => cur[i] + a,
                2 => cur[i] + b,
                3 => cur[i] + ((a + b) >> 1),
                4 => cur[i] + Paeth(a, b, c),
                _ => cur[i],
            };
            cur[i] = (byte)val;
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        int pp = a + b - c;
        int pa = Math.Abs(pp - a), pb = Math.Abs(pp - b), pc = Math.Abs(pp - c);
        if (pa <= pb && pa <= pc) return a;
        return pb <= pc ? b : c;
    }
}
