using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Kitbash.Settings;
using Kitbash.Ui;

namespace Kitbash;

/// <summary>The real one, over Avalonia's own lifetime.</summary>
public sealed class AvaloniaAfterLaunchActions : IAfterLaunchActions
{
    private readonly IApplicationShutdown _shutdown;
    private readonly IUiDispatcher _dispatcher;

    public AvaloniaAfterLaunchActions(IApplicationShutdown shutdown, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(shutdown);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _shutdown = shutdown;
        _dispatcher = dispatcher;
    }

    public void Apply(AfterLaunchAction action)
    {
        switch (action)
        {
            case AfterLaunchAction.Close:
                _shutdown.Shutdown();
                break;

            case AfterLaunchAction.Minimize:
                Minimize();
                break;
        }
    }

    // Posted, since a caller can be on any thread and the window is the UI thread's.
    private void Minimize() => _dispatcher.Post(() =>
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is { } window)
        {
            window.WindowState = WindowState.Minimized;
        }
    });
}
