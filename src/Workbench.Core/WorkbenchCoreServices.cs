using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Workbench.Core.Git;
using Workbench.Core.Godot;
using Workbench.Core.IO;
using Workbench.Core.Platform;
using Workbench.Core.Platform.Linux;
using Workbench.Core.Platform.Windows;
using Workbench.Core.Settings;
using Workbench.Core.Settings.Schema;
using Workbench.Core.Workspaces;

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
        services.AddPlatformIO();

        return services;
    }

    /// <summary>
    /// The IO services that differ per OS. One test, so a new one of these never adds
    /// another place where the running OS is asked about.
    /// </summary>
    private static IServiceCollection AddPlatformIO(this IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            services.TryAddSingleton<IUserDirectories, WindowsUserDirectories>();
            services.TryAddSingleton<IPathShortener, WindowsPathShortener>();
            services.TryAddSingleton<IPathRules, WindowsPathRules>();

            return services;
        }

        if (OperatingSystem.IsLinux())
        {
            services.TryAddSingleton<IUserDirectories, LinuxUserDirectories>();
            services.TryAddSingleton<IPathShortener, LinuxPathShortener>();
            services.TryAddSingleton<IPathRules, LinuxPathRules>();

            return services;
        }

        throw new PlatformNotSupportedException("Workbench supports Windows and Linux on x64.");
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

    /// <summary>
    /// Where the programs Workbench runs are on this machine. A person's override first
    /// and PATH behind it, so this is the only registration that makes the platform
    /// services depend on application settings, and only because where git is became a
    /// choice a person can make.
    /// </summary>
    public static IServiceCollection AddWorkbenchExternalTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkbenchPlatform();
        services.AddWorkbenchApplicationStorage();
        services.TryAddSingleton<ExternalToolsSettingsSchema>();
        services.TryAddSingleton<IExternalTools, ExternalTools>();

        return services;
    }

    /// <summary>
    /// Reading a repository, following one so it stays current, and bringing one up to
    /// date. Needs the platform services, since all of it comes from running git.
    /// </summary>
    public static IServiceCollection AddWorkbenchGit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkbenchExternalTools();
        services.TryAddTransient<IDirectoryWatcher, DirectoryWatcher>();
        services.TryAddSingleton<IGitStatusReader, GitStatusReader>();
        services.TryAddSingleton<IGitUpdater, GitUpdater>();
        services.TryAddSingleton<IGitStatusMonitor, GitStatusMonitor>();

        return services;
    }

    /// <summary>
    /// The list of workspaces this person has added, and which one is open. Builds on
    /// application state, since the list follows the user rather than a workspace.
    /// </summary>
    /// <summary>
    /// Reading what Godot has published. Takes <see cref="ApplicationPaths"/>, so
    /// <see cref="AddWorkbenchApplicationStorage"/> goes in first.
    /// </summary>
    public static IServiceCollection AddWorkbenchEngines(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IWebContent, WebContent>();
        services.TryAddSingleton<IEngineCatalogue, EngineCatalogue>();
        services.TryAddSingleton<IEngineStore, EngineStore>();
        services.TryAddSingleton<IEngineInstaller, EngineInstaller>();
        services.AddWorkbenchExternalTools();
        services.TryAddSingleton<IGodotLauncher, GodotLauncher>();
        services.AddWorkbenchGodotProjects();
        services.AddEngineFiles();

        return services;
    }

    /// <summary>
    /// Reading a Godot project and working out which installed engine answers it. Needed
    /// by the engines page and by workspace naming, which both read the same file.
    /// </summary>
    public static IServiceCollection AddWorkbenchGodotProjects(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkbenchIO();
        services.TryAddSingleton<ISettingsValueConverter, SettingsValueConverter>();
        services.TryAddSingleton<ISettingsDocumentStore, TomlSettingsDocumentStore>();
        services.TryAddSingleton<WorkspaceGodotSettingsSchema>();
        services.TryAddSingleton<IGodotProjectReader, GodotProjectReader>();
        services.TryAddSingleton<IGodotImports, GodotImports>();
        services.TryAddSingleton<IEngineRequirementReader, EngineRequirementReader>();
        services.TryAddSingleton<IEngineResolver, EngineResolver>();

        return services;
    }

    /// <summary>
    /// The one part of engine handling that differs per OS. A third place in this file that
    /// tests the running OS, and the file is still the only one allowed to.
    /// </summary>
    private static IServiceCollection AddEngineFiles(this IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            services.TryAddSingleton<IEngineFiles, WindowsEngineFiles>();

            return services;
        }

        if (OperatingSystem.IsLinux())
        {
            services.TryAddSingleton<IEngineFiles, UnixEngineFiles>();

            return services;
        }

        throw new PlatformNotSupportedException("Workbench supports Windows and Linux on x64.");
    }

    public static IServiceCollection AddWorkbenchWorkspaces(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkbenchApplicationStorage();
        services.AddWorkbenchGodotProjects();
        services.TryAddSingleton<IWorkspaceNameResolver, WorkspaceNameResolver>();
        services.TryAddSingleton<IWorkspaceScaffold, WorkspaceScaffold>();
        services.TryAddSingleton<IWorkspaceRegistry, WorkspaceRegistry>();

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
    /// What this user's Workbench keeps on this machine. Settings, state and the cache
    /// directory. Available without a workspace, so all of it can be read at startup.
    /// </summary>
    public static IServiceCollection AddWorkbenchApplicationStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkbenchIO();
        services.TryAddSingleton<ApplicationPaths>();
        services.TryAddSingleton<ISettingsValueConverter, SettingsValueConverter>();
        services.TryAddSingleton<ISettingsDocumentStore, TomlSettingsDocumentStore>();
        services.TryAddSingleton<IApplicationSettings, ApplicationSettings>();
        services.TryAddSingleton<IApplicationState, ApplicationState>();
        services.TryAddSingleton<WindowSettingsSchema>();
        services.TryAddSingleton<IWindowSettings, WindowSettings>();
        services.TryAddSingleton<GodotSettingsSchema>();
        services.TryAddSingleton<IGodotSettings, GodotSettings>();

        return services;
    }

    /// <summary>
    /// What a settings window is built over: the stores a page can stand on, the read
    /// that names its layer, and the write that saves a page at once.
    /// </summary>
    public static IServiceCollection AddWorkbenchSettingsSchema(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkbenchApplicationStorage();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISettingsHome, ApplicationSettingsHome>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISettingsHome, ApplicationStateHome>());
        services.TryAddSingleton<ISettingsInspector, SettingsInspector>();
        services.TryAddSingleton<ISettingsWriter, SettingsWriter>();

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

        // Contributed whether or not a schema is in use, since an unresolved home costs
        // nothing and this is the only place that knows a workspace is known.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISettingsHome, WorkspaceSettingsHome>());

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
                provider.GetRequiredService<IDesktopLauncherResolver>(),
                provider.GetRequiredService<IExecutableFinder>());
        }

        throw new PlatformNotSupportedException("Workbench supports Windows and Linux on x64.");
    }
}
