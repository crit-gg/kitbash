using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Kitbash.Gallery.Views.Textures;

/// <summary>
/// One cooked map, a square of linear colour. Small on purpose: a node preview is read as a
/// pattern rather than inspected, and the whole graph is cooked again on every parameter
/// move, so the size is what keeps that instant.
/// </summary>
public sealed class TextureMap
{
    public const int Side = 96;

    private readonly float[] _red = new float[Side * Side];
    private readonly float[] _green = new float[Side * Side];
    private readonly float[] _blue = new float[Side * Side];

    public static TextureMap Flat(float level)
    {
        var map = new TextureMap();

        Array.Fill(map._red, level);
        Array.Fill(map._green, level);
        Array.Fill(map._blue, level);

        return map;
    }

    /// <summary>The grey value at a pixel, wrapped, so every operator tiles.</summary>
    public float Grey(int x, int y)
    {
        var at = Wrap(x, y);

        return (_red[at] + _green[at] + _blue[at]) / 3;
    }

    public (float R, float G, float B) Colour(int x, int y)
    {
        var at = Wrap(x, y);

        return (_red[at], _green[at], _blue[at]);
    }

    public void Set(int x, int y, float level) => Set(x, y, level, level, level);

    public void Set(int x, int y, float red, float green, float blue)
    {
        var at = y * Side + x;

        _red[at] = red;
        _green[at] = green;
        _blue[at] = blue;
    }

    /// <summary>
    /// The map as something Avalonia can draw. Built once per cook and kept on the result,
    /// since a node redraws far more often than it is cooked.
    /// </summary>
    public WriteableBitmap ToBitmap()
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(Side, Side),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using var buffer = bitmap.Lock();

        var row = new byte[buffer.RowBytes];

        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var at = y * Side + x;
                var to = x * 4;

                row[to] = Byte(_blue[at]);
                row[to + 1] = Byte(_green[at]);
                row[to + 2] = Byte(_red[at]);
                row[to + 3] = 255;
            }

            System.Runtime.InteropServices.Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, row.Length);
        }

        return bitmap;
    }

    // Everything is cooked in linear light and read on a screen, so the last step out is the
    // usual approximation of sRGB. Three powers a pixel over sixteen maps is most of what the
    // whole cook costs, so the curve is a table rather than a call.
    private static readonly byte[] Shown = Curve();

    private static byte[] Curve()
    {
        var table = new byte[1025];

        for (var step = 0; step < table.Length; step++)
        {
            var level = step / (float)(table.Length - 1);
            var shown = level <= 0.0031308f ? level * 12.92f : 1.055f * MathF.Pow(level, 1 / 2.4f) - 0.055f;

            table[step] = (byte)Math.Clamp(shown * 255f + 0.5f, 0f, 255f);
        }

        return table;
    }

    private static byte Byte(float level) =>
        Shown[(int)(Math.Clamp(level, 0f, 1f) * 1024f + 0.5f)];

    private static int Wrap(int x, int y) =>
        ((y % Side + Side) % Side) * Side + (x % Side + Side) % Side;
}
