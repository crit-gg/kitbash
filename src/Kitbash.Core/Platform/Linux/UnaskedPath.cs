namespace Kitbash.Core.Platform.Linux;

/// <summary>
/// Linux, where PATH is the shell profile's, which is the person's file and not ours to edit.
/// </summary>
internal sealed class UnaskedPath : IPathRequest
{
    public Task<PathReach> AskAsync(string directory, CancellationToken cancellation) =>
        Task.FromResult(PathReach.NotOnPath);
}
