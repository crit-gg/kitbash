namespace Kitbash.Core.Platform;

/// <summary>How much of a window is shadow rather than frame, in device pixels a side.</summary>
public readonly record struct WindowShadowExtents(int Left, int Right, int Top, int Bottom)
{
    /// <summary>No shadow at all, which is what a maximized window has.</summary>
    public static WindowShadowExtents None => default;
}

/// <summary>
/// Says which part of a window is the shadow the app draws itself. A desktop that is told
/// subtracts it before it snaps, tiles or maximizes, so a person drags the frame they can
/// see rather than the transparent room around it.
/// </summary>
public interface IWindowShadow
{
    /// <summary>Whether this desktop is told anything at all.</summary>
    bool CanDeclare { get; }

    /// <summary>
    /// Says how much of one window is shadow. A handle this desktop does not recognise is
    /// ignored, and nothing here throws.
    /// </summary>
    /// <param name="window">The window's native handle.</param>
    /// <param name="kind">What the toolkit calls that handle, such as XID.</param>
    /// <param name="extents">The shadow, in device pixels.</param>
    void Declare(nint window, string kind, WindowShadowExtents extents);
}
