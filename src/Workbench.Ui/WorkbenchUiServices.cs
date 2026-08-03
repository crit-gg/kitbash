using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Workbench.Ui.Settings;
using Workbench.Ui.Toasts;

namespace Workbench.Ui;

/// <summary>
/// Service registration for the control library. Each executable calls what it needs and
/// builds its own provider, which is the shape <c>WorkbenchCoreServices</c> already has.
/// Static because the language requires a container for extension methods.
/// </summary>
public static class WorkbenchUiServices
{
    /// <summary>
    /// The toast service, its clock and its settings. Register any of the three
    /// beforehand to substitute it, since these all use <c>TryAdd</c>.
    /// </summary>
    public static IServiceCollection AddWorkbenchToasts(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IToastScheduler, ToastScheduler>();
        services.TryAddSingleton(new ToastOptions());
        services.TryAddSingleton<IToastService, ToastService>();
        services.TryAddSingleton<IToastServiceFactory, ToastServiceFactory>();

        return services;
    }

    /// <summary>
    /// The settings window. It draws whatever <c>SettingsSchema</c> is registered, so an
    /// app writes a schema and one line to open it. Core's
    /// <c>AddWorkbenchSettingsSchema</c> supplies everything behind it.
    /// </summary>
    public static IServiceCollection AddWorkbenchSettingsWindow(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IApplicationRestart, ApplicationRestart>();
        services.TryAddSingleton<ISettingsWindows, SettingsWindows>();

        return services;
    }
}
