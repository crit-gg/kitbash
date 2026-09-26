using Kitbash.Core.Godot;

namespace Kitbash.Core.Tests.Godot;

/// <summary>
/// An engine repository on GitHub releases, read, pinned, installed, replaced and given
/// templates, over the shape the API answers with.
/// </summary>
public sealed class EngineRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset First = new(2026, 9, 25, 22, 18, 40, TimeSpan.Zero);
    private static readonly EngineRepositoryAddress Slopworks =
        EngineRepositoryAddress.ForGitHub(Kitbash.Core.Platform.WebAddress.Parse("https://github.com/crit-gg/godot-slopworks"))!;

    private readonly EngineRepositoryHost _host = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task ReleasesAreListedNewestFirstWithDraftsLeftOutAndOddTagsCounted()
    {
        _host.Publish("4.7.2-slopworks-18d5d19", First);
        _host.Publish("4.7.2-slopworks-abc1234", First.AddDays(1), prerelease: true);
        _host.Publish("4.7.2-slopworks-dddd000", First.AddDays(2), draft: true);
        _host.PublishUnreadable("nightly");

        var read = await Repository().ReadReleasesAsync(refresh: false, Token);

        Assert.Equal(["4.7.2-slopworks-abc1234", "4.7.2-slopworks-18d5d19"], read.Releases.Select(r => r.Name));
        Assert.True(read.Releases[0].IsPrerelease);
        Assert.Equal("abc1234", read.Releases[0].Build);
        Assert.Equal(EngineChannel.Custom, read.Releases[0].Channel);
        Assert.Equal(1, read.Skipped);
    }

    [Fact]
    public async Task AManifestHoldsTheBuildsTheirDigestsAndTheTemplates()
    {
        _host.Publish("4.7.2-slopworks-18d5d19", First);

        var manifest = await Repository().ReadManifestAsync("4.7.2-slopworks-18d5d19", Token);

        Assert.Equal(EngineChecksumKind.Sha256, manifest.ChecksumKind);
        Assert.Equal(5, manifest.Builds.Count);
        Assert.All(manifest.Builds, build => Assert.Equal(Slopworks, build.Id.Repository));
        Assert.All(manifest.Builds, build => Assert.Equal(64, manifest.ChecksumFor(build)!.Length));
        Assert.Equal("Godot_v4.7.2-slopworks-18d5d19_mono_export_templates.tpz", manifest.TemplatesFor(mono: true)!.FileName);
        Assert.Null(manifest.TemplatesFor(mono: false));
    }

    [Fact]
    public async Task AListIsAskedForOnceInTenMinutesUnlessARefreshAsks()
    {
        _host.Publish("4.7.2-slopworks-18d5d19", First);

        await Repository().ReadReleasesAsync(refresh: false, Token);
        await Repository().ReadReleasesAsync(refresh: false, Token);

        Assert.Single(_host.Web.Asked, EngineRepositoryHost.Api);

        await Repository().ReadReleasesAsync(refresh: true, Token);

        Assert.Equal(2, _host.Web.Asked.Count(asked => asked == EngineRepositoryHost.Api));
    }

    [Fact]
    public async Task OfflineTheLastListAnswersAndSaysItIsStale()
    {
        _host.Publish("4.7.2-slopworks-18d5d19", First);
        await Repository().ReadReleasesAsync(refresh: false, Token);

        _host.Web.IsOffline = true;

        var read = await Repository().ReadReleasesAsync(refresh: true, Token);

        Assert.True(read.IsStale);
        Assert.Single(read.Releases);
    }

    [Fact]
    public void TheWorkspaceNamesItsRepositoryAndANewestPin()
    {
        _host.ListGlobally();
        _host.Project("[godot]\nrepository = \"slopworks\"\nengine = \"4.7.2\"\n");

        var requirement = Requirement();

        Assert.Equal(Slopworks, requirement.Repository!.Address);
        Assert.False(requirement.Version!.Value.IsExact);
        Assert.True(requirement.NeedsDotnet);
        Assert.Equal("4.7.2-mono", requirement.Slot);
    }

    [Fact]
    public void WithNoPinTheProjectsOwnVersionIsFollowed()
    {
        _host.ListGlobally();
        _host.Project("[godot]\nrepository = \"slopworks\"\n");

        var requirement = Requirement();

        Assert.Equal(EngineRequirementSource.Project, requirement.Source);
        Assert.Equal("4.7-mono", requirement.Slot);
    }

    [Fact]
    public void AnExactPinOwnsNoSlot()
    {
        _host.ListGlobally();
        _host.Project("[godot]\nrepository = \"slopworks\"\nengine = \"4.7.2+18d5d19\"\n");

        var requirement = Requirement();

        Assert.True(requirement.Version!.Value.IsExact);
        Assert.Null(requirement.Slot);
    }

    [Fact]
    public void TheWorkspacesOwnEntryWinsOverAGlobalOneOfTheSameName()
    {
        _host.ListGlobally(url: "https://github.com/someone/else");
        _host.Project(
            "[godot]\nrepository = \"slopworks\"\nengine = \"4.7.2\"\n\n"
            + "[[godot.repositories]]\nname = \"slopworks\"\ntype = \"github\"\n"
            + "url = \"https://github.com/crit-gg/godot-slopworks\"\n");

        Assert.Equal(Slopworks, Requirement().Repository!.Address);
    }

    [Fact]
    public void ANameNoListHoldsIsSaidRatherThanGuessed()
    {
        _host.Project("[godot]\nrepository = \"slopworks\"\nengine = \"4.7.2\"\n");

        var requirement = Requirement();

        Assert.True(requirement.IsRepositoryUnknown);
        Assert.Null(requirement.Slot);
    }

    [Fact]
    public async Task ANewestPinInstallsIntoItsSlotAndMatches()
    {
        var requirement = Pinned("4.7.2");

        _host.Publish("4.7.2-slopworks-18d5d19", First);

        var engine = await InstallAsync(requirement);

        Assert.Equal("4.7.2-mono", engine.Record.Slot);
        Assert.Equal("18d5d19", engine.Id.Build);
        Assert.Equal(EngineChecksumKind.Sha256, engine.Record.ChecksumKind);
        Assert.EndsWith(Path.Combine("github", "crit-gg", "godot-slopworks", "newest", "4.7.2-mono"), engine.Directory);

        var installed = await _host.Get<IEngineStore>().ReadAsync(Token);
        var resolved = _host.Get<IEngineResolver>().Resolve(requirement, installed, theDefault: null);

        Assert.Equal(EngineMatch.Matched, resolved.Match);
        Assert.Equal(engine.Directory, resolved.Engine!.Directory);
    }

    [Fact]
    public async Task ANewerBuildReplacesTheSlotAndLeavesOneInstall()
    {
        var requirement = Pinned("4.7.2");

        _host.Publish("4.7.2-slopworks-18d5d19", First);
        await InstallAsync(requirement);

        _host.Publish("4.7.2-slopworks-abc1234", First.AddDays(1));

        var update = await _host.Get<IEngineUpdater>().UpdateAsync(requirement, refresh: true, progress: null, Token);
        var installed = await _host.Get<IEngineStore>().ReadAsync(Token);

        Assert.Equal(EngineUpdateOutcome.Updated, update.Outcome);
        Assert.Equal("abc1234", Assert.Single(installed).Id.Build);
    }

    [Fact]
    public async Task APrereleaseIsNotANewerBuild()
    {
        var requirement = Pinned("4.7.2");

        _host.Publish("4.7.2-slopworks-18d5d19", First);
        await InstallAsync(requirement);

        _host.Publish("4.7.2-slopworks-abc1234", First.AddDays(1), prerelease: true);

        var update = await _host.Get<IEngineUpdater>().UpdateAsync(requirement, refresh: true, progress: null, Token);

        Assert.Equal(EngineUpdateOutcome.UpToDate, update.Outcome);
    }

    [Fact]
    public async Task ARunningEngineWaitsAndIsSwappedOnceItCloses()
    {
        var requirement = Pinned("4.7.2");

        _host.Publish("4.7.2-slopworks-18d5d19", First);
        await InstallAsync(requirement);

        _host.Publish("4.7.2-slopworks-abc1234", First.AddDays(1));
        _host.Running.Everything = true;

        var waiting = await _host.Get<IEngineUpdater>().UpdateAsync(requirement, refresh: true, progress: null, Token);

        Assert.Equal(EngineUpdateOutcome.Waiting, waiting.Outcome);
        Assert.Equal("18d5d19", Assert.Single(await _host.Get<IEngineStore>().ReadAsync(Token)).Id.Build);

        // Closed, and the network gone as well, so the build already waiting is what moves.
        _host.Running.Everything = false;
        _host.Web.IsOffline = true;

        var swapped = await _host.Get<IEngineUpdater>().UpdateAsync(requirement, refresh: true, progress: null, Token);

        Assert.Equal(EngineUpdateOutcome.Updated, swapped.Outcome);
        Assert.Equal("abc1234", Assert.Single(await _host.Get<IEngineStore>().ReadAsync(Token)).Id.Build);
    }

    [Fact]
    public async Task AnExactPinGetsItsOwnFolderAndIsNeverMoved()
    {
        var exact = Pinned("4.7.2+18d5d19");

        _host.Publish("4.7.2-slopworks-18d5d19", First);
        _host.Publish("4.7.2-slopworks-abc1234", First.AddDays(1));

        var engine = await InstallAsync(exact);

        Assert.Equal("18d5d19", engine.Id.Build);
        Assert.False(engine.IsSlot);
        Assert.EndsWith(Path.Combine("godot-slopworks", "4.7.2-slopworks-18d5d19-mono"), engine.Directory);

        var update = await _host.Get<IEngineUpdater>().UpdateAsync(exact, refresh: true, progress: null, Token);

        Assert.Equal(EngineUpdateOutcome.NotFollowing, update.Outcome);
    }

    [Fact]
    public async Task ABuildThatDoesNotMatchItsDigestIsRefused()
    {
        var requirement = Pinned("4.7.2");

        _host.Publish("4.7.2-slopworks-18d5d19", First, badDigest: true);

        await Assert.ThrowsAsync<EngineInstallException>(() => InstallAsync(requirement));
        Assert.Empty(await _host.Get<IEngineStore>().ReadAsync(Token));
    }

    [Fact]
    public async Task OfficialPinsNeverMatchARepositoryBuild()
    {
        _host.Publish("4.7.2-slopworks-18d5d19", First);
        await InstallAsync(Pinned("4.7.2"));

        _host.Project("[godot]\nengine = \"4.7\"\n");

        var installed = await _host.Get<IEngineStore>().ReadAsync(Token);
        var resolved = _host.Get<IEngineResolver>().Resolve(Requirement(), installed, theDefault: null);

        Assert.Equal(EngineMatch.Missing, resolved.Match);
    }

    [Fact]
    public async Task TemplatesGoWhereGodotReadsThemAndFollowTheSlot()
    {
        var requirement = Pinned("4.7.2");

        _host.Publish("4.7.2-slopworks-18d5d19", First);

        var engine = await InstallAsync(requirement);
        var templates = await _host.Get<IEngineTemplates>().InstallAsync(engine, progress: null, Token);
        var folder = Path.Combine(_host.ExportTemplates, "4.7.2.slopworks.mono");

        Assert.Equal("4.7.2.slopworks.mono", templates.Record.Templates);
        Assert.True(File.Exists(Path.Combine(folder, "version.txt")));
        Assert.True(File.Exists(Path.Combine(folder, "linux_debug.x86_64")));

        _host.Publish("4.7.2-slopworks-abc1234", First.AddDays(1));

        var update = await _host.Get<IEngineUpdater>().UpdateAsync(requirement, refresh: true, progress: null, Token);

        Assert.Equal(EngineUpdateOutcome.Updated, update.Outcome);
        Assert.Null(update.TemplatesFailure);
        Assert.Equal("4.7.2.slopworks.mono", update.Engine!.Record.Templates);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public async Task TemplatesAnotherInstallReadsAreNotOwnedByEither()
    {
        _host.Publish("4.7.2-slopworks-18d5d19", First);

        var slot = await InstallAsync(Pinned("4.7.2"));
        var exact = await InstallAsync(Pinned("4.7.2+18d5d19"));

        slot = await _host.Get<IEngineTemplates>().InstallAsync(slot, progress: null, Token);

        var templates = _host.Get<IEngineTemplates>();

        Assert.Null(templates.OwnedBy(slot, [slot, exact]));
        Assert.Equal("4.7.2.slopworks.mono", templates.OwnedBy(slot, [slot]));
    }

    private IEngineRepository Repository() => _host.Get<IEngineRepositories>().For(Slopworks);

    private EngineRequirement Requirement() => _host.Get<IEngineRequirementReader>().Read(_host.Workspace);

    private EngineRequirement Pinned(string pin)
    {
        _host.ListGlobally();
        _host.Project($"[godot]\nrepository = \"slopworks\"\nengine = \"{pin}\"\n");

        return Requirement();
    }

    private async Task<InstalledEngine> InstallAsync(EngineRequirement requirement)
    {
        var updater = _host.Get<IEngineUpdater>();
        var build = await updater.FindAsync(requirement, refresh: true, Token);

        Assert.NotNull(build);

        return await updater.InstallAsync(requirement, build, progress: null, Token);
    }
}
