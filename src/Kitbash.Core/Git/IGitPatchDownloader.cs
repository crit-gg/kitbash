using Kitbash.Core.Platform;

namespace Kitbash.Core.Git;

/// <summary>Why a patch could not be downloaded.</summary>
public enum GitPatchDownloadFailure
{
    /// <summary>The address is not https, so the patch could be changed on its way.</summary>
    NotSecure,

    /// <summary>Larger than <see cref="IGitPatchDownloader.Limit"/>.</summary>
    TooLarge,

    /// <summary>The server refused, could not be reached or stopped answering.</summary>
    Failed,
}

public sealed class GitPatchDownloadException : Exception
{
    public GitPatchDownloadException(GitPatchDownloadFailure failure, string message, Exception? inner = null)
        : base(message, inner)
    {
        Failure = failure;
    }

    public GitPatchDownloadFailure Failure { get; }
}

/// <summary>Fetches a patch a link names into this app's own cache, for git am to read.</summary>
public interface IGitPatchDownloader
{
    /// <summary>The most a patch may be, in bytes.</summary>
    long Limit { get; }

    /// <summary>Downloads the patch and gives back the file it landed in.</summary>
    /// <exception cref="GitPatchDownloadException">It was not https, was too large or did not arrive.</exception>
    Task<string> DownloadAsync(WebAddress address, CancellationToken cancellation = default);

    /// <summary>Deletes a file this downloaded. Does nothing for one that has gone.</summary>
    void Discard(string path);
}
