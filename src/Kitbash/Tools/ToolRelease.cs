using Kitbash.Core.Platform;

namespace Kitbash.Tools;

/// <summary>
/// One version a repository offers, with the files published against it. The tag is the
/// version, so a release carries both and a manifest that disagrees is refused.
/// </summary>
/// <param name="Assets">The files on the release, by name.</param>
public sealed record ToolRelease(
    ToolVersion Version,
    string Tag,
    bool IsPrerelease,
    IReadOnlyDictionary<string, WebAddress> Assets);
