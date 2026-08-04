using System.Globalization;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A colour as four floats in the sRGB space. Red, green and blue are 0 to 1 for a colour a
/// screen can show and may go above 1, which is what carries an HDR value through unchanged.
/// Alpha is 0 to 1 and never leaves it.
/// </summary>
public readonly record struct ColorValue
{
    private const double Tau = Math.PI * 2;

    /// <summary>The three constants the toe of the OKHSL lightness curve is built from.</summary>
    private const double ToeK1 = 0.206;
    private const double ToeK2 = 0.03;
    private const double ToeK3 = (1 + ToeK1) / (1 + ToeK2);

    /// <summary>
    /// Godot's own colour names, from core/math/color_names.inc, with the underscores taken
    /// out of the keys. Read rather than typed, so the values cannot drift from the engine's.
    /// </summary>
    private static readonly Dictionary<string, uint> Names = new(StringComparer.Ordinal)
    {
        ["ALICEBLUE"] = 0xF0F8FFFF, ["ANTIQUEWHITE"] = 0xFAEBD7FF, ["AQUA"] = 0x00FFFFFF,
        ["AQUAMARINE"] = 0x7FFFD4FF, ["AZURE"] = 0xF0FFFFFF, ["BEIGE"] = 0xF5F5DCFF,
        ["BISQUE"] = 0xFFE4C4FF, ["BLACK"] = 0x000000FF, ["BLANCHEDALMOND"] = 0xFFEBCDFF,
        ["BLUE"] = 0x0000FFFF, ["BLUEVIOLET"] = 0x8A2BE2FF, ["BROWN"] = 0xA52A2AFF,
        ["BURLYWOOD"] = 0xDEB887FF, ["CADETBLUE"] = 0x5F9EA0FF, ["CHARTREUSE"] = 0x7FFF00FF,
        ["CHOCOLATE"] = 0xD2691EFF, ["CORAL"] = 0xFF7F50FF, ["CORNFLOWERBLUE"] = 0x6495EDFF,
        ["CORNSILK"] = 0xFFF8DCFF, ["CRIMSON"] = 0xDC143CFF, ["CYAN"] = 0x00FFFFFF,
        ["DARKBLUE"] = 0x00008BFF, ["DARKCYAN"] = 0x008B8BFF, ["DARKGOLDENROD"] = 0xB8860BFF,
        ["DARKGRAY"] = 0xA9A9A9FF, ["DARKGREEN"] = 0x006400FF, ["DARKKHAKI"] = 0xBDB76BFF,
        ["DARKMAGENTA"] = 0x8B008BFF, ["DARKOLIVEGREEN"] = 0x556B2FFF, ["DARKORANGE"] = 0xFF8C00FF,
        ["DARKORCHID"] = 0x9932CCFF, ["DARKRED"] = 0x8B0000FF, ["DARKSALMON"] = 0xE9967AFF,
        ["DARKSEAGREEN"] = 0x8FBC8FFF, ["DARKSLATEBLUE"] = 0x483D8BFF, ["DARKSLATEGRAY"] = 0x2F4F4FFF,
        ["DARKTURQUOISE"] = 0x00CED1FF, ["DARKVIOLET"] = 0x9400D3FF, ["DEEPPINK"] = 0xFF1493FF,
        ["DEEPSKYBLUE"] = 0x00BFFFFF, ["DIMGRAY"] = 0x696969FF, ["DODGERBLUE"] = 0x1E90FFFF,
        ["FIREBRICK"] = 0xB22222FF, ["FLORALWHITE"] = 0xFFFAF0FF, ["FORESTGREEN"] = 0x228B22FF,
        ["FUCHSIA"] = 0xFF00FFFF, ["GAINSBORO"] = 0xDCDCDCFF, ["GHOSTWHITE"] = 0xF8F8FFFF,
        ["GOLD"] = 0xFFD700FF, ["GOLDENROD"] = 0xDAA520FF, ["GRAY"] = 0xBEBEBEFF,
        ["GREEN"] = 0x00FF00FF, ["GREENYELLOW"] = 0xADFF2FFF, ["HONEYDEW"] = 0xF0FFF0FF,
        ["HOTPINK"] = 0xFF69B4FF, ["INDIANRED"] = 0xCD5C5CFF, ["INDIGO"] = 0x4B0082FF,
        ["IVORY"] = 0xFFFFF0FF, ["KHAKI"] = 0xF0E68CFF, ["LAVENDER"] = 0xE6E6FAFF,
        ["LAVENDERBLUSH"] = 0xFFF0F5FF, ["LAWNGREEN"] = 0x7CFC00FF, ["LEMONCHIFFON"] = 0xFFFACDFF,
        ["LIGHTBLUE"] = 0xADD8E6FF, ["LIGHTCORAL"] = 0xF08080FF, ["LIGHTCYAN"] = 0xE0FFFFFF,
        ["LIGHTGOLDENROD"] = 0xFAFAD2FF, ["LIGHTGRAY"] = 0xD3D3D3FF, ["LIGHTGREEN"] = 0x90EE90FF,
        ["LIGHTPINK"] = 0xFFB6C1FF, ["LIGHTSALMON"] = 0xFFA07AFF, ["LIGHTSEAGREEN"] = 0x20B2AAFF,
        ["LIGHTSKYBLUE"] = 0x87CEFAFF, ["LIGHTSLATEGRAY"] = 0x778899FF, ["LIGHTSTEELBLUE"] = 0xB0C4DEFF,
        ["LIGHTYELLOW"] = 0xFFFFE0FF, ["LIME"] = 0x00FF00FF, ["LIMEGREEN"] = 0x32CD32FF,
        ["LINEN"] = 0xFAF0E6FF, ["MAGENTA"] = 0xFF00FFFF, ["MAROON"] = 0xB03060FF,
        ["MEDIUMAQUAMARINE"] = 0x66CDAAFF, ["MEDIUMBLUE"] = 0x0000CDFF, ["MEDIUMORCHID"] = 0xBA55D3FF,
        ["MEDIUMPURPLE"] = 0x9370DBFF, ["MEDIUMSEAGREEN"] = 0x3CB371FF, ["MEDIUMSLATEBLUE"] = 0x7B68EEFF,
        ["MEDIUMSPRINGGREEN"] = 0x00FA9AFF, ["MEDIUMTURQUOISE"] = 0x48D1CCFF, ["MEDIUMVIOLETRED"] = 0xC71585FF,
        ["MIDNIGHTBLUE"] = 0x191970FF, ["MINTCREAM"] = 0xF5FFFAFF, ["MISTYROSE"] = 0xFFE4E1FF,
        ["MOCCASIN"] = 0xFFE4B5FF, ["NAVAJOWHITE"] = 0xFFDEADFF, ["NAVYBLUE"] = 0x000080FF,
        ["OLDLACE"] = 0xFDF5E6FF, ["OLIVE"] = 0x808000FF, ["OLIVEDRAB"] = 0x6B8E23FF,
        ["ORANGE"] = 0xFFA500FF, ["ORANGERED"] = 0xFF4500FF, ["ORCHID"] = 0xDA70D6FF,
        ["PALEGOLDENROD"] = 0xEEE8AAFF, ["PALEGREEN"] = 0x98FB98FF, ["PALETURQUOISE"] = 0xAFEEEEFF,
        ["PALEVIOLETRED"] = 0xDB7093FF, ["PAPAYAWHIP"] = 0xFFEFD5FF, ["PEACHPUFF"] = 0xFFDAB9FF,
        ["PERU"] = 0xCD853FFF, ["PINK"] = 0xFFC0CBFF, ["PLUM"] = 0xDDA0DDFF,
        ["POWDERBLUE"] = 0xB0E0E6FF, ["PURPLE"] = 0xA020F0FF, ["REBECCAPURPLE"] = 0x663399FF,
        ["RED"] = 0xFF0000FF, ["ROSYBROWN"] = 0xBC8F8FFF, ["ROYALBLUE"] = 0x4169E1FF,
        ["SADDLEBROWN"] = 0x8B4513FF, ["SALMON"] = 0xFA8072FF, ["SANDYBROWN"] = 0xF4A460FF,
        ["SEAGREEN"] = 0x2E8B57FF, ["SEASHELL"] = 0xFFF5EEFF, ["SIENNA"] = 0xA0522DFF,
        ["SILVER"] = 0xC0C0C0FF, ["SKYBLUE"] = 0x87CEEBFF, ["SLATEBLUE"] = 0x6A5ACDFF,
        ["SLATEGRAY"] = 0x708090FF, ["SNOW"] = 0xFFFAFAFF, ["SPRINGGREEN"] = 0x00FF7FFF,
        ["STEELBLUE"] = 0x4682B4FF, ["TAN"] = 0xD2B48CFF, ["TEAL"] = 0x008080FF,
        ["THISTLE"] = 0xD8BFD8FF, ["TOMATO"] = 0xFF6347FF, ["TRANSPARENT"] = 0xFFFFFF00,
        ["TURQUOISE"] = 0x40E0D0FF, ["VIOLET"] = 0xEE82EEFF, ["WEBGRAY"] = 0x808080FF,
        ["WEBGREEN"] = 0x008000FF, ["WEBMAROON"] = 0x800000FF, ["WEBPURPLE"] = 0x800080FF,
        ["WHEAT"] = 0xF5DEB3FF, ["WHITE"] = 0xFFFFFFFF, ["WHITESMOKE"] = 0xF5F5F5FF,
        ["YELLOW"] = 0xFFFF00FF, ["YELLOWGREEN"] = 0x9ACD32FF,
    };

    public ColorValue(float r, float g, float b, float a)
    {
        R = r;
        G = g;
        B = b;
        A = Math.Clamp(a, 0f, 1f);
    }

    public ColorValue(double r, double g, double b, double a)
        : this((float)r, (float)g, (float)b, (float)a)
    {
    }

    public float R { get; }

    public float G { get; }

    public float B { get; }

    public float A { get; }

    /// <summary>The brightest of the three channels, which is what says a colour is over range.</summary>
    public float Peak => Math.Max(R, Math.Max(G, B));

    /// <summary>True while every channel is inside the range a screen can show.</summary>
    public bool IsInRange => R is >= 0 and <= 1 && G is >= 0 and <= 1 && B is >= 0 and <= 1;

    /// <summary>Brighter than a screen can show, which is what a mark on the sample says.</summary>
    public bool IsOverbright => R > 1 || G > 1 || B > 1;

    /// <summary>False when the colour has no hex, which is any channel outside 0 to 1.</summary>
    public bool HasHex => !IsOverbright && R >= 0 && G >= 0 && B >= 0;

    public static ColorValue FromColor(Color color) =>
        new(color.R / 255.0, color.G / 255.0, color.B / 255.0, color.A / 255.0);

    /// <summary>Hue in degrees, saturation and value 0 to 1. A channel over range is clamped.</summary>
    public static ColorValue FromHsv(double hue, double saturation, double value, double alpha)
    {
        hue = Wrap(hue) / 60;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);

        var chroma = value * saturation;
        var second = chroma * (1 - Math.Abs(hue % 2 - 1));
        var low = value - chroma;

        var (r, g, b) = (int)hue switch
        {
            0 => (chroma, second, 0.0),
            1 => (second, chroma, 0.0),
            2 => (0.0, chroma, second),
            3 => (0.0, second, chroma),
            4 => (second, 0.0, chroma),
            _ => (chroma, 0.0, second),
        };

        return new ColorValue(r + low, g + low, b + low, alpha);
    }

    /// <summary>Hue in degrees, saturation and lightness 0 to 1, in Ottosson's OKHSL space.</summary>
    public static ColorValue FromOkhsl(double hue, double saturation, double lightness, double alpha)
    {
        saturation = Math.Clamp(saturation, 0, 1);
        lightness = Math.Clamp(lightness, 0, 1);

        if (lightness <= 0)
        {
            return new ColorValue(0, 0, 0, alpha);
        }

        if (lightness >= 1)
        {
            return new ColorValue(1, 1, 1, alpha);
        }

        var angle = Wrap(hue) / 360 * Tau;
        var a = Math.Cos(angle);
        var b = Math.Sin(angle);
        var light = ToeInverse(lightness);

        var (zero, mid, max) = Chromas(light, a, b);

        double chroma;

        if (saturation < 0.8)
        {
            var t = saturation * 1.25;
            var k1 = 0.8 * zero;
            var k2 = 1 - k1 / mid;

            chroma = t * k1 / (1 - k2 * t);
        }
        else
        {
            var t = (saturation - 0.8) * 5;
            var k1 = 0.2 * mid * mid * 1.25 * 1.25 / zero;
            var k2 = 1 - k1 / (max - mid);

            chroma = mid + t * k1 / (1 - k2 * t);
        }

        var (lr, lg, lb) = FromOklab(light, chroma * a, chroma * b);

        return new ColorValue(ToGamma(lr), ToGamma(lg), ToGamma(lb), alpha);
    }

    /// <summary>
    /// A hex code, a name such as red, or a Color expression. The forms and the names are
    /// Godot's, so anything its own colour field accepts is accepted here.
    /// </summary>
    public static bool TryParse(string? text, out ColorValue color)
    {
        color = default;

        var trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed.StartsWith("Color", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseExpression(trimmed, out color);
        }

        var digits = trimmed.TrimStart('#');

        // The odd lengths are the ones Godot fixes up, so a code pasted from software that
        // writes them lands on the colour a person meant.
        digits = digits.Length switch
        {
            1 => new string(digits[0], 6),
            2 => string.Concat(Enumerable.Repeat(digits, 3)),
            3 or 4 => string.Concat(digits.Select(digit => new string(digit, 2))),
            5 => digits[..4],
            7 => digits[..6],
            _ => digits,
        };

        if (digits.Length is 3 or 4)
        {
            digits = string.Concat(digits.Select(digit => new string(digit, 2)));
        }

        if (digits.Length is not (6 or 8)
            || !uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return TryParseName(trimmed, out color);
        }

        var alpha = digits.Length == 8 ? (packed & 0xFF) / 255.0 : 1.0;

        if (digits.Length == 8)
        {
            packed >>= 8;
        }

        color = new ColorValue(
            ((packed >> 16) & 0xFF) / 255.0,
            ((packed >> 8) & 0xFF) / 255.0,
            (packed & 0xFF) / 255.0,
            alpha);

        return true;
    }

    /// <summary>
    /// The same colour as light, which is the space an exposure multiplies in. The transfer
    /// is sRGB's own and it is not clamped, so a channel over 1 goes through it as well.
    /// </summary>
    public ColorValue ToLinear() => new(ToLinear(R), ToLinear(G), ToLinear(B), A);

    /// <summary>The way back from light to the numbers a colour is written with.</summary>
    public ColorValue ToSrgb() => new(ToGamma(R), ToGamma(G), ToGamma(B), A);

    /// <summary>
    /// What Godot writes in a script. Alpha is left out while it is 1, which is what Godot
    /// does, and three decimals is its own precision.
    /// </summary>
    public string ToExpression() =>
        A < 1
            ? string.Create(CultureInfo.InvariantCulture, $"Color({R:0.###}, {G:0.###}, {B:0.###}, {A:0.###})")
            : string.Create(CultureInfo.InvariantCulture, $"Color({R:0.###}, {G:0.###}, {B:0.###})");

    /// <summary>
    /// The hex while there is one and the expression otherwise, which is the rule Godot's
    /// own field follows. A colour over 1 has no hex.
    /// </summary>
    public string ToText() => HasHex ? ToHex() : ToExpression();

    /// <summary>What a brush is painted with. A channel over range is clamped on the way out.</summary>
    public Color ToColor() =>
        Color.FromArgb(Byte(A), Byte(R), Byte(G), Byte(B));

    public string ToHex() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"#{Byte(R):X2}{Byte(G):X2}{Byte(B):X2}{Byte(A):X2}");

    public ColorValue WithAlpha(double alpha) => new(R, G, B, (float)alpha);

    /// <summary>Multiplies the three colour channels and leaves alpha alone.</summary>
    public ColorValue Scaled(double factor) => new(R * factor, G * factor, B * factor, A);

    /// <summary>Hue in degrees, saturation and value 0 to 1.</summary>
    public (double Hue, double Saturation, double Value) ToHsv()
    {
        var r = Math.Clamp(R, 0f, 1f);
        var g = Math.Clamp(G, 0f, 1f);
        var b = Math.Clamp(B, 0f, 1f);

        var high = Math.Max(r, Math.Max(g, b));
        var low = Math.Min(r, Math.Min(g, b));
        var chroma = high - low;

        var hue = chroma == 0 ? 0
            : high == r ? 60 * (((g - b) / chroma + 6) % 6)
            : high == g ? 60 * ((b - r) / chroma + 2)
            : 60 * ((r - g) / chroma + 4);

        return (hue, high == 0 ? 0 : chroma / high, high);
    }

    /// <summary>Hue in degrees, saturation and lightness 0 to 1, in Ottosson's OKHSL space.</summary>
    public (double Hue, double Saturation, double Lightness) ToOkhsl()
    {
        var (light, a, b) = ToOklab(
            ToLinear(Math.Clamp(R, 0f, 1f)),
            ToLinear(Math.Clamp(G, 0f, 1f)),
            ToLinear(Math.Clamp(B, 0f, 1f)));

        var chroma = Math.Sqrt(a * a + b * b);

        if (chroma <= 0 || light <= 0 || light >= 1)
        {
            return (0, 0, Toe(light));
        }

        var an = a / chroma;
        var bn = b / chroma;
        var hue = Wrap(180 + Math.Atan2(-b, -a) / Math.PI * 180);

        var (zero, mid, max) = Chromas(light, an, bn);

        double saturation;

        if (chroma < mid)
        {
            var k1 = 0.8 * zero;
            var k2 = 1 - k1 / mid;

            saturation = 0.8 * chroma / (k1 + k2 * chroma);
        }
        else
        {
            var k1 = 0.2 * mid * mid * 1.25 * 1.25 / zero;
            var k2 = 1 - k1 / (max - mid);

            saturation = 0.8 + 0.2 * (chroma - mid) / (k1 + k2 * (chroma - mid));
        }

        return (hue, saturation, Toe(light));
    }

    /// <summary>Color(r, g, b) or Color(r, g, b, a), which is what the picker writes out.</summary>
    private static bool TryParseExpression(string text, out ColorValue color)
    {
        color = default;

        var open = text.IndexOf('(');
        var close = text.LastIndexOf(')');

        if (open < 0 || close < open)
        {
            return false;
        }

        var parts = text[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length is not (3 or 4))
        {
            return false;
        }

        var channels = new double[4];

        channels[3] = 1;

        for (var part = 0; part < parts.Length; part++)
        {
            if (!double.TryParse(parts[part], NumberStyles.Float, CultureInfo.InvariantCulture, out channels[part]))
            {
                return false;
            }
        }

        color = new ColorValue(channels[0], channels[1], channels[2], channels[3]);

        return true;
    }

    /// <summary>
    /// One of Godot's 146 names. Spaces, dashes, underscores, apostrophes and dots are
    /// dropped and case is ignored, which is Godot's own normalising.
    /// </summary>
    private static bool TryParseName(string text, out ColorValue color)
    {
        var key = new string([.. text.Where(letter => !" -_'.".Contains(letter))]).ToUpperInvariant();

        if (!Names.TryGetValue(key, out var packed))
        {
            color = default;

            return false;
        }

        color = new ColorValue(
            ((packed >> 24) & 0xFF) / 255.0,
            ((packed >> 16) & 0xFF) / 255.0,
            ((packed >> 8) & 0xFF) / 255.0,
            (packed & 0xFF) / 255.0);

        return true;
    }

    private static byte Byte(double channel) =>
        (byte)Math.Round(Math.Clamp(channel, 0, 1) * 255);

    private static double Wrap(double degrees)
    {
        var wrapped = degrees % 360;

        return wrapped < 0 ? wrapped + 360 : wrapped;
    }

    private static double ToLinear(double channel) =>
        channel >= 0.04045 ? Math.Pow((channel + 0.055) / 1.055, 2.4) : channel / 12.92;

    private static double ToGamma(double channel) =>
        channel >= 0.0031308 ? 1.055 * Math.Pow(channel, 1 / 2.4) - 0.055 : 12.92 * channel;

    private static (double L, double A, double B) ToOklab(double r, double g, double b)
    {
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);

        return (
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    private static (double R, double G, double B) FromOklab(double light, double a, double b)
    {
        var l = Cube(light + 0.3963377774 * a + 0.2158037573 * b);
        var m = Cube(light - 0.1055613458 * a - 0.0638541728 * b);
        var s = Cube(light - 0.0894841775 * a - 1.2914855480 * b);

        return (
            4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
    }

    private static double Cube(double value) => value * value * value;

    private static double Toe(double light) =>
        0.5 * (ToeK3 * light - ToeK1
            + Math.Sqrt((ToeK3 * light - ToeK1) * (ToeK3 * light - ToeK1) + 4 * ToeK2 * ToeK3 * light));

    private static double ToeInverse(double light) =>
        (light * light + ToeK1 * light) / (ToeK3 * (light + ToeK2));

    /// <summary>
    /// The greatest chroma the sRGB gamut holds along one hue, for saturation 0, the middle
    /// of the ramp, and the gamut edge. Ottosson's get_Cs.
    /// </summary>
    private static (double Zero, double Mid, double Max) Chromas(double light, double a, double b)
    {
        var cusp = Cusp(a, b);
        var max = GamutEdge(a, b, light, 1, light, cusp);

        var maxS = cusp.Chroma / cusp.Light;
        var maxT = cusp.Chroma / (1 - cusp.Light);
        var k = max / Math.Min(light * maxS, (1 - light) * maxT);

        var (midS, midT) = MidLimits(a, b);
        var midA = light * midS;
        var midB = (1 - light) * midT;
        var mid = 0.9 * k * Math.Sqrt(Math.Sqrt(1 / (1 / Quad(midA) + 1 / Quad(midB))));

        var zeroA = light * 0.4;
        var zeroB = (1 - light) * 0.8;
        var zero = Math.Sqrt(1 / (1 / (zeroA * zeroA) + 1 / (zeroB * zeroB)));

        return (zero, mid, max);
    }

    private static double Quad(double value) => value * value * value * value;

    /// <summary>Where a hue leaves the sRGB gamut, in lightness and chroma. Ottosson's find_cusp.</summary>
    private static (double Light, double Chroma) Cusp(double a, double b)
    {
        var saturation = MaxSaturation(a, b);
        var (r, g, bl) = FromOklab(1, saturation * a, saturation * b);
        var light = Math.Cbrt(1 / Math.Max(r, Math.Max(g, bl)));

        return (light, light * saturation);
    }

    /// <summary>The greatest chroma over lightness one hue holds. Ottosson's compute_max_saturation.</summary>
    private static double MaxSaturation(double a, double b)
    {
        double k0, k1, k2, k3, k4, wl, wm, ws;

        if (-1.88170328 * a - 0.80936493 * b > 1)
        {
            (k0, k1, k2, k3, k4) = (1.19086277, 1.76576728, 0.59662641, 0.75515197, 0.56771245);
            (wl, wm, ws) = (4.0767416621, -3.3077115913, 0.2309699292);
        }
        else if (1.81444104 * a - 1.19445276 * b > 1)
        {
            (k0, k1, k2, k3, k4) = (0.73956515, -0.45954404, 0.08285427, 0.12541070, 0.14503204);
            (wl, wm, ws) = (-1.2684380046, 2.6097574011, -0.3413193965);
        }
        else
        {
            (k0, k1, k2, k3, k4) = (1.35733652, -0.00915799, -1.15130210, -0.50559606, 0.00692167);
            (wl, wm, ws) = (-0.0041960863, -0.7034186147, 1.7076147010);
        }

        var saturation = k0 + k1 * a + k2 * b + k3 * a * a + k4 * a * b;

        var kl = 0.3963377774 * a + 0.2158037573 * b;
        var km = -0.1055613458 * a - 0.0638541728 * b;
        var ks = -0.0894841775 * a - 1.2914855480 * b;

        var ml = 1 + saturation * kl;
        var mm = 1 + saturation * km;
        var ms = 1 + saturation * ks;

        var l = Cube(ml);
        var m = Cube(mm);
        var s = Cube(ms);

        var dl = 3 * kl * ml * ml;
        var dm = 3 * km * mm * mm;
        var ds = 3 * ks * ms * ms;

        var ddl = 6 * kl * kl * ml;
        var ddm = 6 * km * km * mm;
        var dds = 6 * ks * ks * ms;

        var f = wl * l + wm * m + ws * s;
        var f1 = wl * dl + wm * dm + ws * ds;
        var f2 = wl * ddl + wm * ddm + ws * dds;

        return saturation - f * f1 / (f1 * f1 - 0.5 * f * f2);
    }

    /// <summary>
    /// How far along a line from a grey of the same lightness the sRGB gamut ends.
    /// Ottosson's find_gamut_intersection, with the one Halley step his notes call for.
    /// </summary>
    private static double GamutEdge(
        double a, double b, double light1, double chroma1, double light0, (double Light, double Chroma) cusp)
    {
        if ((light1 - light0) * cusp.Chroma - (cusp.Light - light0) * chroma1 <= 0)
        {
            return cusp.Chroma * light0 / (chroma1 * cusp.Light + cusp.Chroma * (light0 - light1));
        }

        var t = cusp.Chroma * (light0 - 1) / (chroma1 * (cusp.Light - 1) + cusp.Chroma * (light0 - light1));

        var kl = 0.3963377774 * a + 0.2158037573 * b;
        var km = -0.1055613458 * a - 0.0638541728 * b;
        var ks = -0.0894841775 * a - 1.2914855480 * b;

        var dlight = light1 - light0;
        var ldt = dlight + chroma1 * kl;
        var mdt = dlight + chroma1 * km;
        var sdt = dlight + chroma1 * ks;

        var light = light0 * (1 - t) + t * light1;
        var chroma = t * chroma1;

        var ml = light + chroma * kl;
        var mm = light + chroma * km;
        var ms = light + chroma * ks;

        var l = Cube(ml);
        var m = Cube(mm);
        var s = Cube(ms);

        var l1 = 3 * ldt * ml * ml;
        var m1 = 3 * mdt * mm * mm;
        var s1 = 3 * sdt * ms * ms;

        var l2 = 6 * ldt * ldt * ml;
        var m2 = 6 * mdt * mdt * mm;
        var s2 = 6 * sdt * sdt * ms;

        var step = Math.Min(
            Step(4.0767416621, -3.3077115913, 0.2309699292),
            Math.Min(
                Step(-1.2684380046, 2.6097574011, -0.3413193965),
                Step(-0.0041960863, -0.7034186147, 1.7076147010)));

        return t + step;

        double Step(double wl, double wm, double ws)
        {
            var f = wl * l + wm * m + ws * s - 1;
            var f1 = wl * l1 + wm * m1 + ws * s1;
            var f2 = wl * l2 + wm * m2 + ws * s2;
            var u = f1 / (f1 * f1 - 0.5 * f * f2);

            return u >= 0 ? -f * u : double.MaxValue;
        }
    }

    /// <summary>The middle of the saturation ramp, fitted rather than derived. Ottosson's get_ST_mid.</summary>
    private static (double S, double T) MidLimits(double a, double b)
    {
        var s = 0.11516993 + 1 / (7.44778970 + 4.15901240 * b
            + a * (-2.19557347 + 1.75198401 * b
            + a * (-2.13704948 - 10.02301043 * b
            + a * (-4.24894561 + 5.38770819 * b + 4.69891013 * a))));

        var t = 0.11239642 + 1 / (1.61320320 - 0.68124379 * b
            + a * (0.40370612 + 0.90148123 * b
            + a * (-0.27087943 + 0.61223990 * b
            + a * (0.00299215 - 0.45399568 * b - 0.14661872 * a))));

        return (s, t);
    }
}
