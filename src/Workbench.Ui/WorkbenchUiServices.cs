using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// <remarks>
    /// The library asks for the dependency injection contract rather than a container,
    /// so an application still chooses which one to build.
    /// </remarks>
    public static IServiceCollection AddWorkbenchToasts(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IToastScheduler, ToastScheduler>();
        services.TryAddSingleton(new ToastOptions());
        services.TryAddSingleton<IToastService, ToastService>();
        services.TryAddSingleton<IToastServiceFactory, ToastServiceFactory>();

        return services;
    }
}
