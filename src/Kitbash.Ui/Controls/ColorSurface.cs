using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The three picker shapes that cannot be drawn with gradients. OKHSL is not a straight line
/// in sRGB, so every pixel is its own conversion and the surface is painted into a bitmap.
/// It belongs to <see cref="ColorPicker"/> and nothing else should place one.
/// </summary>
public class ColorSurface : Control
{
    /// <summary>Which of the three mappings is painted.</summary>
    public static readonly StyledProperty<ColorShape> KindProperty =
        AvaloniaProperty.Register<ColorSurface, ColorShape>(nameof(Kind), ColorShape.OkhslCircle);

    /// <summary>
    /// The component the surface does not carry, 0 to 1. Lightness for the circle and the
    /// hue and saturation rectangle, saturation for the hue and lightness one.
    /// </summary>
    public static readonly StyledProperty<double> ThirdProperty =
        AvaloniaProperty.Register<ColorSurface, double>(nameof(Third), 1d);

    /// <summary>
    /// How many screen pixels one painted pixel covers. The surfaces are smooth, so painting
    /// at half and letting the bitmap scale up costs a quarter of the conversions and cannot
    /// be told apart. Measured in the picker probe.
    /// </summary>
    private const int Coarse = 2;

    /// <summary>Bytes per pixel, which is what the buffer stride is counted in.</summary>
    private const int Channels = 4;

    private WriteableBitmap? _bitmap;
    private ColorShape _painted;
    private double _paintedThird = double.NaN;
    private PixelSize _paintedSize;

    static ColorSurface()
    {
        AffectsRender<ColorSurface>(KindProperty, ThirdProperty);
    }

    public ColorSurface()
    {
        // The bitmap is coarser than the control, so the upscale has to be a smooth one.
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
    }

    /// <inheritdoc cref="KindProperty"/>
    public ColorShape Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <inheritdoc cref="ThirdProperty"/>
    public double Third
    {
        get => GetValue(ThirdProperty);
        set => SetValue(ThirdProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var size = Bounds.Size;

        if (size.Width < 1 || size.Height < 1)
        {
            return;
        }

        Paint(new PixelSize(
            Math.Max(1, (int)Math.Ceiling(size.Width / Coarse)),
            Math.Max(1, (int)Math.Ceiling(size.Height / Coarse))));

        if (_bitmap is not null)
        {
            context.DrawImage(_bitmap, new Rect(size));
        }
    }

    /// <summary>Repaints only when the size, the mapping or the third component has moved.</summary>
    private void Paint(PixelSize size)
    {
        if (_bitmap is not null
            && _paintedSize == size
            && _painted == Kind
            && _paintedThird.Equals(Third))
        {
            return;
        }

        if (_bitmap is null || _paintedSize != size)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            _paintedSize = size;
        }

        _painted = Kind;
        _paintedThird = Third;

        var pixels = new byte[size.Width * size.Height * Channels];

        if (Kind == ColorShape.OkhslCircle)
        {
            Circle(pixels, size);
        }
        else
        {
            Rectangle(pixels, size);
        }

        using var buffer = _bitmap.Lock();

        Marshal.Copy(pixels, 0, buffer.Address, pixels.Length);
    }

    /// <summary>
    /// Hue around and saturation out from the middle, at one lightness. The turn is Godot's,
    /// which reads the angle with the screen's own downward y, so hue runs clockwise.
    /// </summary>
    private void Circle(byte[] pixels, PixelSize size)
    {
        var lightness = Math.Clamp(Third, 0, 1);
        var middle = new Point(size.Width / 2.0, size.Height / 2.0);
        var at = 0;

        for (var row = 0; row < size.Height; row++)
        {
            var y = row + 0.5 - middle.Y;

            for (var column = 0; column < size.Width; column++, at += Channels)
            {
                var x = column + 0.5 - middle.X;
                var radius = Math.Sqrt(x * x + y * y) / middle.X;

                // One painted pixel of feather at the edge, which is what the engine's four
                // samples come to.
                var edge = Math.Clamp((1 - radius) * middle.X, 0, 1);

                if (edge <= 0)
                {
                    continue;
                }

                var hue = Math.Atan2(y, x) * 180 / Math.PI;
                var colour = ColorValue.FromOkhsl(hue, Math.Min(radius, 1), lightness, 1);

                Put(pixels, at, colour, edge);
            }
        }
    }

    /// <summary>Hue across, and saturation or lightness up, at one of the other two.</summary>
    private void Rectangle(byte[] pixels, PixelSize size)
    {
        var third = Math.Clamp(Third, 0, 1);
        var lightness = Kind == ColorShape.OkhlRectangle;
        var at = 0;

        for (var row = 0; row < size.Height; row++)
        {
            var up = 1 - (row + 0.5) / size.Height;

            for (var column = 0; column < size.Width; column++, at += Channels)
            {
                var hue = (column + 0.5) / size.Width * 360;
                var colour = lightness
                    ? ColorValue.FromOkhsl(hue, third, up, 1)
                    : ColorValue.FromOkhsl(hue, up, third, 1);

                Put(pixels, at, colour, 1);
            }
        }
    }

    /// <summary>One pixel, blue first, with the alpha already multiplied in.</summary>
    private static void Put(byte[] pixels, int at, ColorValue colour, double cover)
    {
        var drawn = colour.ToColor();

        pixels[at] = (byte)(drawn.B * cover);
        pixels[at + 1] = (byte)(drawn.G * cover);
        pixels[at + 2] = (byte)(drawn.R * cover);
        pixels[at + 3] = (byte)(255 * cover);
    }
}
