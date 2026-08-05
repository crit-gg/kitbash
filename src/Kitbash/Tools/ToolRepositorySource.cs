using Kitbash.Core.Platform;

namespace Kitbash.Tools;

/// <summary>
/// One entry of a repository list. The type is written in the file rather than worked out
/// from the url, since a self hosted forge and a plain git url look identical.
/// </summary>
/// <param name="Origin">
/// Where the entry is listed, in words, so a refusal can say which file to edit.
/// </param>
public sealed record ToolRepositorySource(string Type, WebAddress Url, string Origin)
{
    /// <summary>GitHub releases, the first and so far only kind.</summary>
    public const string GitHub = "github";

    public override string ToString() => Url.ToString();
}
