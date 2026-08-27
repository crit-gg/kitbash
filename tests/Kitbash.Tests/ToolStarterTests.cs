using Kitbash.Core.Platform;
using Kitbash.Tools;

namespace Kitbash.Tests;

/// <summary>What a tool is handed when it is opened, read off the request that was started.</summary>
public sealed class ToolStarterTests
{
    private const string Root = "/home/a/ws";

    [Fact]
    public void AToolIsToldWhichWorkspaceIsOpen()
    {
        var started = Start(Tool(takesWorkspace: true), Root);

        Assert.Equal([ToolStarter.WorkspaceArgument, Root], started.Arguments);
    }

    /// <summary>A tool keeping its own list of what it opens is handed nothing.</summary>
    [Fact]
    public void AManifestSayingNoWorkspaceIsHandedNone()
    {
        var started = Start(Tool(takesWorkspace: false), Root);

        Assert.Empty(started.Arguments);
    }

    [Fact]
    public void NoWorkspaceOpenMeansNoArgument()
    {
        var started = Start(Tool(takesWorkspace: true), null);

        Assert.Empty(started.Arguments);
    }

    private static ProcessRequest Start(InstalledTool tool, string? workspace)
    {
        var platform = new FakeStarts();

        new ToolStarter(platform).Start(tool, workspace);

        return Assert.Single(platform.Requests);
    }

    private static InstalledTool Tool(bool takesWorkspace)
    {
        Assert.True(ToolId.TryParse("rime", out var id));
        Assert.True(ToolVersion.TryParse("1.0.0", out var version));

        var payload = new ToolPayload(ToolPayload.AnyRuntime, null, 0, null, "Rime");

        var manifest = new ToolManifest(
            2, id.Name, "Rime", "Materials", "Tools", null, version, Required: false, [payload])
        {
            TakesWorkspace = takesWorkspace,
        };

        return new InstalledTool(
            id, version, "/tools/rime", manifest, ToolCommand.For(payload, "/tools/rime"));
    }

    private sealed class FakeStarts : IPlatformServices
    {
        public List<ProcessRequest> Requests { get; } = [];

        public PlatformKind Kind => PlatformKind.Linux;

        public void OpenInBrowser(WebAddress address) => throw new NotSupportedException();

        public void OpenInFileBrowser(DirectoryLocation location) => throw new NotSupportedException();

        public void StartDetached(ProcessRequest request) => Requests.Add(request);
    }
}
