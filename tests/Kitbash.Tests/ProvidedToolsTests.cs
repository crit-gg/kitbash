using Kitbash.Core.Platform;
using Kitbash.Tools;

namespace Kitbash.Tests;

/// <summary>
/// Which installed tools belong on the page in the open workspace. No window and no disk,
/// since the whole rule is what the lists say against what an install recorded.
/// </summary>
public sealed class ProvidedToolsTests
{
    private const string Foundry = "https://github.com/owner/foundry";
    private const string Splice = "https://github.com/owner/splice";

    /// <summary>The global list offers a tool everywhere, whatever workspace is open.</summary>
    [Fact]
    public void AGlobalRepositoryProvidesItsToolHere()
    {
        var list = new FakeToolRepositoryList
        {
            InForce = [Global(Foundry)],
        };

        var provided = Assert.Single(new ProvidedTools(list).Here([Installed(Foundry, global: true)]));

        Assert.Equal("github.foundry", provided.Tool.Id.Value);

        // Nothing for the mark to say, since it is not a workspace's tool.
        Assert.Empty(provided.Workspaces);
    }

    /// <summary>
    /// The open workspace's own list provides it, and the mark names every workspace that
    /// lists the same repository rather than the open one alone.
    /// </summary>
    [Fact]
    public void AWorkspaceRepositoryProvidesItsToolAndNamesEveryWorkspace()
    {
        var list = new FakeToolRepositoryList
        {
            InForce = [In("Art", Foundry)],
            Workspaces = [In("Art", Foundry), In("Docs", Foundry), In("Art", Splice)],
        };

        var provided = Assert.Single(new ProvidedTools(list).Here([Installed(Foundry, global: false)]));

        Assert.Equal(["Art", "Docs"], provided.Workspaces);
    }

    /// <summary>
    /// A workspace that is not open provides it, so it belongs there and not here. This is
    /// the whole point: installing a tool does not make it the machine's.
    /// </summary>
    [Fact]
    public void AToolAnotherWorkspaceProvidesIsNotHere()
    {
        var list = new FakeToolRepositoryList
        {
            InForce = [],
            Workspaces = [In("Docs", Foundry)],
        };

        Assert.Empty(new ProvidedTools(list).Here([Installed(Foundry, global: false)]));
    }

    /// <summary>
    /// The repository has left every list. One the global list installed stays, since
    /// nothing else could take it off the machine, and one a workspace installed goes.
    /// </summary>
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void ARepositoryNoListNamesLeavesTheInstallToDecide(bool global, int expected)
    {
        var list = new FakeToolRepositoryList
        {
            InForce = [Global(Splice)],
            Workspaces = [In("Docs", Splice)],
        };

        Assert.Equal(expected, new ProvidedTools(list).Here([Installed(Foundry, global)]).Count);
    }

    /// <summary>
    /// A folder somebody pointed at is on this machine rather than in a repository, so no
    /// workspace list has anything to say about it.
    /// </summary>
    [Fact]
    public void ALinkedToolIsAlwaysHere()
    {
        var list = new FakeToolRepositoryList
        {
            InForce = [],
        };

        Assert.True(ToolId.TryParse("local.foundry", out var id));

        var linked = Tool(id, origin: null) with { IsLinked = true };

        Assert.Empty(Assert.Single(new ProvidedTools(list).Here([linked])).Workspaces);
    }

    /// <summary>
    /// Nothing recorded where it came from, which is a tool installed before this was
    /// written. Hiding it would leave no way to take it off, so it stays.
    /// </summary>
    [Fact]
    public void AToolWithNothingRecordedIsHere()
    {
        var list = new FakeToolRepositoryList
        {
            InForce = [],
        };

        Assert.Single(new ProvidedTools(list).Here([Installed(url: null, global: false)]));
    }

    /// <summary>
    /// The global list wins whichever workspace also names the repository, so the mark stays
    /// off a tool that is offered everywhere anyway.
    /// </summary>
    [Fact]
    public void AGlobalEntryBeatsAWorkspaceNamingTheSameRepository()
    {
        var list = new FakeToolRepositoryList
        {
            InForce = [Global(Foundry), In("Art", Foundry)],
            Workspaces = [In("Art", Foundry)],
        };

        Assert.Empty(Assert.Single(new ProvidedTools(list).Here([Installed(Foundry, global: false)])).Workspaces);
    }

