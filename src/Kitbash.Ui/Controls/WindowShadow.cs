using Avalonia.Controls;
using Kitbash.Core.Platform;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Keeps the desktop told how much of a window is shadow rather than frame. The gutter is
/// the window's own Padding, so a frame that gives it up when maximized declares nothing
/// without anybody saying so a second time.
/// </summary>
internal sealed class WindowShadow
{
    private readonly Window _window;

    private nint _handle;
    private WindowShadowExtents _declared;

    internal WindowShadow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;

        // Extents are device pixels, so the same gutter is a different number after the
        // window moves to a display that scales differently.
        _window.ScalingChanged += OnScalingChanged;
    }

    /// <summary>Who is told, handed over when the window is built. Null tells nobody.</summary>
    internal IWindowShadow? Service { get; set; }

    /// <summary>
    /// Says what the gutter is now. Cheap to call again, since a value already declared on
    /// the same window is not written twice.
    /// </summary>
    internal void Declare()
    {
        // A handle with no descriptor is one no backend named, so there is nothing to say
        // which desktop it belongs to.
        if (Service is not { } service ||
            _window.TryGetPlatformHandle() is not { HandleDescriptor: { } kind } handle)
        {
            return;
        }

        var padding = _window.Padding;
        var scaling = _window.RenderScaling;

        var extents = new WindowShadowExtents(
            Pixels(padding.Left, scaling),
            Pixels(padding.Right, scaling),
            Pixels(padding.Top, scaling),
            Pixels(padding.Bottom, scaling));

        // Closing and showing a window again builds a new native one, so the handle is part
        // of what makes a declaration the same as the last.
        if (handle.Handle == _handle && extents == _declared)
        {
            return;
        }

        _handle = handle.Handle;
        _declared = extents;

        service.Declare(handle.Handle, kind, extents);
    }

    private static int Pixels(double length, double scaling) =>
        Math.Max(0, (int)Math.Round(length * scaling, MidpointRounding.AwayFromZero));

    private void OnScalingChanged(object? sender, EventArgs e) => Declare();
}
