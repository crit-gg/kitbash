using Kitbash.Core.Godot;
using Kitbash.Core.Settings;
using Kitbash.Core.Workspaces;
using Kitbash.ViewModels;

namespace Kitbash.Tests;

/// <summary>
/// The engine list in the new workspace dialog. A row is a build, so what is picked is
/// what the workspace is pinned to, .NET flag and all.
/// </summary>
public sealed class NewWorkspaceEngineTests
{
    private static readonly EngineTag Tag = EngineTag.Parse("4.7.1-stable");

    [Fact]
    public async Task AVersionInstalledBothWaysIsTwoRows()
    {
        var model = await ModelAsync(Installed(mono: false), Installed(mono: true));

        Assert.Equal(
            [new EngineId(Tag, IsMono: false), new EngineId(Tag, IsMono: true)],
            model.Engines.Select(row => row.Id));

        Assert.Equal([false, true], model.Engines.Select(row => row.IsMono));
        Assert.All(model.Engines, row => Assert.True(row.IsInstalled));
    }

    // The reported bug. Only the .NET build is here, so the row is it and the pin says so.
    [Fact]
    public async Task PickingTheDotnetBuildPinsTheDotnetBuild()
    {
        var model = await ModelAsync(Installed(mono: true));

        model.Engine = Assert.Single(model.Engines);

        Assert.Equal("4.7.1-stable-mono", model.Request.Engine?.ToString());
    }

    [Fact]
    public async Task PickingThePlainBuildPinsNoRuntime()
    {
        var model = await ModelAsync(Installed(mono: false), Installed(mono: true));

        model.Engine = model.Engines.First(row => !row.IsMono);

        Assert.Equal("4.7.1-stable", model.Request.Engine?.ToString());
    }

    /// <summary>
    /// A release nothing has installed is offered as a plain build, since the dialog reads
    /// no manifest and cannot say whether a .NET build was published for this machine.
    /// </summary>
    [Fact]
    public async Task AReleaseThatIsNotInstalledIsOfferedAsAPlainBuild()
    {
        var model = await ModelAsync([], new EngineRelease(Tag, new DateOnly(2026, 7, 21), null));

        var row = Assert.Single(model.Engines);

        Assert.False(row.IsMono);
        Assert.False(row.IsInstalled);
    }

    /// <summary>The default names one build, so it opens on that one and not on its twin.</summary>
    [Fact]
    public async Task TheListOpensOnTheMachineDefaultBuild()
    {
        var model = await ModelAsync(
            new EngineId(Tag, IsMono: true),
            [Installed(mono: false), Installed(mono: true)],
            []);

        Assert.True(model.Engine?.IsMono);
    }

    // No project means no engine is pinned at all, whatever the list is showing.
    [Fact]
    public async Task AnEmptyWorkspacePinsNothing()
    {
        var model = await ModelAsync(Installed(mono: true));

        model.Engine = Assert.Single(model.Engines);
        model.HasGodotProject = false;

        Assert.Null(model.Request.Engine);
    }

    private static Task<NewWorkspaceViewModel> ModelAsync(params InstalledEngine[] installed) =>
        ModelAsync(null, installed, []);

    private static Task<NewWorkspaceViewModel> ModelAsync(
        InstalledEngine[] installed,
        params EngineRelease[] releases) =>
        ModelAsync(null, installed, releases);

    private static async Task<NewWorkspaceViewModel> ModelAsync(
        EngineId? theDefault,
        IReadOnlyList<InstalledEngine> installed,
        IReadOnlyList<EngineRelease> releases)
    {
        var model = new NewWorkspaceViewModel(
            new FakeMaker(),
            new FakeStore(installed),
            new FakeCatalogue(releases),
            new FakeGodotSettings(theDefault),
            Path.GetTempPath());

        await model.LoadEnginesAsync();

        return model;
    }

    private static InstalledEngine Installed(bool mono) => new(
        new EngineRecord(
            new EngineId(Tag, mono),
            EnginePlatform.Linux,
            EngineArchitecture.X64,
            "4.7.1.stable.official",
            "godot",
            "godot.zip",
            "checksum",
            DateTimeOffset.UnixEpoch),
        Directory: "/engines/" + new EngineId(Tag, mono),
        Executable: "/engines/godot",
        SizeOnDisk: 1,
        IsMissing: false,
        IsImported: false);

    private sealed class FakeStore(IReadOnlyList<InstalledEngine> installed) : IEngineStore
    {
        public Task<IReadOnlyList<InstalledEngine>> ReadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(installed);

        public Task<InstalledEngine?> ImportAsync(string directory, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RemoveAsync(InstalledEngine engine, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<InstalledEngine> RegisterAsync(
            string directory,
            EngineBuild build,
            string checksum,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeCatalogue(IReadOnlyList<EngineRelease> releases) : IEngineCatalogue
    {
        public Task<EngineReleases> ReadReleasesAsync(bool refresh, CancellationToken cancellationToken) =>
            Task.FromResult(new EngineReleases(releases, DateTimeOffset.UnixEpoch, IsStale: false));

        public Task<EngineManifest> ReadManifestAsync(EngineTag tag, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeGodotSettings(EngineId? theDefault) : IGodotSettings
    {
        public string EngineDirectory => Path.GetTempPath();

        public EngineId? DefaultEngine => theDefault;

        public GodotBuildTool BuildTool => GodotBuildTool.Auto;

        public void SetEngineDirectory(string value) => throw new NotSupportedException();

        public void SetDefaultEngine(EngineId? value) => throw new NotSupportedException();
    }

    /// <summary>Answers every request the same way, since nothing here is about the path.</summary>
    private sealed class FakeMaker : IWorkspaceMaker
    {
        public NewWorkspaceState Check(NewWorkspace request) => NewWorkspaceState.WillCreate;

        public string Resolve(string path) => path;

        public string FolderNameFor(string name) => name;

        public Task<Workspace> MakeAsync(NewWorkspace request, CancellationToken cancellation = default) =>
            throw new NotSupportedException();
    }
}