    /// <summary>An address is a repository however it is spelled, so the match ignores case.</summary>
    [Fact]
    public void TheAddressIsMatchedWithoutRegardToCase()
    {
        var list = new FakeToolRepositoryList
        {
            InForce = [Global("https://GitHub.com/Owner/Foundry")],
        };

        Assert.Single(new ProvidedTools(list).Here([Installed(Foundry, global: false)]));
    }

    /// <summary>
    /// Nothing installed means neither list is read, since reading every workspace's config
    /// is a file per workspace and there is nothing to decide.
    /// </summary>
    [Fact]
    public void NothingInstalledReadsNothing()
    {
        var list = new FakeToolRepositoryList();

        Assert.Empty(new ProvidedTools(list).Here([]));
        Assert.Equal(0, list.Reads);
        Assert.Equal(0, list.WorkspaceReads);
    }

    /// <summary>
    /// The workspace lists are only read when a tool needs them, and only once however many
    /// tools do. A page of globally provided tools costs one read of the list in force.
    /// </summary>
    [Fact]
    public void TheWorkspaceListsAreReadAtMostOnce()
    {
        var list = new FakeToolRepositoryList
        {
            InForce = [Global(Foundry), In("Art", Splice)],
            Workspaces = [In("Art", Splice)],
        };

        var tools = new ProvidedTools(list);

        Assert.Equal(2, tools.Here([Installed(Foundry, global: true), Installed(Splice, global: false)]).Count);
        Assert.Equal(1, list.Reads);
        Assert.Equal(1, list.WorkspaceReads);

        Assert.Single(tools.Here([Installed(Foundry, global: true)]));
        Assert.Equal(2, list.Reads);
        Assert.Equal(1, list.WorkspaceReads);
    }

    private static ToolRepositorySource Global(string url) =>
        new(ToolRepositorySource.GitHub, WebAddress.Parse(url), "the global config");

    private static ToolRepositorySource In(string workspace, string url) =>
        new(ToolRepositorySource.GitHub, WebAddress.Parse(url), $"the {workspace} workspace", workspace);

    /// <summary>A tool named after the last part of its repository, so the two agree.</summary>
    private static InstalledTool Installed(string? url, bool global)
    {
        var name = url is null ? "foundry" : url[(url.LastIndexOf('/') + 1)..];

        Assert.True(ToolId.TryParse($"github.{name}", out var id));

        return Tool(id, url is null ? null : new ToolOrigin(WebAddress.Parse(url), global));
    }

    private static InstalledTool Tool(ToolId id, ToolOrigin? origin)
    {
        Assert.True(ToolVersion.TryParse("1.0.0", out var version));

        var payload = new ToolPayload(ToolPayload.AnyRuntime, null, 0, null, "run");

        var manifest = new ToolManifest(
            1,
            id.Name,
            id.Name,
            "A tool",
            "Tools",
            null,
            version,
            Required: false,
            [payload]);

        var directory = $"/tools/{id.Value}";

        return new InstalledTool(
            id, version, directory, manifest, ToolCommand.For(payload, directory), Origin: origin);
    }

    /// <summary>
    /// Two fixed lists, counting the reads, since when each one is read is part of what is
    /// being checked. The global half is what is in force with no workspace against it.
    /// </summary>
    private sealed class FakeToolRepositoryList : IToolRepositoryList
    {
        public IReadOnlyList<ToolRepositorySource> InForce { get; init; } = [];

        public IReadOnlyList<ToolRepositorySource> Workspaces { get; init; } = [];

        public int Reads { get; private set; }

        public int WorkspaceReads { get; private set; }

        public IReadOnlyList<ToolRepositorySource> Read()
        {
            Reads++;
            return InForce;
        }

        public IReadOnlyList<ToolRepositorySource> ReadGlobal() =>
            [.. InForce.Where(source => source.Workspace is null)];

        public IReadOnlyList<ToolRepositorySource> ReadWorkspaces()
        {
            WorkspaceReads++;
            return Workspaces;
        }

        public void WriteGlobal(IReadOnlyList<ToolRepositorySource> repositories) =>
            throw new NotSupportedException();
    }
}
