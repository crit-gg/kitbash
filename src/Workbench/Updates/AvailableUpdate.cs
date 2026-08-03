using Velopack;

namespace Workbench.Updates;

/// <summary>
/// A release the feed is offering, and what downloading it would cost.
/// </summary>
/// <param name="Size">Bytes of the package that would be fetched.</param>
public sealed record AvailableUpdate(string Version, long Size)
{
    /// <summary>
    /// What the check found, carried through so the download does not look it up again.
    /// Internal, so nothing outside this folder sees a Velopack type.
    /// </summary>
    internal UpdateInfo? Found { get; init; }
}
