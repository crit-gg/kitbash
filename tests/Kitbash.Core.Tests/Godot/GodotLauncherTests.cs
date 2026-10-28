using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kitbash.Core.Tests.Godot;

/// <summary>
/// Opening a project with its C# built by the editor, over real files and scripted
/// processes. Every case is a rebuild, since that mode stops before anything is started.
/// </summary>
public sealed class GodotLauncherTests : IDisposable
{
    private const string CompilerError =
        "A.cs(4,89): error CS0103: The name 'Missing' does not exist in the current context";

    private const string CompilerWarning =
        "A.cs(4,79): warning CS8602: Dereference of a possibly null reference.";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "kitbash-launcher-" + Guid.NewGuid().ToString("n"));
    private readonly ScriptedProcesses _processes = new();
    private readonly ServiceProvider _provider;

    public GodotLauncherTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "config"));
        Directory.CreateDirectory(Project.Directory);
        Build("editor");

        var services = new ServiceCollection();
        services.TryAddSingleton<IUserDirectories>(new TestDirectories(_root));
        services.TryAddSingleton<IProcessRunner>(_processes);

        _provider = services.AddKitbashEngines().BuildServiceProvider();
    }

    private GodotProject Project => new()
    {
        File = Path.Combine(_root, "game", "project.godot"),
        Directory = Path.Combine(_root, "game"),
        UsesDotnet = true,
    };

    private IGodotLauncher Launcher => _provider.GetRequiredService<IGodotLauncher>();

    public void Dispose()
    {
        _provider.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task AFailedEditorBuildCarriesTheLogGodotKept()
    {
        var engine = Engine();
        var logs = Path.Combine(_root, "state", "godot", "mono", "build_logs");

        _processes.OnBuild = () => Log(logs, "a1_Debug", Project.Directory);

        var failure = await Assert.ThrowsAsync<GodotLaunchException>(() => Open(engine));

        Assert.Equal(GodotLaunchStage.Building, failure.Stage);
        Assert.Contains(CompilerError, failure.Output);
        Assert.Contains("An EditorPlugin build callback failed.", failure.Output);
    }

    [Fact]
    public async Task ALogNamingAnotherProjectIsLeftAlone()
    {
        var engine = Engine();
        var logs = Path.Combine(_root, "state", "godot", "mono", "build_logs");

        // A sibling whose name starts the same, so a match on a prefix would take it.
        _processes.OnBuild = () => Log(logs, "b2_Debug", Project.Directory + "2");

        var failure = await Assert.ThrowsAsync<GodotLaunchException>(() => Open(engine));

        Assert.DoesNotContain(CompilerError, failure.Output);
    }

    [Fact]
    public async Task ALogFromAnEarlierBuildIsLeftAlone()
    {
        var engine = Engine();
        var file = Log(Path.Combine(_root, "state", "godot", "mono", "build_logs"), "a1_Debug", Project.Directory);

        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-5));

        var failure = await Assert.ThrowsAsync<GodotLaunchException>(() => Open(engine));

        Assert.DoesNotContain(CompilerError, failure.Output);
    }

    [Fact]
    public async Task ASelfContainedEditorKeepsItsLogBesideItself()
    {
        var engine = Engine();
        var beside = Path.GetDirectoryName(engine.Executable)!;

        File.WriteAllText(Path.Combine(beside, "._sc_"), string.Empty);

        _processes.OnBuild = () => Log(Path.Combine(beside, "editor_data", "mono", "build_logs"), "a1_Debug", Project.Directory);

        var failure = await Assert.ThrowsAsync<GodotLaunchException>(() => Open(engine));

        Assert.Contains(CompilerError, failure.Output);
    }

    [Fact]
    public async Task SkippingTheBuildRunsNoBuild()
    {
        var engine = Engine();

        await Open(engine, skipBuild: true);

        Assert.DoesNotContain(_processes.Asked, request => request.Arguments.Contains("--build-solutions"));
        Assert.Contains(_processes.Asked, request => request.Arguments.Contains("--import"));
    }

    [Fact]
    public void AProjectWithCSharpBuilds()
    {
        Assert.True(Launcher.Builds(Engine(), Project));
    }

    [Fact]
    public void NothingBuildsWithTheBuildTurnedOff()
    {
        Build("off");

        Assert.False(Launcher.Builds(Engine(), Project));
    }

    private Task Open(InstalledEngine engine, bool skipBuild = false) =>
        Launcher.OpenAsync(
            engine, Project, GodotLaunchMode.Rebuild, skipBuild, new Progress<GodotLaunchStep>(), CancellationToken.None);

    private void Build(string tool) =>
        File.WriteAllText(Path.Combine(_root, "config", "kitbash.toml"), $"[godot]\nbuild = \"{tool}\"\n");

    /// <summary>A log in the shape Godot's build logger writes, naming the project file.</summary>
    private static string Log(string logs, string folder, string projectDirectory)
    {
        var directory = Path.Combine(logs, folder);
        var file = Path.Combine(directory, "msbuild_log.txt");
        var csproj = Path.Combine(projectDirectory, "Game.csproj");

        Directory.CreateDirectory(directory);
        File.WriteAllText(
            file,
            $"Project \"{csproj}\" on node 1 (Build target(s)).\n"
            + $"\t{CompilerError} [{csproj}]\n"
            + $"\t{CompilerWarning} [{csproj}]\n"
            + $"Done building project \"{csproj}\" -- FAILED.\n");

        return file;
    }

    private InstalledEngine Engine()
    {
        var directory = Path.Combine(_root, "engines", "4.7.1-stable-mono");
        var editor = Path.Combine(directory, "Godot_v4.7.1-stable_mono_linux.x86_64");

        Directory.CreateDirectory(directory);
        File.WriteAllText(editor, "#!/bin/sh\n");

        var record = new EngineRecord(
            EngineId.Parse("4.7.1-stable-mono"),
            EnginePlatform.Linux,
            EngineArchitecture.X64,
            "4.7.1.stable.mono.official",
            Path.GetFileName(editor),
            string.Empty,
            string.Empty,
            DateTimeOffset.UnixEpoch);

        return new InstalledEngine(record, directory, editor, 0, IsMissing: false, IsImported: true);
    }

    /// <summary>The editor's build fails the way Godot's does, and everything else succeeds.</summary>
    private sealed class ScriptedProcesses : IProcessRunner
    {
        public List<ProcessRequest> Asked { get; } = [];

        /// <summary>What the editor writes while it builds.</summary>
        public Action? OnBuild { get; set; }

        public void Run(ProcessRequest request) => throw new NotSupportedException();

        public Task<ProcessOutput> ReadAsync(ProcessRequest request, CancellationToken cancellation = default)
        {
            Asked.Add(request);

            if (!request.Arguments.Contains("--build-solutions"))
            {
                return Task.FromResult(new ProcessOutput(0, string.Empty, string.Empty));
            }

            OnBuild?.Invoke();

            return Task.FromResult(new ProcessOutput(
                1,
                string.Empty,
                "ERROR: An EditorPlugin build callback failed.\n   at: call_build (editor/editor_node.cpp:7678)\n"));
        }

        public Task<ProcessOutput> ReadLinesAsync(
            ProcessRequest request, Action<string> onLine, CancellationToken cancellation = default)
        {
            Asked.Add(request);

            return Task.FromResult(new ProcessOutput(0, string.Empty, string.Empty));
        }
    }

    private sealed class TestDirectories(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state", application);

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "run");
    }
}
