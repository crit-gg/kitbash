using Microsoft.Extensions.DependencyInjection;
using Kitbash.Core.Godot;
using Kitbash.Core.Platform;

namespace Kitbash.Core.Tests.Godot;

/// <summary>Where a project.godot opened from the desktop goes, over real project files.</summary>
public sealed class ProjectFileRouterTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "kitbash-route-" + Guid.NewGuid().ToString("n"));

    private readonly FakeStore _store = new();

    private ServiceProvider? _provider;

    public void Dispose()
    {
        _provider?.Dispose();

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    [Fact]
    public async Task AWorkspacesOwnProjectGoesToThatWorkspace()
    {
        var workspace = Path.Combine(_root, "workspace");
        var file = Project(Path.Combine(workspace, "game"), "4.7", dotnet: false);

        var route = await RouteAsync(file, workspace);

        Assert.Equal(ProjectFileRouteKind.Workspace, route.Kind);
        Assert.Equal(workspace, route.WorkspaceRoot);
    }

    [Fact]
    public async Task AnotherProjectInsideAWorkspaceIsAProjectOfItsOwn()
    {
        var workspace = Path.Combine(_root, "workspace");
        Project(workspace, "4.7", dotnet: false);
        var inner = Project(Path.Combine(workspace, "samples", "demo"), "4.7", dotnet: false);
        _store.Engines.Add(Engine("4.7.1-stable"));

        var route = await RouteAsync(inner, workspace);

        Assert.Equal(ProjectFileRouteKind.Engine, route.Kind);
    }

    [Fact]
    public async Task AProjectNeedingCSharpOpensOnlyInADotnetEngine()
    {
        var file = Project(Path.Combine(_root, "game"), "4.7", dotnet: true);
        _store.Engines.Add(Engine("4.7.1-stable"));

        Assert.Equal(ProjectFileRouteKind.NotInstalled, (await RouteAsync(file)).Kind);

        _store.Engines.Add(Engine("4.7.1-stable-mono"));

        var route = await RouteAsync(file);

        Assert.Equal(ProjectFileRouteKind.Engine, route.Kind);
        Assert.True(route.Engine!.IsMono);
    }

    [Fact]
    public async Task TheDefaultEngineNeverStandsInForAnotherVersion()
    {
        var file = Project(Path.Combine(_root, "game"), "4.7", dotnet: false);
        _store.Engines.Add(Engine("4.6.2-stable"));

        var route = await RouteAsync(file);

        Assert.Equal(ProjectFileRouteKind.NotInstalled, route.Kind);
        Assert.Equal("4.7", route.Requirement!.Version.ToString());
    }

    [Fact]
    public async Task AFileThatHasGoneSaysSo()
    {
        var file = Path.Combine(_root, "nothing", "project.godot");

        Assert.Equal(ProjectFileRouteKind.Gone, (await RouteAsync(file)).Kind);
    }

    [Fact]
    public async Task AGodot3ProjectIsNotMatched()
    {
        var folder = Path.Combine(_root, "old");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "project.godot");
        File.WriteAllText(file, "config_version=4\n\n[application]\n\nconfig/name=\"Old\"\n");

        Assert.Equal(ProjectFileRouteKind.Unsupported, (await RouteAsync(file)).Kind);
    }

    private Task<ProjectFileRoute> RouteAsync(string file, params string[] workspaces)
    {
        Assert.True(ProjectFileArgument.TryParse(file, out var argument));

        return Router().RouteAsync(argument, workspaces, CancellationToken.None);
    }

    private IProjectFileRouter Router()
    {
        if (_provider is null)
        {
            var services = new ServiceCollection();

            // TryAdd means the first registration wins.
            services.AddSingleton<IEngineStore>(_store);
            services.AddKitbashIO();
            services.AddKitbashEngines();

            _provider = services.BuildServiceProvider();
        }

        return _provider.GetRequiredService<IProjectFileRouter>();
    }

    private static string Project(string folder, string version, bool dotnet)
    {
        Directory.CreateDirectory(folder);

        var features = dotnet ? $"\"{version}\", \"C#\", \"Forward Plus\"" : $"\"{version}\", \"Forward Plus\"";
        var file = Path.Combine(folder, "project.godot");

        File.WriteAllText(
            file,
            $"config_version=5\n\n[application]\n\nconfig/name=\"Probe\"\nconfig/features=PackedStringArray({features})\n");

        return file;
    }

    private InstalledEngine Engine(string id)
    {
        var directory = Path.Combine(_root, "engines", id);
        var editor = Path.Combine(directory, "godot");

        Directory.CreateDirectory(directory);
        File.WriteAllText(editor, "#!/bin/sh\n");

        var record = new EngineRecord(
            EngineId.Parse(id),
            EnginePlatform.Linux,
            EngineArchitecture.X64,
            id,
            Path.GetFileName(editor),
            string.Empty,
            string.Empty,
            DateTimeOffset.UnixEpoch);

        return new InstalledEngine(record, directory, editor, 0, IsMissing: false, IsImported: true);
    }

    private sealed class FakeStore : IEngineStore
    {
        public List<InstalledEngine> Engines { get; } = [];

        public Task<IReadOnlyList<InstalledEngine>> ReadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<InstalledEngine>>([.. Engines]);

        public InstalledEngine? ReadAt(string directory) => null;

        public Task<InstalledEngine?> ImportAsync(string directory, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RemoveAsync(InstalledEngine engine, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<InstalledEngine> RegisterAsync(
            string directory,
            EngineBuild build,
            string checksum,
            EngineChecksumKind checksumKind,
            string slot,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public InstalledEngine RecordTemplates(InstalledEngine engine, string folder) =>
            throw new NotSupportedException();
    }
}
