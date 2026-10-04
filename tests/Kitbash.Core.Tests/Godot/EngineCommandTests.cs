using Microsoft.Extensions.DependencyInjection;
using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;

namespace Kitbash.Core.Tests.Godot;

/// <summary>
/// godot on PATH following the default engine, over a real home and real application state
/// in a temporary folder. Linux alone, since macOS would ask for an administrator and
/// Windows writes a shim.
/// </summary>
public sealed class EngineCommandTests : IDisposable
{
    public static bool OnLinux => OperatingSystem.IsLinux();

    private const string LinuxOnly = "The link and an unasked PATH are the Linux answer.";

    private static readonly EngineId Stable = EngineId.Parse("4.7.1-stable");
    private static readonly EngineId Mono = EngineId.Parse("4.7.1-stable-mono");

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "kitbash-command-" + Guid.NewGuid().ToString("n"));

    private readonly FakeSettings _settings = new();
    private readonly FakeStore _store = new();
    private readonly List<ServiceProvider> _providers = [];

    private string Home => Path.Combine(_root, "home");

    private string Bin => Path.Combine(Home, ".local", "bin");

    private string Link => Path.Combine(Bin, "godot");

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task OffByDefaultAndWritesNothing()
    {
        var state = await Command().SyncAsync(CancellationToken.None);

        Assert.Equal(EngineCommandKind.Off, state.Kind);
        Assert.False(Directory.Exists(Bin));
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task TurnedOnItLinksTheDefaultEngine()
    {
        var engine = Install(Stable);
        _settings.OnPath = true;
        _settings.Default = Stable;

        var state = await Command().SyncAsync(CancellationToken.None);

        Assert.Equal(EngineCommandKind.Placed, state.Kind);
        Assert.Equal(Link, state.Command);
        Assert.Equal(engine.Id, state.Engine!.Id);
        Assert.Equal(new CommandReach(PathReach.OnPath, null), state.Reach);
        Assert.Equal(engine.Executable, new FileInfo(Link).LinkTarget);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task ANewDefaultMovesTheLink()
    {
        Install(Stable);
        var mono = Install(Mono);
        _settings.OnPath = true;
        _settings.Default = Stable;
        var command = Command();
        await command.SyncAsync(CancellationToken.None);

        _settings.Default = Mono;
        var state = await command.SyncAsync(CancellationToken.None);

        Assert.Equal(EngineCommandKind.Placed, state.Kind);
        Assert.Equal(mono.Executable, new FileInfo(Link).LinkTarget);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task TurningItOffRemovesTheLink()
    {
        Install(Stable);
        _settings.OnPath = true;
        _settings.Default = Stable;
        var command = Command();
        await command.SyncAsync(CancellationToken.None);

        _settings.OnPath = false;
        var state = await command.SyncAsync(CancellationToken.None);

        Assert.Equal(EngineCommandKind.Off, state.Kind);
        Assert.False(File.Exists(Link) || new FileInfo(Link).LinkTarget is not null);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task NoDefaultTakesTheLinkBack()
    {
        Install(Stable);
        _settings.OnPath = true;
        _settings.Default = Stable;
        var command = Command();
        await command.SyncAsync(CancellationToken.None);

        _settings.Default = null;
        var state = await command.SyncAsync(CancellationToken.None);

        Assert.Equal(EngineCommandKind.NoDefault, state.Kind);
        Assert.Null(new FileInfo(Link).LinkTarget);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task ADefaultWhoseFolderHasGoneCountsAsNone()
    {
        _store.Engines.Add(Install(Stable) with { IsMissing = true });
        _store.Engines.RemoveAt(0);
        _settings.OnPath = true;
        _settings.Default = Stable;

        var state = await Command().SyncAsync(CancellationToken.None);

        Assert.Equal(EngineCommandKind.NoDefault, state.Kind);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task AFileSomebodyElsePutThereIsLeftAlone()
    {
        Install(Stable);
        Directory.CreateDirectory(Bin);
        File.WriteAllText(Link, "#!/bin/sh\necho mine\n");
        _settings.OnPath = true;
        _settings.Default = Stable;

        var state = await Command().SyncAsync(CancellationToken.None);

        Assert.Equal(EngineCommandKind.Conflict, state.Kind);
        Assert.Equal("#!/bin/sh\necho mine\n", File.ReadAllText(Link));
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task ALinkSomebodyElseMadeIsLeftAlone()
    {
        Install(Stable);
        var theirs = Path.Combine(_root, "theirs");
        File.WriteAllText(theirs, string.Empty);
        Directory.CreateDirectory(Bin);
        File.CreateSymbolicLink(Link, theirs);
        _settings.OnPath = true;
        _settings.Default = Stable;

        var command = Command();
        var state = await command.SyncAsync(CancellationToken.None);

        Assert.Equal(EngineCommandKind.Conflict, state.Kind);
        Assert.Equal(theirs, new FileInfo(Link).LinkTarget);

        _settings.OnPath = false;
        await command.SyncAsync(CancellationToken.None);

        Assert.Equal(theirs, new FileInfo(Link).LinkTarget);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task OurLinkPointedElsewhereByAPersonIsTheirsNow()
    {
        Install(Stable);
        _settings.OnPath = true;
        _settings.Default = Stable;
        var command = Command();
        await command.SyncAsync(CancellationToken.None);

        var theirs = Path.Combine(_root, "theirs");
        File.WriteAllText(theirs, string.Empty);
        File.Delete(Link);
        File.CreateSymbolicLink(Link, theirs);

        var state = await command.SyncAsync(CancellationToken.None);

        Assert.Equal(EngineCommandKind.Conflict, state.Kind);
        Assert.Equal(theirs, new FileInfo(Link).LinkTarget);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task AFolderThatCannotBeMadeIsReportedRatherThanThrown()
    {
        Install(Stable);
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(Home, ".local"), "a file where the folder goes");
        _settings.OnPath = true;
        _settings.Default = Stable;

        var state = await Command().SyncAsync(CancellationToken.None);

        Assert.Equal(EngineCommandKind.Failed, state.Kind);
        Assert.False(string.IsNullOrWhiteSpace(state.Failure));
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task NoHomeIsNowhereToPutIt()
    {
        Install(Stable);
        _settings.OnPath = true;
        _settings.Default = Stable;

        var state = await Command(home: string.Empty).SyncAsync(CancellationToken.None);

        Assert.Equal(EngineCommandKind.Unavailable, state.Kind);
        Assert.Null(state.Command);
    }

    private InstalledEngine Install(EngineId id)
    {
        var directory = Path.Combine(_root, "engines", id.DirectoryName);
        Directory.CreateDirectory(directory);
        var editor = Path.Combine(directory, "Godot_v4.7.1-stable_linux.x86_64");
        File.WriteAllText(editor, "#!/bin/sh\n");
        new FileSystem().MakeExecutableFile(editor);

        var record = new EngineRecord(
            id,
            EnginePlatform.Linux,
            EngineArchitecture.X64,
            "4.7.1.stable.official",
            Path.GetFileName(editor),
            string.Empty,
            string.Empty,
            DateTimeOffset.UnixEpoch);

        var engine = new InstalledEngine(record, directory, editor, 0, IsMissing: false, IsImported: true);
        _store.Engines.Add(engine);

        return engine;
    }

    private IEngineCommand Command(string? home = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEnvironment>(new FakeEnvironment(home ?? Home));
        services.AddSingleton<IUserDirectories>(new TestDirectories(_root));
        services.AddSingleton<IProcessRunner>(new FakeShell($"KITBASH_PATH=/usr/bin:{Bin}\n"));
        services.AddSingleton<IGodotSettings>(_settings);
        services.AddSingleton<IEngineStore>(_store);
        services.AddKitbashEngines();

        var provider = services.BuildServiceProvider();
        _providers.Add(provider);

        return provider.GetRequiredService<IEngineCommand>();
    }

    private sealed class FakeSettings : IGodotSettings
    {
        public bool OnPath { get; set; }

        public EngineId? Default { get; set; }

        public string EngineDirectory => Path.Combine(Path.GetTempPath(), "kitbash-no-engines");

        public EngineId? DefaultEngine => Default;

        public bool DefaultOnPath => OnPath;

        public GodotBuildTool BuildTool => GodotBuildTool.Auto;

        public void SetEngineDirectory(string value) => throw new NotSupportedException();

        public void SetDefaultEngine(EngineId? value) => throw new NotSupportedException();
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

    private sealed class FakeEnvironment(string home) : IEnvironment
    {
        public string? GetVariable(string name) => name == "SHELL" ? "/bin/sh" : null;

        public string GetHomeDirectory() => home;

        public IReadOnlyList<string> GetProcessCommand() => [];
    }

    private sealed class FakeShell(string output) : IProcessRunner
    {
        public void Run(ProcessRequest request) => throw new NotSupportedException();

        public Task<ProcessOutput> ReadAsync(ProcessRequest request, CancellationToken cancellation = default) =>
            Task.FromResult(new ProcessOutput(0, output, string.Empty));

        public Task<ProcessOutput> ReadLinesAsync(
            ProcessRequest request, Action<string> onLine, CancellationToken cancellation = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestDirectories(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state", application);

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "run");
    }
}
