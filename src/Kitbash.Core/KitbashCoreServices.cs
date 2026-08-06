using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Kitbash.Core.Git;
using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Platform.Linux;
using Kitbash.Core.Platform.Openers;
using Kitbash.Core.Platform.Windows;
using Kitbash.Core.Projects;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Core.Workspaces;

namespace Kitbash.Core;

/// <summary>
/// Service registration for Core. Each executable calls the pieces it needs and builds
/// its own provider. Static because the language requires a container for extension
/// methods.
/// </summary>
public static class KitbashCoreServices
{
    /// <summary>Filesystem and environment access. Everything else builds on these.</summary>
    public static IServiceCollection AddKitbashIO(this IServiceCollection services)
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

            // Setup.exe writes the shortcut and the uninstall entry, and there is no
            // bundle to correct for, so both of these have nothing to do here.
            services.TryAddSingleton<IBundleEnvironment, PlainEnvironment>();
            services.TryAddSingleton<IDesktopIntegration, WindowsDesktopIntegration>();

            // No portal here, so picking a colour off the screen would mean a window over a
            // capture of the desktop, which is not built.
            services.TryAddSingleton<IScreenColour, NoScreenColour>();

            return services;
        }

        if (OperatingSystem.IsLinux())
        {
            services.TryAddSingleton<IUserDirectories, LinuxUserDirectories>();
            services.TryAddSingleton<IPathShortener, LinuxPathShortener>();
            services.TryAddSingleton<IPathRules, LinuxPathRules>();
            services.TryAddSingleton<IBundleEnvironment, AppImageEnvironment>();
            services.TryAddSingleton<IDesktopIntegration, LinuxDesktopIntegration>();
#if KITBASH_LINUX_SECRETS
            services.TryAddSingleton<IScreenColour, LinuxScreenColour>();
#else
            services.TryAddSingleton<IScreenColour, NoScreenColour>();
#endif

            return services;
        }

        throw new PlatformNotSupportedException("Kitbash supports Windows and Linux on x64.");
    }

    public static IServiceCollection AddKitbashPlatform(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKitbashIO();
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();
        services.TryAddSingleton<IExecutableFinder, ExecutableFinder>();
        services.TryAddSingleton<IDesktopLauncherResolver, DesktopLauncherResolver>();
        services.TryAddSingleton(CreatePlatform);
        services.TryAddSingleton<ISingleInstance>(provider => new SingleInstance(
            ApplicationPaths.ApplicationName,
            provider.GetRequiredService<IUserDirectories>(),
            provider.GetRequiredService<IFileSystem>(),
            provider.GetRequiredService<IEnvironment>()));

        return services;
    }

    /// <summary>
    /// Where a credential is kept for this person on this machine. Its own registration,
    /// so a tool that wants a token does not also take the process runner and the
    /// launcher lookup.
    /// </summary>
    public static IServiceCollection AddKitbashSecrets(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKitbashIO();
        services.TryAddSingleton(CreateSecretStore);

        return services;
    }

    /// <summary>
    /// Where the programs Kitbash runs are on this machine. A person's override first
    /// and PATH behind it, so this is the only registration that makes the platform
    /// services depend on application settings, and only because where git is became a
    /// choice a person can make.
    /// </summary>
    public static IServiceCollection AddKitbashExternalTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKitbashPlatform();
        services.AddKitbashApplicationStorage();
        services.TryAddSingleton<ExternalToolsSettingsSchema>();
        services.TryAddSingleton<IExternalTools, ExternalTools>();

        return services;
    }

    /// <summary>
    /// Reading a repository, following one so it stays current, and bringing one up to
    /// date. Needs the platform services, since all of it comes from running git.
    /// </summary>
    public static IServiceCollection AddKitbashGit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKitbashExternalTools();
        services.TryAddTransient<IDirectoryWatcher, DirectoryWatcher>();
        services.TryAddSingleton<GitEnvironment>();
        services.TryAddSingleton<IGitRunner, GitRunner>();
        services.TryAddSingleton<IGitStatusReader, GitStatusReader>();
        services.TryAddSingleton<IGitUpdater, GitUpdater>();
        services.TryAddSingleton<IGitCloner, GitCloner>();
        services.TryAddSingleton<IGitStatusMonitor, GitStatusMonitor>();
        services.TryAddSingleton<GitPatchReader>();
        services.TryAddSingleton<GitPatchWriter>();
        services.TryAddSingleton<IGitFileStatusReader, GitFileStatusReader>();
        services.TryAddSingleton<IGitHistoryReader, GitHistoryReader>();
        services.TryAddSingleton<IGitDiffReader, GitDiffReader>();
        services.TryAddSingleton<IGitStager, GitStager>();
        services.TryAddSingleton<IGitCommitter, GitCommitter>();
        services.TryAddSingleton<IGitBranches, GitBranches>();
        services.TryAddSingleton<IGitRefReader, GitRefReader>();
        services.TryAddSingleton<IGitSync, GitSync>();
        services.TryAddSingleton<IGitMerger, GitMerger>();
        services.TryAddSingleton<IGitBlobReader, GitBlobReader>();
        services.TryAddSingleton<IGitConflictReader, GitConflictReader>();
        services.TryAddSingleton<IGitMergeDrivers, GitMergeDrivers>();

        return services;
    }

    /// <summary>
    /// The list of workspaces this person has added, and which one is open. Builds on
    /// application state, since the list follows the user rather than a workspace.
    /// </summary>
    /// <summary>
    /// Reading what Godot has published. Takes <see cref="ApplicationPaths"/>, so
    /// <see cref="AddKitbashApplicationStorage"/> goes in first.
    /// </summary>
    public static IServiceCollection AddKitbashEngines(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IWebContent, WebContent>();
        services.TryAddSingleton<IEngineCatalogue, EngineCatalogue>();
        services.TryAddSingleton<IEngineStore, EngineStore>();
        services.TryAddSingleton<IEngineInstaller, EngineInstaller>();
        services.AddKitbashExternalTools();
        services.TryAddSingleton<IGodotLauncher, GodotLauncher>();
        services.AddKitbashGodotProjects();
        services.AddEngineFiles();

        return services;
    }

    /// <summary>
    /// Reading a Godot project and working out which installed engine answers it. Needed
    /// by the engines page and by workspace naming, which both read the same file.
    /// </summary>
    public static IServiceCollection AddKitbashGodotProjects(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKitbashIO();
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

        throw new PlatformNotSupportedException("Kitbash supports Windows and Linux on x64.");
    }

    public static IServiceCollection AddKitbashWorkspaces(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKitbashApplicationStorage();
        services.AddKitbashGodotProjects();
        services.TryAddSingleton<IWorkspaceNameResolver, WorkspaceNameResolver>();
        services.TryAddSingleton<IWorkspaceScaffold, WorkspaceScaffold>();
        services.TryAddSingleton<IWorkspaceRegistry, WorkspaceRegistry>();
        services.TryAddSingleton<IWorkspaceSettingsFactory, WorkspaceSettingsFactory>();

        return services;
    }

    /// <summary>
    /// Making a workspace from nothing, which is more than listing one: it writes a Godot
    /// project and runs git, so it takes both of those as well.
    /// </summary>
    public static IServiceCollection AddKitbashWorkspaceCreation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKitbashWorkspaces();
        services.AddKitbashGit();
        services.AddEngineFiles();
        services.TryAddSingleton<IGitInitializer, GitInitializer>();
        services.TryAddSingleton<IGodotProjectWriter, GodotProjectWriter>();
        services.TryAddSingleton<IWorkspaceMaker, WorkspaceMaker>();

        return services;
    }

    /// <summary>Workspace discovery, for callers that do not yet know which workspace they are in.</summary>
    public static IServiceCollection AddKitbashWorkspace(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKitbashIO();
        services.TryAddSingleton<IWorkspaceLocator, WorkspaceLocator>();

        return services;
    }

    /// <summary>
    /// What this app has opened before, kept in its own state file. The store keeps the
    /// list and the app decides what belongs on it.
    /// </summary>
    /// <param name="scope">
    /// Which state file the list lands in. A tool passes its own scope, so two tools
    /// never share a list.
    /// </param>
    public static IServiceCollection AddKitbashRecentProjects(
        this IServiceCollection services,
        SettingsScope scope)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKitbashApplicationStorage();
        services.TryAddSingleton(new RecentProjectsOptions(scope));
        services.TryAddSingleton<IRecentProjects, RecentProjects>();

        return services;
    }

    /// <summary>
    /// What this user's Kitbash keeps on this machine. Settings, state and the cache
    /// directory. Available without a workspace, so all of it can be read at startup.
    /// </summary>
    public static IServiceCollection AddKitbashApplicationStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKitbashIO();
        services.TryAddSingleton<ApplicationPaths>();
        services.TryAddSingleton<ISettingsValueConverter, SettingsValueConverter>();
        services.TryAddSingleton<ISettingsDocumentStore, TomlSettingsDocumentStore>();
        services.TryAddSingleton<IApplicationSettings, ApplicationSettings>();
        services.TryAddSingleton<IApplicationState, ApplicationState>();
        services.TryAddSingleton<WindowSettingsSchema>();
        services.TryAddSingleton<IWindowSettings, WindowSettings>();

        // The schema alone, so a settings window can draw the page without the lookup
        // behind it. AddKitbashExternalTools is what puts IExternalTools over it.
        services.TryAddSingleton<ExternalToolsSettingsSchema>();
        services.TryAddSingleton<GodotSettingsSchema>();
        services.TryAddSingleton<IGodotSettings, GodotSettings>();
        services.TryAddSingleton<WorkspacesSettingsSchema>();
        services.TryAddSingleton<IWorkspacesSettings, WorkspacesSettings>();

        return services;
    }

    /// <summary>
    /// What a settings window is built over: the stores a page can stand on, the read
    /// that names its layer, and the write that saves a page at once.
    /// </summary>
    public static IServiceCollection AddKitbashSettingsSchema(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKitbashApplicationStorage();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISettingsHome, ApplicationSettingsHome>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISettingsHome, ApplicationStateHome>());
        services.TryAddSingleton<ISettingsInspector, SettingsInspector>();
        services.TryAddSingleton<ISettingsWriter, SettingsWriter>();

        return services;
    }

    /// <summary>
    /// The workspace half of a settings window for an app that holds a list of them,
    /// which is the launcher. Every workspace a person has added becomes a place, so the
    /// window lists them all rather than following whichever one is open. An app that
    /// opens one workspace and keeps it calls <see cref="AddKitbashSettings"/> instead.
    /// </summary>
    public static IServiceCollection AddKitbashKnownWorkspaceSettings(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKitbashWorkspaces();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISettingsHome, KnownWorkspacesSettingsHome>());

        return services;
    }

    /// <summary>Settings for one workspace. Find it first with <see cref="IWorkspaceLocator"/>.</summary>
    public static IServiceCollection AddKitbashSettings(this IServiceCollection services, WorkspacePaths paths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);

        services.AddKitbashIO();
        services.TryAddSingleton(paths);
        services.TryAddSingleton<ISettingsValueConverter, SettingsValueConverter>();
        services.TryAddSingleton<ISettingsDocumentStore, TomlSettingsDocumentStore>();
        services.TryAddSingleton<ISettingsService, WorkspaceSettingsService>();

        // Contributed whether or not a schema is in use, since an unresolved home costs
        // nothing and this is the only place that knows a workspace is known.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISettingsHome, WorkspaceSettingsHome>());

        return services;
    }

    /// <summary>
    /// Opening a workspace in a program a person already has. Over the external tools,
    /// since finding Git Bash on Windows means knowing where git is.
    /// </summary>
    public static IServiceCollection AddKitbashWorkspaceOpeners(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKitbashExternalTools();
        services.TryAddSingleton<JetBrainsToolbox>();
        services.TryAddSingleton<IWorkspaceFileFinder, WorkspaceFileFinder>();
        services.TryAddSingleton<IOpenerArguments, OpenerArguments>();
        services.TryAddSingleton<ICustomOpeners, CustomOpeners>();
        services.TryAddSingleton(CreateOpenerFinder);
        services.TryAddSingleton<IWorkspaceOpeners, WorkspaceOpeners>();

        return services;
    }

    /// <summary>
    /// A fifth place in this file that tests the running OS. Nothing about the registry
    /// needs the version test the secret store has, since its annotation carries no version.
    /// </summary>
    private static IWorkspaceOpenerFinder CreateOpenerFinder(IServiceProvider provider)
    {
        var fileSystem = provider.GetRequiredService<IFileSystem>();
        var environment = provider.GetRequiredService<IEnvironment>();
        var executables = provider.GetRequiredService<IExecutableFinder>();
        var toolbox = provider.GetRequiredService<JetBrainsToolbox>();

        if (OperatingSystem.IsWindows())
        {
            return new WindowsWorkspaceOpenerFinder(
                environment,
                fileSystem,
                executables,
                provider.GetRequiredService<IProcessRunner>(),
                provider.GetRequiredService<IExternalTools>(),
                toolbox);
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxWorkspaceOpenerFinder(executables, environment, fileSystem, toolbox);
        }

        throw new PlatformNotSupportedException("Kitbash supports Windows and Linux on x64.");
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

        throw new PlatformNotSupportedException("Kitbash supports Windows and Linux on x64.");
    }

    /// <summary>
    /// The fourth place in this file that tests the running OS. The constants come from
    /// the project file, which drops the store a runtime is not being built for.
    /// </summary>
    private static ISecretStore CreateSecretStore(IServiceProvider provider)
    {
#if KITBASH_WINDOWS_SECRETS
        // The version is the credential package's own floor, which CA1416 makes this test
        // rather than the plain Windows one every other factory here uses.
        if (OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
        {
            return new WindowsSecretStore();
        }
#endif

#if KITBASH_LINUX_SECRETS
        if (OperatingSystem.IsLinux())
        {
            return new LinuxSecretStore(provider.GetRequiredService<IEnvironment>());
        }
#endif

        throw new PlatformNotSupportedException("Kitbash supports Windows and Linux on x64.");
    }
}
