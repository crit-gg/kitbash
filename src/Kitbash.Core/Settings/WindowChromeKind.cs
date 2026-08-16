namespace Kitbash.Core.Settings;

/// <summary>Who draws a window's frame, and where its caption buttons come from.</summary>
public enum WindowChromeKind
{
    /// <summary>Kitbash draws the frame, its shadow and its caption buttons.</summary>
    Drawn = 0,

    /// <summary>The desktop draws the whole frame above the app. The Kitbash row is content.</summary>
    Desktop,

    /// <summary>
    /// The desktop draws the frame over an extended client area, so its caption buttons sit
    /// on the Kitbash title bar. The macOS traffic lights are what this exists for.
    /// </summary>
    Overlay,
}
