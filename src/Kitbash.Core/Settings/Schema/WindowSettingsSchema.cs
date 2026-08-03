namespace Kitbash.Core.Settings.Schema;

/// <summary>
/// The window settings every Kitbash window reads. Core owns these rather than the
/// launcher, since a tool's window obeys them too, so a launcher or tool schema takes
/// <see cref="Page"/> and adds its own pages around it.
/// </summary>
public sealed class WindowSettingsSchema
{
    public WindowSettingsSchema()
    {
        NativeChrome = new SettingDescriptor<bool>
        {
            Key = "window.nativeChrome",
            Name = "Use the desktop title bar",
            Description = "Use the desktop's title bar and frame instead of the Kitbash one.",
            Default = false,
            NeedsRestart = true,
        };

        Page = new SettingsPage
        {
            Id = "window",
            Title = "Window",
            Home = SettingsHome.Application,
            Sections = [new SettingsSection("Chrome", [NativeChrome])],
        };
    }

    /// <summary>
    /// False means Kitbash draws its own frame, which is the house style. True hands
    /// it to the desktop, for a person who prefers that or a window manager expecting it.
    /// </summary>
    public SettingDescriptor<bool> NativeChrome { get; }

    public SettingsPage Page { get; }
}
