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

    /// <summary>Eight digits with alpha, the form the picker reads back.</summary>
    public static bool TryParse(string? text, out ColorValue color)
    {
        color = default;

        var digits = text?.Trim().TrimStart('#');

        if (string.IsNullOrEmpty(digits))
        {
            return false;
        }

        // Three and four digit forms double each digit, which is the CSS rule and the one
        // every colour field on every platform accepts.
        if (digits.Length is 3 or 4)
        {
            digits = string.Concat(digits.Select(digit => new string(digit, 2)));
        }

        if (digits.Length is not (6 or 8)
            || !uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return false;
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
