namespace Workbench.Ui.Toasts;

/// <summary>
/// Builds toast services that share the application's clock and its settings, and share
/// nothing else.
/// </summary>
public sealed class ToastServiceFactory : IToastServiceFactory
{
    private readonly IToastScheduler _scheduler;
    private readonly ToastOptions _options;

    public ToastServiceFactory(IToastScheduler scheduler, ToastOptions options)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(options);

        _scheduler = scheduler;
        _options = options;
    }

    public IToastService Create() => new ToastService(_scheduler, _options);
}
