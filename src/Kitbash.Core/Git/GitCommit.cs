namespace Kitbash.Core.Git;

/// <summary>
/// One commit as git describes it.
/// </summary>
/// <param name="Parents">
/// In git's own order, so the first is the branch this was committed on and the rest are
/// what was merged in.
/// </param>
/// <param name="AuthoredWhen">When the work was written, which a rebase or a patch keeps.</param>
/// <param name="CommittedWhen">When this commit object was made, which a rebase moves.</param>
/// <param name="Subject">The first line of the message, with no trailing newline.</param>
/// <param name="Body">Everything after the first blank line, or empty.</param>
/// <param name="Refs">
/// The names pointing here, as git decorates them: a branch is its bare name, the current
/// one reads <c>HEAD -> name</c>, a tag reads <c>tag: name</c>.
/// </param>
public sealed record GitCommit(
    string Hash,
    string ShortHash,
    IReadOnlyList<string> Parents,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset AuthoredWhen,
    string CommitterName,
    string CommitterEmail,
    DateTimeOffset CommittedWhen,
    string Subject,
    string Body,
    IReadOnlyList<string> Refs)
{
    public bool IsMerge => Parents.Count > 1;

    /// <summary>The subject and the body as one message, the way it was written.</summary>
    public string Message => Body.Length == 0 ? Subject : $"{Subject}\n\n{Body}";
}
