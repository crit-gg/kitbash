using Kitbash.Core.Platform;

namespace Kitbash.Tools;

/// <summary>
/// One entry of a repository list. The type is written in the file rather than worked out
/// from the url, since a self hosted forge and a plain git url look identical.
/// </summary>
/// <param name="Origin">
/// Where the entry is listed, in words, so a refusal can say which file to edit.
/// </param>
/// <param name="Workspace">
/// The name of the workspace whose list holds it, or null for the global list.
/// </param>
public sealed record ToolRepositorySource(
    string Type,
    WebAddress Url,
    string Origin,
    string? Workspace = null)
{
    /// <summary>GitHub releases, the first and so far only kind.</summary>
    public const string GitHub = "github";

    /// <summary>
    /// Every type <see cref="IToolRepositoryFactory"/> can build, in the order a chooser
    /// offers them. A file may still name one that is not here, which is refused when it
    /// is read rather than when it is written.
    /// </summary>
    public static IReadOnlyList<string> Kinds { get; } = [GitHub];

    public override string ToString() => Url.ToString();
}
