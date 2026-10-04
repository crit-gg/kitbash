using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Kitbash.Core;
using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;
using Kitbash.Settings;
using Kitbash.Tools;
using Kitbash.Ui.Toasts;
using Kitbash.ViewModels;
using Kitbash.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Tests;

/// <summary>
/// The engines page with an engine repository on it, drawn for real. A slot and an
/// official install on the Installed tab, and a repository's releases among the official
/// ones on the Available tab.
/// </summary>
public sealed class EnginesPageRepositoryTests
{
    private static readonly EngineRepositoryAddress Slopworks =
        EngineRepositoryAddress.ForGitHub(WebAddress.Parse("https://github.com/crit-gg/godot-slopworks"))!;

    [AvaloniaFact]
    public async Task AnInstalledSlotNamesItsRepositoryAndThePinItFollows()
    {
        var page = Page();

        await page.LoadAsync();

        var slot = Assert.Single(page.Rows, row => row.Engine.IsSlot);

        Assert.Equal("4.7.2 18d5d19", slot.Title);
        Assert.Equal("SLOPWORKS", slot.Channel);
        Assert.Contains("follows 4.7.2-mono", slot.Parts);
        Assert.Contains("templates", slot.Parts);
        Assert.False(slot.CanSetDefault);
        Assert.False(slot.CanAddTemplates);
        Assert.True(slot.HasTemplates);

        var official = Assert.Single(page.Rows, row => row.Engine.Id.IsOfficial);

        Assert.True(official.CanAddTemplates);

        Render(page, "engines-installed-repository.png");
    }

    [AvaloniaFact]
    public async Task RepositoryReleasesAreListedAmongTheOfficialOnes()
    {
        var page = Page();

        await page.LoadAsync();

        page.ShowsAll = true;
        page.OnAvailable = true;

        Assert.Equal(["4.7.2 abc1234", "4.7.2 18d5d19", "4.7.1 stable"], page.Cards.Select(card => card.Title));
        Assert.True(page.Cards[0].IsPrerelease);
        Assert.Equal("SLOPWORKS", page.Cards[1].Channel);
        Assert.Equal("1 build installed", page.Cards[1].InstalledNote);
        Assert.Equal("1 release from slopworks has a tag that could not be read.", page.RepositoryNote);

        page.ShowsCustom = true;

        Assert.Equal(2, page.Cards.Count);
        Assert.False(page.ShowsStable);

        page.Cards[1].IsOpen = true;

        Render(page, "engines-available-repository.png");
    }

    private static void Render(EnginesViewModel model, string name)
    {
        var view = new EnginesPage { DataContext = model };
        var window = new LauncherWindow { Content = view, Width = 1100, Height = 720 };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var frame = window.CaptureRenderedFrame();

            Assert.NotNull(frame);
            Keep(frame, name);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Keep(WriteableBitmap frame, string name)
    {
        var directory = Environment.GetEnvironmentVariable("KITBASH_RENDER_OUT");

        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);

        using var file = File.Create(Path.Combine(directory, name));

        frame.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    private static EnginesViewModel Page()
    {
        var io = new ServiceCollection().AddKitbashIO().BuildServiceProvider();

        var older = Release("4.7.2-slopworks-18d5d19", "18d5d19", new DateTimeOffset(2026, 9, 25, 22, 0, 0, TimeSpan.Zero));
        var newer = Release("4.7.2-slopworks-abc1234", "abc1234", new DateTimeOffset(2026, 9, 26, 9, 0, 0, TimeSpan.Zero)) with
        {
            IsPrerelease = true,
        };

        var official = new EngineRelease(EngineTag.Parse("4.7.1-stable"), new DateOnly(2026, 7, 21), null)
        {
            Name = "4.7.1-stable",
            PublishedAt = new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero),
        };

        var slot = new InstalledEngine(
            new EngineRecord(
                older.IdFor(mono: true),
                EnginePlatform.Linux,
                EngineArchitecture.X64,
                "4.7.2.slopworks.mono.custom_build.18d5d19",
                "godot",
                "Godot_v4.7.2-slopworks-18d5d19_mono_linux_x86_64.zip",
                "ab",
                DateTimeOffset.UnixEpoch)
            {
                Slot = "4.7.2-mono",
                Templates = "4.7.2.slopworks.mono",
                ChecksumKind = EngineChecksumKind.Sha256,
                Release = older.Name,
            },
            "/engines/github/crit-gg/godot-slopworks/newest/4.7.2-mono",
            "/engines/github/crit-gg/godot-slopworks/newest/4.7.2-mono/godot",
            150L * 1024 * 1024,
            IsMissing: false,
            IsImported: false);

        var plain = new InstalledEngine(
            new EngineRecord(
                new EngineId(official.Tag, IsMono: false),
                EnginePlatform.Linux,
                EngineArchitecture.X64,
                "4.7.1.stable.official.a13da4feb",
                "godot",
                "Godot_v4.7.1-stable_linux.x86_64.zip",
                "cd",
                DateTimeOffset.UnixEpoch),
            "/engines/4.7.1-stable",
            "/engines/4.7.1-stable/godot",
            76L * 1024 * 1024,
            IsMissing: false,
            IsImported: false);

        return new EnginesViewModel(
            new FakeCatalogue(official),
            new FakeRepositories(new FakeRepository([newer, older])),
            new FakeList(),
            new FakeUpdater(),
            new FakeTemplates(),
            new FakeStore([slot, plain]),
            new FakeGodotSettings(),
            io.GetRequiredService<IPathShortener>(),
            new FakePlatform(),
            io.GetRequiredService<IFileSystem>(),
            new FakeInstaller(),
            new FakeEngineFiles(),
            new FakeToasts(),
            new FakeAfterLaunch(),
            new FakeActions());
    }

