using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Workspaces;

namespace Kitbash.Core.Tests.Workspaces;

/// <summary>
/// Making a workspace, over real folders in a temporary root. Git is off in every case
/// here, so nothing runs a process.
/// </summary>
public class WorkspaceMakerTests : IDisposable
{
    private readonly string _home =
        Path.Combine(Path.GetTempPath(), "kitbash-maker-" + Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            Directory.Delete(_home, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    [Fact]
    public async Task AMonoEngineIsPinnedAsTheDotnetBuild()
    {
        var provider = Services();
        var root = await MakeAsync(provider, "mono", new EngineId(EngineTag.Parse("4.7.1-stable"), IsMono: true));

        Assert.Equal("4.7.1-stable-mono", Pin(root));

        // The pin is what the strip resolves against, so it has to ask for a .NET engine.
        var requirement = provider.GetRequiredService<IEngineRequirementReader>().Read(root);

        Assert.True(requirement.NeedsDotnet);
        Assert.Equal(EngineRequirementSource.Workspace, requirement.Source);
    }

    [Fact]
    public async Task APlainEngineIsPinnedWithoutTheSuffix()
    {
        var provider = Services();
        var root = await MakeAsync(provider, "plain", new EngineId(EngineTag.Parse("4.7.1-stable"), IsMono: false));

        Assert.Equal("4.7.1-stable", Pin(root));

        // The project has no C# in it, so a plain pin asks for a plain engine.
        Assert.False(provider.GetRequiredService<IEngineRequirementReader>().Read(root).NeedsDotnet);
    }

    // config/features carries a version and a renderer, and Godot writes no runtime there.
    [Fact]
    public async Task TheMonoSuffixNeverReachesTheProjectFile()
    {
        var provider = Services();
        var root = await MakeAsync(provider, "features", new EngineId(EngineTag.Parse("4.7.1-stable"), IsMono: true));

        var project = await File.ReadAllTextAsync(Path.Combine(root, "project.godot"), TestContext.Current.CancellationToken);

        Assert.Contains("config/features=PackedStringArray(\"4.7\", \"Forward Plus\")", project, StringComparison.Ordinal);
        Assert.DoesNotContain("mono", project, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> MakeAsync(IServiceProvider provider, string name, EngineId engine)
    {
        var workspace = await provider.GetRequiredService<IWorkspaceMaker>().MakeAsync(new NewWorkspace
        {
            Name = name,
            Path = Path.Combine(_home, "workspaces", name),
            CreatesFolder = true,
            HasGodotProject = true,
            Engine = engine,
            Renderer = GodotRenderer.ForwardPlus,
            UsesGit = false,
        });

        return workspace.Root;
    }

    /// <summary>
    /// What godot.engine says in the team layer, which is where the pin is written. Read as
    /// text, since the document store is Core's own and stays internal.
    /// </summary>
    private static string Pin(string root)
    {
        var file = new WorkspacePaths(root).FileFor(SettingsScope.Global, SettingsLayer.TeamShared);
        var found = Regex.Match(
            File.ReadAllText(file),
            @"^\s*engine\s*=\s*""([^""]*)""",
            RegexOptions.Multiline);

        return found.Success ? found.Groups[1].Value : string.Empty;
    }

    /// <summary>
    /// A provider built the way the launcher builds one, since Core keeps its
    /// implementations internal. Every user directory lands under one temporary root.
    /// </summary>
    private ServiceProvider Services()
    {
        var services = new ServiceCollection();

        // First, so the TryAdd inside AddKitbashIO leaves it alone.
        services.TryAddSingleton<IUserDirectories>(new TestDirectories(_home));

        return services
            .AddKitbashWorkspaceCreation()
            .AddKitbashGodotProjects()
            .BuildServiceProvider();
    }

    private sealed class TestDirectories(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state");

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "run");
    }
}
