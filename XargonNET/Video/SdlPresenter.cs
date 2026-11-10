using SDL3;

namespace XargonNET.Video;

/// <summary>
/// Expands the indexed <see cref="VgaScreen"/> framebuffer into a single SDL streaming
/// texture (ARGB8888) once per frame and presents it. Because we write fully-packed
/// <c>uint</c> ARGB values, there is no per-channel byte-order ambiguity — this avoids the
/// class of bug the previous port hit (writing R,G,B,A bytes into an RGBA8888 texture, which
/// on little-endian actually stores A,B,G,R).
/// </summary>
public sealed class SdlPresenter : IDisposable
{
    private readonly IntPtr _renderer;
    private readonly IntPtr _texture;

    public SdlPresenter(IntPtr renderer)
    {
        _renderer = renderer;
        _texture = SDL.CreateTexture(renderer, SDL.PixelFormat.ARGB8888,
            SDL.TextureAccess.Streaming, VgaScreen.Width, VgaScreen.Height);
        if (_texture == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateTexture failed: {SDL.GetError()}");

        // Crisp pixel-art scaling instead of blur when upscaled.
        SDL.SetTextureScaleMode(_texture, SDL.ScaleMode.Nearest);
    }

    private readonly uint[] _scaledPal = new uint[256];

    /// <summary>
    /// Convert the framebuffer to ARGB and present a frame. <paramref name="brightness"/> (0..256)
    /// scales the palette for fade in/out effects; 256 = full brightness.
    /// </summary>
    public unsafe void Present(VgaScreen screen, int brightness = 256)
    {
        if (!SDL.LockTexture(_texture, IntPtr.Zero, out IntPtr pixptr, out int pitch))
            throw new InvalidOperationException($"SDL_LockTexture failed: {SDL.GetError()}");

        byte* rowBase = (byte*)pixptr;
        byte[] src = screen.Pixels;

        // Build the (optionally dimmed) palette for this frame.
        uint[] pal;
        if (brightness >= 256)
        {
            pal = screen.Palette;
        }
        else
        {
            for (int i = 0; i < 256; i++)
            {
                uint c = screen.Palette[i];
                uint r = (((c >> 16) & 0xff) * (uint)brightness) >> 8;
                uint g = (((c >> 8) & 0xff) * (uint)brightness) >> 8;
                uint b = ((c & 0xff) * (uint)brightness) >> 8;
                _scaledPal[i] = 0xFF000000u | (r << 16) | (g << 8) | b;
            }
            pal = _scaledPal;
        }

        fixed (uint* palPtr = pal)
        fixed (byte* srcPtr = src)
        {
            for (int y = 0; y < VgaScreen.Height; y++)
            {
                uint* dst = (uint*)(rowBase + y * pitch);
                byte* s = srcPtr + y * VgaScreen.Width;
                for (int x = 0; x < VgaScreen.Width; x++)
                    dst[x] = palPtr[s[x]];
            }
        }

        SDL.UnlockTexture(_texture);

        SDL.SetRenderDrawColor(_renderer, 0, 0, 0, 255);
        SDL.RenderClear(_renderer);

        // Logical presentation maps the render target to a 320x200 space, so the destination
        // rect is the full logical screen.
        SDL.FRect dstRect = new() { X = 0, Y = 0, W = VgaScreen.Width, H = VgaScreen.Height };
        SDL.RenderTexture(_renderer, _texture, IntPtr.Zero, in dstRect);
        SDL.RenderPresent(_renderer);
    }

    public void Dispose()
    {
        if (_texture != IntPtr.Zero)
            SDL.DestroyTexture(_texture);
    }
}
