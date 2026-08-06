namespace Kitbash.Core.Git;

/// <summary>
/// What a remote says some of its branches point at, asked over the wire with no objects
/// transferred. That is what lets a large repository be watched without a fetch.
/// </summary>
/// <param name="Reached">
/// False when the remote could not be asked at all. A remote that answered and simply has
/// none of the branches is reached with nothing in it, which is a different thing.
/// </param>
/// <param name="Tips">Full commit hashes, by branch name with no ref prefix.</param>
public sealed record GitRemoteTips(bool Reached, IReadOnlyDictionary<string, string> Tips)
{
    public static GitRemoteTips Unreachable { get; } =
        new(false, new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>What one branch points at, or null where the remote does not have it.</summary>
    public string? Tip(string branch) =>
        branch is { Length: > 0 } && Tips.TryGetValue(branch, out var hash) ? hash : null;
}
