namespace Kitbash.Core.Settings;

/// <summary>
/// Window choices that belong to one person on one machine. These live in
/// <see cref="IApplicationSettings"/>, which has no layer to choose, so a workspace
/// can never share them. Every Kitbash window reads this, not just the launcher.
/// </summary>
public interface IWindowSettings
{
    /// <summary>
    /// Whether the desktop draws the window frame. False means Kitbash draws it.
    /// A person who prefers their desktop's own title bar, or runs a window manager
    /// that expects one, turns this on.
    /// </summary>
    bool UseNativeChrome { get; }

    /// <summary>
    /// What <see cref="UseNativeChrome"/> means on this desktop. Read once when a window is
    /// built, since nothing watches it.
    /// </summary>
    WindowChromeKind Chrome { get; }

    /// <summary>Writes <see cref="UseNativeChrome"/>. Open windows do not follow it.</summary>
    void SetUseNativeChrome(bool value);
}
