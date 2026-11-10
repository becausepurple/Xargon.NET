namespace XargonNET.Assets;

/// <summary>
/// A single shape/sprite: an indexed bitmap of colour indices (1 byte/pixel), width*height
/// bytes, row-major. Colour index 0 is transparent when blitted. Ported from the per-shape
/// records in SHAPEMAN.C's xlate_table.
/// </summary>
public sealed class Shape
{
    public readonly int Width;
    public readonly int Height;
    public readonly byte[] Pixels;

    public Shape(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }
}
