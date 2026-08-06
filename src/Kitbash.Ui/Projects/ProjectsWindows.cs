using Avalonia.Controls;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Projects;
using Kitbash.Core.Settings;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Settings;

namespace Kitbash.Ui.Projects;

internal sealed class ProjectsWindows : IProjectsWindows
{
    private readonly IProjectKind _kind;
    private readonly IRecentProjects _recent;
    private readonly IWindowSettings _windows;
    private readonly IPlatformServices _platform;
    private readonly IFileSystem _files;
    private readonly ISettingsWindows? _settings;

    private ProjectsWindow? _open;

    public ProjectsWindows(
        IProjectKind kind,
        IRecentProjects recent,
        IWindowSettings windows,
        IPlatformServices platform,
        IFileSystem files,
        ISettingsWindows? settings)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(recent);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(files);

        _kind = kind;
        _recent = recent;
        _windows = windows;
        _platform = platform;
        _files = files;
        _settings = settings;
    }

    public Window Open(Window? owner)
    {
        if (_open is { } already)
        {
            already.Activate();
            return already;
        }

        // Read once here, the way every window reads it. A change to the setting takes
        // effect the next time a window opens.
        var window = new ProjectsWindow
        {
            UsesNativeChrome = _windows.UseNativeChrome,
            Kind = _kind,
            Platform = _platform,
            FileSystem = _files,
            Settings = _settings,
            DataContext = new ProjectsViewModel(_recent, _kind),
        };

        window.Closed += (_, _) => _open = null;
        _open = window;

        if (owner is null)
        {
            // Nothing to centre on and nothing else representing the app, so it places
            // itself and appears in the task bar.
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.ShowInTaskbar = true;
            window.Show();
        }
        else
        {
            window.Show(owner);
        }

        return window;
    }
}
