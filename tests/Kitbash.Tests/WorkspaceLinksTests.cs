using Kitbash.Core;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Workspaces;
using Kitbash.Ui.Controls;
using Kitbash.Workspaces;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Tests;

/// <summary>
/// Real workspaces on disk, each with its own links, read the way the launcher reads them.
/// </summary>
public sealed class WorkspaceLinksTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"kitbash-workspace-links-{Guid.NewGuid():N}");

    private readonly ServiceProvider _services;

    public WorkspaceLinksTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IUserDirectories>(new FakeUserDirectories(_root))
            .AddKitbashIO()
            .AddKitbashPlatform()
            .AddKitbashApplicationStorage()
            .AddKitbashWorkspaces()
            .AddSingleton<WorkspaceLog>()
            .AddSingleton<WorkspaceLinkIcons>()
            .AddSingleton<IWorkspaceLinks, WorkspaceLinks>()
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

    /// <summary>The file's own order, since a workspace decides what comes first.</summary>
    [Fact]
    public void TheLinksAreReadInTheOrderTheFileWritesThem()
    {
        Workspace(
            "Art",
            Link("Design docs", "https://example.com/design", "file"),
            Link("Issue tracker", "https://example.com/issues", "alert-circle"));

        Open("Art");

        var links = Links();

        Assert.Equal(["Design docs", "Issue tracker"], links.Select(link => link.Label));
        Assert.Equal("https://example.com/design", links[0].Address.ToString());
        Assert.Equal([IconGlyph.File, IconGlyph.AlertCircle], links.Select(link => link.Icon));
    }

    /// <summary>
    /// The set belongs to the workspace, so switching swaps the whole of it the way it
    /// moves the git and engine strips.
    /// </summary>
    [Fact]
    public void SwitchingWorkspaceSwapsTheSet()
    {
        Workspace("Art", Link("Design docs", "https://example.com/design"));
        Workspace("Docs", Link("Style guide", "https://example.com/style"));

        Open("Art");
        Assert.Equal(["Design docs"], Links().Select(link => link.Label));

        Open("Docs");
        Assert.Equal(["Style guide"], Links().Select(link => link.Label));
    }

    /// <summary>A workspace that names none, which is what leaves the section out.</summary>
    [Fact]
    public void AWorkspaceWithNoLinksReadsNone()
    {
        Workspace("Art");
        Open("Art");

        Assert.Empty(Links());
    }

    /// <summary>Nothing is open, so there is no file to read.</summary>
    [Fact]
    public void NoWorkspaceReadsNone()
    {
        Assert.Empty(Links());
    }

    /// <summary>
    /// A row Kitbash cannot use is left out and the rest of the list still draws. A whole
    /// section lost to one typo would be worse than a missing row.
    /// </summary>
    [Theory]
    [InlineData("label = \"No address\"")]
    [InlineData("url = \"https://example.com/nameless\"")]
    [InlineData("label = \"Not a web address\"\nurl = \"ftp://example.com/files\"")]
    [InlineData("label = \"Not absolute\"\nurl = \"example.com/design\"")]
    public void ARowKitbashCannotUseIsLeftOut(string row)
    {
        Workspace("Art", $"[[workspace.links]]{Environment.NewLine}{row.Replace("\n", Environment.NewLine, StringComparison.Ordinal)}{Environment.NewLine}", Link("Design docs", "https://example.com/design"));

        Open("Art");

        Assert.Equal(["Design docs"], Links().Select(link => link.Label));
    }

    /// <summary>
    /// The icon set is closed, so a name outside it draws the link mark rather than taking
    /// the row away. Box Icons spells its names with hyphens and the glyphs do not.
    /// </summary>
    [Theory]
    [InlineData("git-branch", IconGlyph.GitBranch)]
    [InlineData("GitBranch", IconGlyph.GitBranch)]
    [InlineData("NETWORK-CHART", IconGlyph.NetworkChart)]
    [InlineData("teapot", IconGlyph.Link)]
    [InlineData("", IconGlyph.Link)]
    public void AnIconNameOutsideTheSetFallsBackToTheLinkMark(string icon, IconGlyph expected)
    {
        Workspace("Art", Link("Repository", "https://example.com/repo", icon));
        Open("Art");

        Assert.Equal(expected, Assert.Single(Links()).Icon);
    }

    /// <summary>
    /// The two layers add up rather than one winning, so a person's own links join the
    /// team's instead of hiding them. The team's come first, since they are the ones
    /// everybody opening this workspace has.
    /// </summary>
    [Fact]
    public void ThePersonalLayerAddsToTheTeamList()
    {
        var root = Workspace("Art", Link("Design docs", "https://example.com/design"));

        Personal(root, Link("My board", "https://example.com/board"));

        Open("Art");

        Assert.Equal(["Design docs", "My board"], Links().Select(link => link.Label));
    }

    /// <summary>
    /// One layer is what a page edits, so each is read and written on its own and the
    /// other is left exactly as it was.
    /// </summary>
    [Fact]
    public void EachLayerIsReadOnItsOwn()
    {
        var root = Workspace("Art", Link("Design docs", "https://example.com/design"));

        Personal(root, Link("My board", "https://example.com/board"));

        var links = _services.GetRequiredService<IWorkspaceLinks>();

        Assert.Equal(
            ["Design docs"],
            links.ReadEntries(root, SettingsLayer.TeamShared).Select(entry => entry.Label));

        Assert.Equal(
            ["My board"],
            links.ReadEntries(root, SettingsLayer.User).Select(entry => entry.Label));
    }

    /// <summary>
    /// A row Kitbash cannot draw is still a row here, so the settings page can show it and
    /// a save can never quietly drop what somebody typed.
    /// </summary>
    [Fact]
    public void AnUnusableRowIsStillAnEntry()
    {
        var root = Workspace(
            "Art",
            $"[[workspace.links]]{Environment.NewLine}label = \"Half a link\"{Environment.NewLine}");

        var entry = Assert.Single(
            _services.GetRequiredService<IWorkspaceLinks>().ReadEntries(root, SettingsLayer.TeamShared));

        Assert.Equal("Half a link", entry.Label);
        Assert.Equal(string.Empty, entry.Url);
    }

    /// <summary>
    /// Writing one layer replaces its list and leaves the other file alone, which is what
    /// makes the two settings pages independent.
    /// </summary>
    [Fact]
    public void WritingOneLayerLeavesTheOtherAlone()
    {
        var root = Workspace("Art", Link("Design docs", "https://example.com/design"));

        Personal(root, Link("My board", "https://example.com/board"));

        var links = _services.GetRequiredService<IWorkspaceLinks>();

        links.Write(
            root,
            SettingsLayer.User,
            [new WorkspaceLinkEntry("My board", "https://example.com/board", "table")]);

        Assert.Equal(
            ["Design docs"],
            links.ReadEntries(root, SettingsLayer.TeamShared).Select(entry => entry.Label));

        var written = Assert.Single(links.ReadEntries(root, SettingsLayer.User));

        Assert.Equal("table", written.Icon);

        Open("Art");
        Assert.Equal([IconGlyph.Link, IconGlyph.Table], Links().Select(link => link.Icon));
    }

    /// <summary>
    /// A row with no icon keeps none, so a save never writes a key in where the file had
    /// nothing and the row goes on drawing the link mark.
    /// </summary>
    [Fact]
    public void ARowWithNoIconIsWrittenWithout()
    {
        var root = Workspace("Art");
        var links = _services.GetRequiredService<IWorkspaceLinks>();

        links.Write(
            root,
            SettingsLayer.TeamShared,
            [new WorkspaceLinkEntry("Design docs", "https://example.com/design", string.Empty)]);

        var file = new WorkspacePaths(root).FileFor(SettingsScope.Global, SettingsLayer.TeamShared);

        Assert.DoesNotContain("icon", File.ReadAllText(file), StringComparison.Ordinal);
        Assert.Equal(string.Empty, Assert.Single(links.ReadEntries(root, SettingsLayer.TeamShared)).Icon);
    }

    /// <summary>The personal file, which a workspace does not start with.</summary>
    private static void Personal(string root, string links)
    {
        var file = new WorkspacePaths(root).FileFor(SettingsScope.Global, SettingsLayer.User);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, links);
    }

    /// <summary>
    /// A config nobody can parse loses its links rather than the app. Broken after the
    /// workspace was opened, since the registry reads the same file to name it.
    /// </summary>
    [Fact]
    public void AConfigThatWillNotParseReadsNone()
    {
        var root = Workspace("Art", Link("Design docs", "https://example.com/design"));

        Open("Art");
        Assert.Single(Links());

        var paths = new WorkspacePaths(root);

        File.WriteAllText(paths.FileFor(SettingsScope.Global, SettingsLayer.TeamShared), "[[[nope");

        Assert.Empty(Links());
    }

    /// <summary>
    /// The sample in the config every workspace gets is what a person copies, so it has to
    /// be a link the reader takes. Uncommented exactly as it is written.
    /// </summary>
    [Fact]
    public void TheSampleInTheScaffoldedConfigReadsBack()
    {
        var root = Path.Combine(_root, "workspaces", "Art");

        Directory.CreateDirectory(root);

        // Registering scaffolds the config, which is the file under test here.
        _services.GetRequiredService<IWorkspaceRegistry>().Add(root);

        var file = new WorkspacePaths(root).FileFor(SettingsScope.Global, SettingsLayer.TeamShared);
        var lines = File.ReadAllLines(file);
        var block = false;

        // The block runs from its header to the blank line under it. Everything after that
        // is the next setting's own commented sample.
        for (var at = 0; at < lines.Length && !(block && lines[at].Length == 0); at++)
        {
            block = block || lines[at].Contains("[[workspace.links]]", StringComparison.Ordinal);

            if (block)
            {
                lines[at] = lines[at][2..];
            }
        }

        File.WriteAllLines(file, lines);

        Open("Art");

        var link = Assert.Single(Links());

        Assert.Equal("Design docs", link.Label);
        Assert.Equal("https://example.com/design", link.Address.ToString());
        Assert.Equal(IconGlyph.File, link.Icon);
    }

    private IReadOnlyList<WorkspaceLink> Links() =>
        _services.GetRequiredService<IWorkspaceLinks>().Read();

    private static string Link(string label, string url, string? icon = null)
    {
        var text = $"[[workspace.links]]{Environment.NewLine}"
            + $"label = \"{label}\"{Environment.NewLine}"
            + $"url = \"{url}\"{Environment.NewLine}";

        return icon is null
            ? text
            : text + $"icon = \"{icon}\"{Environment.NewLine}";
    }

    /// <summary>A folder with a workspace config holding the blocks given.</summary>
    private string Workspace(string name, params string[] links)
    {
        var root = Path.Combine(_root, "workspaces", name);
        var paths = new WorkspacePaths(root);
        var file = paths.FileFor(SettingsScope.Global, SettingsLayer.TeamShared);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        // Written before the folder is registered, since registering scaffolds a config
        // into a workspace that has none and this one is meant to have its own.
        File.WriteAllText(file, string.Join(Environment.NewLine, links));

        _services.GetRequiredService<IWorkspaceRegistry>().Add(root);

        return root;
    }

    private void Open(string name)
    {
        var registry = _services.GetRequiredService<IWorkspaceRegistry>();

        registry.SetCurrent(registry.All.Single(workspace => workspace.Name == name).Root);
    }

    private sealed class FakeUserDirectories(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state");

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "runtime");
    }
}
