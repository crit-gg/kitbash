namespace Kitbash.Core.Platform;

/// <summary>
/// A desktop that is never told. Windows measures snapping against the window rectangle and
/// has no shadow extent of its own, so the answer there is to have no gutter at all rather
/// than to describe one. macOS draws the frame itself and is never handed a gutter.
/// </summary>
public sealed class NoWindowShadow : IWindowShadow
{
    public bool CanDeclare => false;

    public void Declare(nint window, string kind, WindowShadowExtents extents)
    {
    }
}
