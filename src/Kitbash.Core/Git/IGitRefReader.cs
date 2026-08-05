namespace Kitbash.Core.Git;

/// <summary>
/// How far one revision is from another. Both counts are about the head, against the
/// baseline it was compared with.
/// </summary>
/// <param name="Ahead">Commits the head has that the baseline does not.</param>
/// <param name="Behind">Commits the baseline has that the head does not.</param>
public sealed record GitDivergence(int Ahead, int Behind)
{
    public static GitDivergence Level { get; } = new(0, 0);

    /// <summary>True when neither has anything the other lacks.</summary>
    public bool IsLevel => Ahead == 0 && Behind == 0;

    /// <summary>True when both moved, so one cannot be fast forwarded onto the other.</summary>
    public bool HasParted => Ahead > 0 && Behind > 0;
}

/// <summary>Answers questions about refs that are not about branches as a list.</summary>
public interface IGitRefReader
{
    /// <summary>
    /// The branch a remote calls its default, from its own HEAD. Null when the remote has
    /// not recorded one, which a repository that was never cloned usually has not.
    /// </summary>
    Task<string?> ReadDefaultBranchAsync(
        string root, string remote = "origin", CancellationToken cancellation = default);

    /// <summary>Whether git can resolve this to a commit.</summary>
    Task<bool> ExistsAsync(string root, string revision, CancellationToken cancellation = default);

    /// <summary>
    /// How far <paramref name="head"/> is from <paramref name="baseline"/>. Null when
    /// either does not resolve, which is a different answer from being level.
    /// </summary>
    Task<GitDivergence?> CompareAsync(
        string root, string baseline, string head, CancellationToken cancellation = default);

    /// <summary>The name of the branch the head is on, or null when it is detached.</summary>
    Task<string?> ReadHeadBranchAsync(string root, CancellationToken cancellation = default);
}
