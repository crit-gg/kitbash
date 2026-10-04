namespace Kitbash.Core.Platform;

/// <summary>
/// Asks this desktop to put a folder on every shell's PATH, for a Unix family member whose
/// answer differs.
/// </summary>
public interface IPathRequest
{
    /// <summary>Never throws. A desktop with nothing to ask answers <see cref="PathReach.NotOnPath"/>.</summary>
    Task<PathReach> AskAsync(string directory, CancellationToken cancellation);
}
