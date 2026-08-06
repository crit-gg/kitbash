using Kitbash.Core;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Tests;

/// <summary>
/// What a form is remembered with, over a real state file in a temporary folder, so it is
/// written and read back the way it will be on a person's machine.
/// </summary>
public sealed class ToolInputMemoryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"kitbash-memory-{Guid.NewGuid():N}");

    private readonly ServiceProvider _services;
    private readonly IToolInputMemory _memory;
    private readonly ToolId _id;

    public ToolInputMemoryTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IUserDirectories>(new FakeUserDirectories(_root))
            .AddKitbashApplicationStorage()
            .AddSingleton<ToolLog>()
            .AddSingleton<IToolInputMemory, ToolInputMemory>()
            .BuildServiceProvider();

        _memory = _services.GetRequiredService<IToolInputMemory>();

        Assert.True(ToolId.TryParse("sprites", out _id));
    }

    public void Dispose()
    {
        _services.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Only an input the manifest marks is kept, and a fresh read finds it.</summary>
    [Fact]
    public void OnlyARememberedInputSurvivesTheRun()
    {
        IReadOnlyList<ToolInput> inputs = [Input("source", remember: true), Input("target", remember: false)];

        _memory.Write(
            _id,
            inputs,
            new Dictionary<string, string> { ["source"] = "/sheets", ["target"] = "/out" });

        var read = Reread().Read(_id, inputs);

        Assert.Equal("/sheets", read["source"]);
        Assert.DoesNotContain("target", read.Keys);
    }

    /// <summary>Clearing a field forgets it rather than remembering nothing under its name.</summary>
    [Fact]
    public void AnEmptyAnswerTakesTheKeyOutAgain()
    {
        IReadOnlyList<ToolInput> inputs = [Input("source", remember: true)];

        _memory.Write(_id, inputs, new Dictionary<string, string> { ["source"] = "/sheets" });
        _memory.Write(_id, inputs, new Dictionary<string, string> { ["source"] = "  " });

        Assert.Empty(Reread().Read(_id, inputs));
    }

    /// <summary>It goes in the tool's own state file, which is where the version it runs is.</summary>
    [Fact]
    public void ItIsKeptInTheToolsOwnState()
    {
        _memory.Write(
            _id,
            [Input("source", remember: true)],
            new Dictionary<string, string> { ["source"] = "/sheets" });

        var file = Path.Combine(_root, "state", "tools", "sprites", "state.toml");

        Assert.True(File.Exists(file));

        var text = File.ReadAllText(file);

        Assert.Contains("[inputs]", text, StringComparison.Ordinal);
        Assert.Contains("source = \"/sheets\"", text, StringComparison.Ordinal);
    }

    /// <summary>A form nothing remembers writes nothing at all.</summary>
    [Fact]
    public void AFormWithNothingWorthKeepingWritesNoFile()
    {
        _memory.Write(
            _id,
            [Input("target", remember: false)],
            new Dictionary<string, string> { ["target"] = "/out" });

        Assert.False(Directory.Exists(Path.Combine(_root, "state", "tools", "sprites")));
    }

    /// <summary>A second reader over the same folder, so nothing answers out of memory.</summary>
    private IToolInputMemory Reread()
    {
        var services = new ServiceCollection()
            .AddSingleton<IUserDirectories>(new FakeUserDirectories(_root))
            .AddKitbashApplicationStorage()
            .AddSingleton<ToolLog>()
            .AddSingleton<IToolInputMemory, ToolInputMemory>()
            .BuildServiceProvider();

        return services.GetRequiredService<IToolInputMemory>();
    }

    private static ToolInput Input(string key, bool remember) =>
        new(
            key,
            key,
            string.Empty,
            ToolInputKind.Text,
            Argument: null,
            Required: false,
            remember,
            Default: string.Empty,
            Choices: [],
            Minimum: null,
            Maximum: null);

    private sealed class FakeUserDirectories(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state");

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "runtime");
    }
}