    private static EngineRelease Release(string name, string build, DateTimeOffset at) =>
        new(EngineTag.Parse("4.7.2-custom"), DateOnly.FromDateTime(at.UtcDateTime), null)
        {
            Repository = Slopworks,
            Name = name,
            Build = build,
            PublishedAt = at,
        };

    private static EngineManifest ManifestFor(EngineRelease release)
    {
        var assets = new Dictionary<string, WebAddress>(StringComparer.Ordinal)
        {
            [$"Godot_v{release.Name}_mono_linux_x86_64.zip"] = WebAddress.Parse("https://example.com/a.zip"),
            [$"Godot_v{release.Name}_mono_win64.zip"] = WebAddress.Parse("https://example.com/b.zip"),
        };

        return new EngineManifest(
            release.Tag,
            EngineBuild.ReadAll(release, assets),
            new Dictionary<string, string>(),
            EngineBuild.PublishedTargets(release.FileTag, assets.Keys));
    }

    private sealed class FakeCatalogue(EngineRelease official) : IEngineCatalogue
    {
        public Task<EngineReleases> ReadReleasesAsync(bool refresh, CancellationToken cancellationToken) =>
            Task.FromResult(new EngineReleases([official], DateTimeOffset.UtcNow, IsStale: false));

        public Task<EngineManifest> ReadManifestAsync(EngineTag tag, CancellationToken cancellationToken)
        {
            var names = new[] { $"Godot_v{tag}_linux.x86_64.zip" };

            return Task.FromResult(new EngineManifest(
                tag, EngineBuild.ReadAll(tag, names), new Dictionary<string, string>(), EngineBuild.PublishedTargets(tag, names)));
        }
    }

    private sealed class FakeRepository(IReadOnlyList<EngineRelease> releases) : IEngineRepository
    {
        public EngineRepositoryAddress? Address => Slopworks;

        public Task<EngineReleases> ReadReleasesAsync(bool refresh, CancellationToken cancellationToken) =>
            Task.FromResult(new EngineReleases(releases, DateTimeOffset.UtcNow, IsStale: false) { Skipped = 1 });

        public Task<EngineManifest> ReadManifestAsync(string release, CancellationToken cancellationToken) =>
            Task.FromResult(ManifestFor(releases.First(candidate => candidate.Name == release)));
    }

    private sealed class FakeRepositories(IEngineRepository repository) : IEngineRepositories
    {
        public IEngineRepository For(EngineRepositoryAddress? address) => repository;
    }

    private sealed class FakeList : IEngineRepositoryList
    {
        private static readonly EngineRepositorySource Source = new(
            "slopworks", Slopworks, WebAddress.Parse("https://github.com/crit-gg/godot-slopworks"), "the global config");

