using System.Collections.ObjectModel;
using Dock.Model.Core;
using Dock.Serializer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Projects;
using Kitbash.Core.Settings;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Docking;
using Kitbash.Ui.Projects;
using Kitbash.Ui.Settings;
using Kitbash.Ui.Toasts;

namespace Kitbash.Ui;

/// <summary>
/// Service registration for the control library. Each executable calls what it needs and
/// builds its own provider, which is the shape <c>KitbashCoreServices</c> already has.
/// Static because the language requires a container for extension methods.
/// </summary>
public static class KitbashUiServices
{
    /// <summary>
    /// The way onto the thread that owns the views. Anything raising an event off a timer,
    /// a watch or a background read needs one before it touches a bound property.
    /// </summary>
    public static IServiceCollection AddKitbashDispatcher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();

        return services;
    }

    /// <summary>
    /// The toast service, its clock and its settings. Register any of the three
    /// beforehand to substitute it, since these all use <c>TryAdd</c>.
    /// </summary>
    public static IServiceCollection AddKitbashToasts(this IServiceCollection services)
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
    /// <c>AddKitbashSettingsSchema</c> supplies everything behind it.
    /// </summary>
    public static IServiceCollection AddKitbashSettingsWindow(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IApplicationRestart, ApplicationRestart>();
        services.TryAddSingleton<ISettingsWindows, SettingsWindows>();

        return services;
    }

    /// <summary>
    /// The welcome window, over whatever <c>IProjectKind</c> is registered. Takes Core's
    /// <c>AddKitbashRecentProjects</c> for the list and <c>AddKitbashPlatform</c> for
    /// opening a folder. Registering <c>AddKitbashSettingsWindow</c> as well is what
    /// draws the cog at the foot of the rail.
    /// </summary>
    public static IServiceCollection AddKitbashProjectsWindow(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The one lookup, and it sits in the composition root where a lookup belongs. The
        // settings window is optional and nothing else here is.
        services.TryAddSingleton<IProjectsWindows>(provider => new ProjectsWindows(
            provider.GetRequiredService<IProjectKind>(),
            provider.GetRequiredService<IRecentProjects>(),
            provider.GetRequiredService<IWindowSettings>(),
            provider.GetRequiredService<IPlatformServices>(),
            provider.GetRequiredService<IFileSystem>(),
            provider.GetService<ISettingsWindows>()));

        return services;
    }

    /// <summary>
    /// Where a dock layout is kept between runs. Takes Core's
    /// <c>AddKitbashApplicationStorage</c> and <c>AddKitbashIO</c>, since the file sits in
    /// application state. The look is a separate line, the style include in
    /// Themes/KitbashDocking.axaml.
    /// </summary>
    public static IServiceCollection AddKitbashDocking(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The list type a restored layout rebuilds its collections as. Dock's own model
        // holds ObservableCollection, so a layout read back notifies the way a built one
        // does. Substitute IDockSerializer to write the layout some other way.
        services.TryAddSingleton<IDockSerializer>(_ => new DockSerializer(typeof(ObservableCollection<>)));
        services.TryAddSingleton<IDockLayoutStore, DockLayoutStore>();

        return services;
    }

    /// <summary>
    /// Where a grid's column layout is kept between runs. Takes Core's
    /// <c>AddKitbashApplicationStorage</c> and <c>AddKitbashIO</c>, since the file sits in
    /// application state. A grid keeps nothing on its own, so an app that does not register
    /// this has grids that start on the columns their tool declared, every time.
    /// </summary>
    public static IServiceCollection AddKitbashGridColumns(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IGridColumnStore, GridColumnStore>();

        return services;
    }
}
