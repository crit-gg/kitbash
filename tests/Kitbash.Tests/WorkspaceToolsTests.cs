using Kitbash.Core;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;
using Kitbash.Core.Workspaces;
using Kitbash.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Tests;

/// <summary>
/// Real workspaces on disk, each with its own repository list, read the way the launcher
/// reads them. What is checked is that opening a workspace changes which tools belong here.
/// </summary>
public sealed class WorkspaceToolsTests : IDisposable
{
    private const string Foundry = "https://github.com/owner/foundry";
    private const string Splice = "https://github.com/owner/splice";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"kitbash-workspace-tools-{Guid.NewGuid():N}");

    private readonly ServiceProvider _services;

    public WorkspaceToolsTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IUserDirectories>(new FakeUserDirectories(_root))
            .AddKitbashIO()
            .AddKitbashPlatform()
            .AddKitbashApplicationStorage()
            .AddKitbashWorkspaces()
            .AddSingleton<ToolLog>()
            .AddSingleton<IToolRepositoryList, ToolRepositoryList>()
            .AddSingleton<IProvidedTools, ProvidedTools>()
            .BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// A tool each of two workspaces provides, installed once. Opening one workspace shows
    /// its tool and hides the other's, which is the whole of what a person sees.
    /// </summary>
    [Fact]
    public void OpeningAWorkspaceChangesWhichToolsAreHere()
    {
        Workspace("Art", Foundry);
        Workspace("Docs", Splice);

        var installed = new[] { Installed("foundry", Foundry), Installed("splice", Splice) };
        var tools = _services.GetRequiredService<IProvidedTools>();

        Open("Art");

        var art = Assert.Single(tools.Here(installed));

        Assert.Equal("github.foundry", art.Tool.Id.Value);
        Assert.Equal(["Art"], art.Workspaces);

        Open("Docs");

        var docs = Assert.Single(tools.Here(installed));

        Assert.Equal("github.splice", docs.Tool.Id.Value);
        Assert.Equal(["Docs"], docs.Workspaces);
    }

    /// <summary>
    /// Two workspaces listing the same repository both provide the tool, and the mark on it
    /// names both wherever a person is standing.
    /// </summary>
    [Fact]
    public void AToolTwoWorkspacesShareIsInBoth()
    {
        Workspace("Art", Foundry);
        Workspace("Docs", Foundry);
        Workspace("Empty");

        var installed = new[] { Installed("foundry", Foundry) };
        var tools = _services.GetRequiredService<IProvidedTools>();

        foreach (var workspace in new[] { "Art", "Docs" })
        {
            Open(workspace);

            Assert.Equal(["Art", "Docs"], Assert.Single(tools.Here(installed)).Workspaces);
        }

        Open("Empty");

        Assert.Empty(tools.Here(installed));
    }

    /// <summary>The global list provides a tool in every workspace and in none at all.</summary>
    [Fact]
    public void AGlobalRepositoryProvidesItsToolInEveryWorkspace()
    {
        Workspace("Art", Foundry);
        Workspace("Docs");

        _services.GetRequiredService<IToolRepositoryList>().WriteGlobal([
            new ToolRepositorySource(ToolRepositorySource.GitHub, WebAddress.Parse(Splice), "the global config"),
        ]);

        var installed = new[] { Installed("splice", Splice) };
        var tools = _services.GetRequiredService<IProvidedTools>();

        foreach (var workspace in new[] { "Art", "Docs" })
        {
            Open(workspace);

            Assert.Empty(Assert.Single(tools.Here(installed)).Workspaces);
        }
    }

    /// <summary>
    /// The repository has left every list, so what the install recorded is all that is left
    /// to go on. One the global list installed stays and one a workspace installed goes.
    /// </summary>
    [Fact]
    public void ARepositoryNoListStillNamesLeavesTheInstallToDecide()
    {
        Workspace("Art");
        Open("Art");

        var tools = _services.GetRequiredService<IProvidedTools>();

        Assert.Empty(tools.Here([Installed("foundry", Foundry)]));
        Assert.Single(tools.Here([Installed("foundry", Foundry, global: true)]));
    }

    /// <summary>
    /// A tool installed before the origin was recorded is on the page everywhere, since
    /// nothing says which workspace it came from. Recording it, which the page does from
    /// whichever repository answers for it, is what puts it back where it belongs.
    /// </summary>
    [Fact]
    public void AnInstallThatRecordedNothingIsEverywhereUntilItIsAdopted()
    {
        Workspace("Art", Foundry);
        Workspace("Docs");

        var tools = _services.GetRequiredService<IProvidedTools>();
        var unknown = Installed("foundry", Foundry) with { Origin = null };

        foreach (var workspace in new[] { "Art", "Docs" })
        {
            Open(workspace);

            Assert.Single(tools.Here([unknown]));
        }

        var adopted = Installed("foundry", Foundry);

        Open("Art");
        Assert.Single(tools.Here([adopted]));

        Open("Docs");
        Assert.Empty(tools.Here([adopted]));
    }

    /// <summary>A folder with a workspace config listing the repositories given.</summary>
    private string Workspace(string name, params string[] repositories)
    {
        var root = Path.Combine(_root, "workspaces", name);
        var paths = new WorkspacePaths(root);
        var file = paths.FileFor(SettingsScope.Global, SettingsLayer.TeamShared);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        var lines = repositories.Select(url =>
            $"[[tools.repositories]]{Environment.NewLine}"
            + $"type = \"github\"{Environment.NewLine}"
            + $"url = \"{url}\"{Environment.NewLine}");

        // Written before the folder is registered, since registering scaffolds a config
        // into a workspace that has none and this one is meant to have its own.
        File.WriteAllText(file, string.Join(Environment.NewLine, lines));

        _services.GetRequiredService<IWorkspaceRegistry>().Add(root);

        return root;
    }

    private void Open(string name)
    {
        var registry = _services.GetRequiredService<IWorkspaceRegistry>();

        registry.SetCurrent(registry.All.Single(workspace => workspace.Name == name).Root);
    }

    private static InstalledTool Installed(string name, string url, bool global = false)
    {
        Assert.True(ToolId.TryParse($"github.{name}", out var id));
        Assert.True(ToolVersion.TryParse("1.0.0", out var version));

        var payload = new ToolPayload(ToolPayload.AnyRuntime, null, 0, null, "run");

        var manifest = new ToolManifest(
            1, id.Name, id.Name, "A tool", "Tools", null, version, Required: false, [payload]);

        var directory = $"/tools/{id.Value}";

        return new InstalledTool(
            id,
            version,
            directory,
            manifest,
            ToolCommand.For(payload, directory),
            Origin: new ToolOrigin(WebAddress.Parse(url), global));
    }

    private sealed class FakeUserDirectories(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state");

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "runtime");
    }
}
