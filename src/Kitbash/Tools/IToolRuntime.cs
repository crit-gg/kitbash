namespace Kitbash.Tools;

/// <summary>Which of a tool's payloads, if any, runs on this machine.</summary>
public interface IToolRuntime
{
    /// <summary>This machine's runtime identifier, such as <c>linux-x64</c>.</summary>
    string Identifier { get; }

    /// <summary>
    /// The payload to install or run here, or null when the version has none. A version
    /// with no payload for this machine is not offered rather than offered and disabled.
    /// </summary>
    ToolPayload? PayloadFor(ToolManifest manifest);
}
