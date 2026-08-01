namespace Workbench.Core.Settings;

internal sealed class WindowSettings : IWindowSettings
{
    // Named once here so the key is never a literal at a call site.
    private const string NativeChromeKey = "window.nativeChrome";

    private readonly IApplicationSettings _settings;

    public WindowSettings(IApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
    }

    // Workbench draws its own frame unless asked not to.
    public bool UseNativeChrome => _settings.Global.Get(NativeChromeKey, false);

    public void SetUseNativeChrome(bool value) =>
        _settings.Set(SettingsScope.Global, NativeChromeKey, value);
}
