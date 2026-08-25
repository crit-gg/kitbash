using Avalonia.Media;

namespace Kitbash.Gallery.Views.Textures;

/// <summary>What a node does. The set the Rime design draws, and no more.</summary>
public enum TextureOp
{
    Tile,
    Perlin,
    Warp,
    Levels,
    Blend,
    Normal,
    Occlusion,
    GradientMap,
    HistogramScan,
    Invert,
    Output,
}

/// <summary>
/// The operators, each a plain function of its inputs and its parameters. Nothing here knows
/// about the graph: the cook walks it and hands each one what it asked for.
/// </summary>
public static class TextureMaths
{
    private const int Side = TextureMap.Side;

    /// <summary>Metal plates on a grid, with a bevel, a seam and optional rivets.</summary>
    public static TextureMap Tile(int across, int down, double bevel, double gap, bool rivets, int seed)
    {
        var map = new TextureMap();
        var wide = (double)Side / across;
        var tall = (double)Side / down;

        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var column = (int)(x / wide);
                var row = (int)(y / tall);
                var acrossPlate = x / wide - column;
                var downPlate = y / tall - row;

                // Distance to the nearest plate edge, in plate units, so a bevel reads the
                // same whatever size the plates are.
                var edge = Math.Min(
                    Math.Min(acrossPlate, 1 - acrossPlate),
                    Math.Min(downPlate, 1 - downPlate));

                var level = edge < gap
                    ? 0
                    : Smooth(Math.Clamp((edge - gap) / Math.Max(bevel, 0.001), 0, 1));

                // A little per plate variation, so a wall of them does not read as one sheet.
                level *= 0.82 + 0.18 * Random(column, row, seed);

                if (rivets && level > 0)
                {
                    level = Math.Max(level, Rivet(acrossPlate, downPlate) * level);
                }

                map.Set(x, y, (float)level);
            }
        }

        return map;
    }

    /// <summary>Value noise over four octaves, tiling by wrapping the lattice.</summary>
    public static TextureMap Perlin(int scale, int seed)
    {
        var map = new TextureMap();

        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var level = 0d;
                var weight = 0.5d;
                var cells = Math.Max(2, scale);

                for (var octave = 0; octave < 4 && cells <= Side; octave++)
                {
                    level += weight * Lattice(x * (double)cells / Side, y * (double)cells / Side, cells, seed + octave);
                    weight *= 0.5;
                    cells *= 2;
                }

                map.Set(x, y, (float)Math.Clamp(level * 1.35, 0, 1));
            }
        }

        return map;
    }

    /// <summary>Pushes one map along the gradient of another.</summary>
    public static TextureMap Warp(TextureMap input, TextureMap by, double strength)
    {
        var map = new TextureMap();
        var reach = strength * 18;

        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var acrossSlope = by.Grey(x + 1, y) - by.Grey(x - 1, y);
                var downSlope = by.Grey(x, y + 1) - by.Grey(x, y - 1);

                map.Set(
                    x,
                    y,
                    input.Grey((int)Math.Round(x + acrossSlope * reach), (int)Math.Round(y + downSlope * reach)));
            }
        }

        return map;
    }

    /// <summary>Pulls a range of the input out to the whole range, which is what makes a mask.</summary>
    public static TextureMap Levels(TextureMap input, double coverage, double contrast)
    {
        var map = new TextureMap();
        var middle = 1 - coverage;
        var width = Math.Max(0.02, 1 - contrast) * 0.5;

        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                map.Set(x, y, (float)Smooth(Math.Clamp((input.Grey(x, y) - (middle - width)) / (width * 2), 0, 1)));
            }
        }

        return map;
    }

    public static TextureMap Blend(TextureMap front, TextureMap back, TextureMap mask)
    {
        var map = new TextureMap();

        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var pick = mask.Grey(x, y);

                map.Set(x, y, back.Grey(x, y) * (1 - pick) + front.Grey(x, y) * pick);
            }
        }

        return map;
    }

    /// <summary>Height to a tangent space normal, by the usual Sobel pair.</summary>
    public static TextureMap Normal(TextureMap height, double strength)
    {
        var map = new TextureMap();

        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var acrossSlope = Sobel(height, x, y, true) * -strength;
                var downSlope = Sobel(height, x, y, false) * -strength;
                var length = Math.Sqrt(acrossSlope * acrossSlope + downSlope * downSlope + 1);

                map.Set(
                    x,
                    y,
                    (float)(acrossSlope / length * 0.5 + 0.5),
                    (float)(downSlope / length * 0.5 + 0.5),
                    (float)(1 / length * 0.5 + 0.5));
            }
        }

        return map;
    }

    /// <summary>
    /// A cheap occlusion: how far below its own neighbourhood a pixel sits. Not a ray in
    /// sight, and near enough to read as one on a preview this size.
    /// </summary>
    public static TextureMap Occlusion(TextureMap height)
    {
        var map = new TextureMap();
        const int Reach = 4;

        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var here = height.Grey(x, y);
                var above = 0d;
                var count = 0;

                for (var downStep = -Reach; downStep <= Reach; downStep += 2)
                {
                    for (var acrossStep = -Reach; acrossStep <= Reach; acrossStep += 2)
                    {
                        above += Math.Max(0, height.Grey(x + acrossStep, y + downStep) - here);
                        count++;
                    }
                }

                map.Set(x, y, (float)Math.Clamp(1 - above / count * 4.2, 0, 1));
            }
        }

        return map;
    }

    /// <summary>Grey through three stops: bare metal, rust, and the coat it was sprayed with.</summary>
    public static TextureMap GradientMap(TextureMap input, Color paint)
    {
        var map = new TextureMap();

        var metal = (0.55f, 0.56f, 0.58f);
        var rust = (0.56f, 0.29f, 0.14f);
        var coat = (Linear(paint.R), Linear(paint.G), Linear(paint.B));

        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var level = input.Grey(x, y);

                var (red, green, blue) = level < 0.5f
                    ? Mix(metal, rust, level * 2)
                    : Mix(rust, coat, (level - 0.5f) * 2);

                map.Set(x, y, red, green, blue);
            }
        }

        return map;
    }

    /// <summary>A window on the histogram, opened to the whole range. Roughness comes out of it.</summary>
    public static TextureMap HistogramScan(TextureMap input, double position, double contrast)
    {
        var map = new TextureMap();
        var width = Math.Max(0.02, 1 - contrast);

        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                map.Set(x, y, (float)Math.Clamp((input.Grey(x, y) - position + width / 2) / width, 0, 1));
            }
        }

        return map;
    }

    public static TextureMap Invert(TextureMap input)
    {
        var map = new TextureMap();

        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                map.Set(x, y, 1 - input.Grey(x, y));
            }
        }

        return map;
    }

    /// <summary>
    /// The Sobel pair, over eight taps, so the answer is divided by eight and comes back as a
    /// slope rather than a sum. Without that an intensity of one already saturates.
    /// </summary>
    private static double Sobel(TextureMap map, int x, int y, bool across)
    {
        var sum = across
            ? map.Grey(x + 1, y - 1) + 2 * map.Grey(x + 1, y) + map.Grey(x + 1, y + 1)
                - map.Grey(x - 1, y - 1) - 2 * map.Grey(x - 1, y) - map.Grey(x - 1, y + 1)
            : map.Grey(x - 1, y + 1) + 2 * map.Grey(x, y + 1) + map.Grey(x + 1, y + 1)
                - map.Grey(x - 1, y - 1) - 2 * map.Grey(x, y - 1) - map.Grey(x + 1, y - 1);

        return sum / 8;
    }

    private static double Rivet(double across, double down)
    {
        const double Reach = 0.14;

        var most = 0d;

        foreach (var (x, y) in (ReadOnlySpan<(double X, double Y)>)[(0.18, 0.18), (0.82, 0.18), (0.18, 0.82), (0.82, 0.82)])
        {
            var away = Math.Sqrt((across - x) * (across - x) + (down - y) * (down - y)) / Reach;

            if (away < 1)
            {
                most = Math.Max(most, 0.72 + 0.28 * Math.Sqrt(1 - away * away));
            }
        }

        return most > 0 ? most : 1;
    }

    private static double Lattice(double x, double y, int cells, int seed)
    {
        var left = (int)Math.Floor(x);
        var top = (int)Math.Floor(y);
        var acrossPart = Smooth(x - left);
        var downPart = Smooth(y - top);

        var topLeft = Random(left, top, seed, cells);
        var topRight = Random(left + 1, top, seed, cells);
        var bottomLeft = Random(left, top + 1, seed, cells);
        var bottomRight = Random(left + 1, top + 1, seed, cells);

        var above = topLeft + (topRight - topLeft) * acrossPart;
        var below = bottomLeft + (bottomRight - bottomLeft) * acrossPart;

        return above + (below - above) * downPart;
    }

    private static double Smooth(double at) => at * at * (3 - 2 * at);

    private static (float, float, float) Mix((float R, float G, float B) from, (float R, float G, float B) to, float at) =>
        (from.R + (to.R - from.R) * at, from.G + (to.G - from.G) * at, from.B + (to.B - from.B) * at);

    private static float Linear(byte channel)
    {
        var level = channel / 255f;

        return level <= 0.04045f ? level / 12.92f : MathF.Pow((level + 0.055f) / 1.055f, 2.4f);
    }

    private static double Random(int x, int y, int seed, int wrap = 0)
    {
        if (wrap > 0)
        {
            x = (x % wrap + wrap) % wrap;
            y = (y % wrap + wrap) % wrap;
        }

        unchecked
        {
            var hash = (uint)(x * 374761393 + y * 668265263 + seed * 1442695040);

            hash = (hash ^ (hash >> 13)) * 1274126177;

            return ((hash ^ (hash >> 16)) & 0xffffff) / (double)0xffffff;
        }
    }
}
