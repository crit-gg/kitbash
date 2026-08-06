using Kitbash.Tools;

namespace Kitbash.Tests;

/// <summary>A script tool built in memory, for what a form and a run do with one.</summary>
public sealed class FakeScriptTool
{
    public static InstalledTool Built(params ToolInput[] inputs) =>
        Built("/tools/sprites", "run.sh", inputs);

    public static InstalledTool Built(string directory, string executable, params ToolInput[] inputs)
    {
        Assert.True(ToolId.TryParse("sprites", out var id));
        Assert.True(ToolVersion.TryParse("1.0.0", out var version));

        var payload = new ToolPayload(ToolPayload.AnyRuntime, null, 0, null, executable);

        var manifest = new ToolManifest(
            2,
            id.Name,
            "Sprites",
            "A script",
            "Tools",
            null,
            version,
            Required: false,
            [payload],
            ToolKind.Script)
        {
            Inputs = inputs,
        };

        return new InstalledTool(id, version, directory, manifest, payload);
    }
}
