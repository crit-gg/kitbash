using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Workbench.Core.IO;
using Workbench.Core.Platform;
using Workbench.Core.Platform.Linux;
using Workbench.Core.Platform.Windows;
using Workbench.Core.Settings;

namespace Workbench.Core;

/// <summary>
/// Service registration for Core. Each executable calls the pieces it needs and builds
/// its own provider. Static because the language requires a container for extension
/// methods.
/// </summary>
public static class WorkbenchCoreServices
{
    /// <summary>Filesystem and environment access. Everything else builds on these.</summary>
    public static IServiceCollection AddWorkbenchIO(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IFileSystem, FileSystem>();
        services.TryAddSingleton<IEnvironment, SystemEnvironment>();

        return services;
    }

    public static IServiceCollection AddWorkbenchPlatform(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkbenchIO();
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();
        services.TryAddSingleton<IExecutableFinder, ExecutableFinder>();
        services.TryAddSingleton<IDesktopLauncherResolver, DesktopLauncherResolver>();
        services.TryAddSingleton(CreatePlatform);

        return services;
    }

    /// <summary>Workspace discovery, for callers that do not yet know which workspace they are in.</summary>
    public static IServiceCollection AddWorkbenchWorkspace(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkbenchIO();
        services.TryAddSingleton<IWorkspaceLocator, WorkspaceLocator>();

        return services;
    }

    /// <summary>
    /// Settings for this user on this machine. Available without a workspace, so it
    /// can be read at startup.
    /// </summary>
    public static IServiceCollection AddWorkbenchApplicationSettings(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkbenchIO();
        services.TryAddSingleton<ApplicationPaths>();
        services.TryAddSingleton<ISettingsValueConverter, SettingsValueConverter>();
        services.TryAddSingleton<ISettingsDocumentStore, TomlSettingsDocumentStore>();
        services.TryAddSingleton<IApplicationSettings, ApplicationSettings>();

        return services;
    }

    /// <summary>Settings for one workspace. Find it first with <see cref="IWorkspaceLocator"/>.</summary>
    public static IServiceCollection AddWorkbenchSettings(this IServiceCollection services, WorkspacePaths paths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);

        services.AddWorkbenchIO();
        services.TryAddSingleton(paths);
        services.TryAddSingleton<ISettingsValueConverter, SettingsValueConverter>();
        services.TryAddSingleton<ISettingsDocumentStore, TomlSettingsDocumentStore>();
        services.TryAddSingleton<ISettingsService, WorkspaceSettingsService>();

        return services;
    }

    private static IPlatformServices CreatePlatform(IServiceProvider provider)
    {
        var fileSystem = provider.GetRequiredService<IFileSystem>();
        var processes = provider.GetRequiredService<IProcessRunner>();

        if (OperatingSystem.IsWindows())
        {
            return new WindowsPlatform(fileSystem, processes);
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxPlatform(
                fileSystem,
                processes,
                provider.GetRequiredService<IDesktopLauncherResolver>());
        }

        throw new PlatformNotSupportedException("Workbench supports Windows and Linux on x64.");
    }
}