        public IReadOnlyList<EngineRepositorySource> ReadGlobal() => [Source];

        public IReadOnlyList<EngineRepositorySource> ReadFor(string workspaceRoot) => [Source];

        public void WriteGlobal(IReadOnlyList<EngineRepositorySource> repositories) => throw new NotSupportedException();
    }

    private sealed class FakeUpdater : IEngineUpdater
    {
        public Task<EngineBuild?> FindAsync(EngineRequirement requirement, bool refresh, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<InstalledEngine> InstallAsync(
            EngineRequirement requirement,
            EngineBuild build,
            IProgress<EngineInstallProgress>? progress,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EngineUpdate> UpdateAsync(
            EngineRequirement requirement,
            bool refresh,
            IProgress<EngineInstallProgress>? progress,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeTemplates : IEngineTemplates
    {
        public Task<InstalledEngine> InstallAsync(
            InstalledEngine engine,
            IProgress<EngineInstallProgress>? progress,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public string? OwnedBy(InstalledEngine engine, IReadOnlyList<InstalledEngine> installed) => null;

        public long SizeOf(string folder) => 0;

        public void Remove(string folder) => throw new NotSupportedException();
    }

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
            EngineChecksumKind checksumKind,
            string slot,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public InstalledEngine? ReadAt(string directory) => null;

        public InstalledEngine RecordTemplates(InstalledEngine engine, string folder) =>
            throw new NotSupportedException();
    }

    private sealed class FakeGodotSettings : IGodotSettings
    {
        public string EngineDirectory => "/engines";

        public EngineId? DefaultEngine => null;

        public bool DefaultOnPath => false;

        public GodotBuildTool BuildTool => GodotBuildTool.Auto;

        public void SetEngineDirectory(string value) => throw new NotSupportedException();

        public void SetDefaultEngine(EngineId? value) => throw new NotSupportedException();
    }

    private sealed class FakePlatform : IPlatformServices
    {
        public PlatformKind Kind => PlatformKind.Linux;

        public void OpenInBrowser(WebAddress address) => throw new NotSupportedException();

        public void OpenInFileBrowser(DirectoryLocation location) => throw new NotSupportedException();

        public void StartDetached(ProcessRequest request) => throw new NotSupportedException();
    }

    private sealed class FakeInstaller : IEngineInstaller
    {
        public Task<InstalledEngine> InstallAsync(
            EngineBuild build, IProgress<EngineInstallProgress>? progress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<InstalledEngine> StageAsync(
            EngineBuild build, string slot, IProgress<EngineInstallProgress>? progress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public InstalledEngine? Staged(EngineRepositoryAddress address, string slot) => null;

        public InstalledEngine? Place(EngineRepositoryAddress address, string slot) => null;
    }

    private sealed class FakeEngineFiles : IEngineFiles
    {
        public EnginePlatform Platform => EnginePlatform.Linux;

        public EngineArchitecture Architecture => EngineArchitecture.X64;

        public string ExportTemplatesDirectory => "/templates";

        public string? FindEditor(string directory) => null;

        public string CommandFor(string editor) => editor;

        public void Hide(string path)
        {
        }
    }

    private sealed class FakeToasts : IToastService
    {
        public Toast Show(ToastRequest request) => throw new NotSupportedException();

        public void Post(ToastRequest request)
        {
        }

        public IToastRegion Region(ToastAnchor anchor) => throw new NotSupportedException();

        public void DismissAll()
        {
        }
    }

    private sealed class FakeAfterLaunch : IAfterLaunchSettings
    {
        public AfterLaunchAction AfterProjectManager => AfterLaunchAction.DoNothing;

        public AfterLaunchAction AfterEditor => AfterLaunchAction.DoNothing;

        public AfterLaunchAction AfterPlay => AfterLaunchAction.DoNothing;

        public AfterLaunchAction AfterExternalTool => AfterLaunchAction.DoNothing;

        public AfterLaunchAction AfterTool => AfterLaunchAction.DoNothing;

        public AfterLaunchAction ForTool(ToolId id) => AfterLaunchAction.DoNothing;
    }

    private sealed class FakeActions : IAfterLaunchActions
    {
        public void Apply(AfterLaunchAction action)
        {
        }
    }
}
