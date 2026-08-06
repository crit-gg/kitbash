using Kitbash.Tools;

namespace Kitbash.Tests;

/// <summary>
/// A fixed set of installed tools, so what a page draws does not depend on what happens
/// to be under the state directory on this machine.
/// </summary>
public sealed class FakeInstalledTools(params string[] ids) : IInstalledTools
{
    public IReadOnlyList<InstalledTool> Read() => [.. ids.Select(Tool)];

    public void SetActiveVersion(ToolId id, ToolVersion version) => throw new NotSupportedException();

    public void Link(ToolId id, string directory) => throw new NotSupportedException();

    public void Unlink(ToolId id) => throw new NotSupportedException();

    /// <summary>Named after its id, capitalised, since only the name and the id are drawn.</summary>
    private static InstalledTool Tool(string value)
    {
        Assert.True(ToolId.TryParse(value, out var id));
        Assert.True(ToolVersion.TryParse("1.0.0", out var version));

        var payload = new ToolPayload(ToolPayload.AnyRuntime, null, 0, null, "run");

        var manifest = new ToolManifest(
            1,
            id.Name,
            char.ToUpperInvariant(id.Name[0]) + id.Name[1..],
            "A tool",
            "Tools",
            null,
            version,
            Required: false,
            [payload]);

        return new InstalledTool(id, version, $"/tools/{id.Value}", manifest, payload);
    }
}
