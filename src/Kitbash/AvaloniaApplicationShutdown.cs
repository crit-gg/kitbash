using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Kitbash.Ui;

namespace Kitbash;

/// <summary>The real one, over Avalonia's own lifetime.</summary>
public sealed class AvaloniaApplicationShutdown : IApplicationShutdown
{
    private readonly IUiDispatcher _dispatcher;

    public AvaloniaApplicationShutdown(IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dispatcher = dispatcher;
    }

    // Posted, since a caller can be on any thread and the lifetime is the UI thread's.
    public void Shutdown() => _dispatcher.Post(() =>
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    });
}
