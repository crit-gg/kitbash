using System.Runtime.InteropServices;

namespace Kitbash.Tools;

/// <summary>
/// Matches a manifest against this machine. The identifier is the one .NET reports for
/// the running host, so nothing here tests the operating system itself.
/// </summary>
public sealed class ToolRuntime : IToolRuntime
{
    public string Identifier { get; } = RuntimeInformation.RuntimeIdentifier;

    /// <summary>
    /// A payload naming this identifier wins, so a tool may ship a portable payload and
    /// replace it where it has something better. Matched ignoring case, since a runtime
    /// identifier is written in lower case and a manifest is written by hand often enough.
    /// </summary>
    public ToolPayload? PayloadFor(ToolManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        ToolPayload? portable = null;

        foreach (var payload in manifest.Payloads)
        {
            if (string.Equals(payload.Runtime, Identifier, StringComparison.OrdinalIgnoreCase))
            {
                return payload;
            }

            if (string.Equals(payload.Runtime, ToolPayload.AnyRuntime, StringComparison.OrdinalIgnoreCase))
            {
                portable ??= payload;
            }
        }

        return portable;
    }
}
