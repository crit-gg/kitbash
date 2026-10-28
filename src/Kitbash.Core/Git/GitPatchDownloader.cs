using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;

namespace Kitbash.Core.Git;

internal sealed class GitPatchDownloader : IGitPatchDownloader
{
    private const string FolderName = "patches";

    private readonly IWebContent _web;
    private readonly IFileSystem _fileSystem;
    private readonly string _folder;

    public GitPatchDownloader(IWebContent web, IFileSystem fileSystem, ApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(web);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(paths);

        _web = web;
        _fileSystem = fileSystem;
        _folder = Path.Combine(paths.Cache, FolderName);
    }

    /// <summary>64 MiB, room for a patch carrying art and well short of filling a disk.</summary>
    public long Limit => 64L * 1024 * 1024;

    public async Task<string> DownloadAsync(WebAddress address, CancellationToken cancellation = default)
    {
        if (address.Value.Scheme != Uri.UriSchemeHttps)
        {
            throw new GitPatchDownloadException(GitPatchDownloadFailure.NotSecure, "Only https is downloaded.");
        }

        // One download at a time is all the dialog ever holds, so whatever is here is a
        // file an earlier run did not get to discard.
        ClearFolder();

        var directory = Path.Combine(_folder, Path.GetRandomFileName());
        var file = Path.Combine(directory, FileNameFor(address));

        using var tooLarge = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

        try
        {
            _fileSystem.CreateDirectory(directory);

            await _web.DownloadAsync(address, file, new Ceiling(Limit, tooLarge), tooLarge.Token).ConfigureAwait(false);

            // The block that passes the limit can be the last one, and then the download
            // ends before anything reads the token.
            tooLarge.Token.ThrowIfCancellationRequested();

            return file;
        }
        catch (OperationCanceledException) when (tooLarge.IsCancellationRequested && !cancellation.IsCancellationRequested)
        {
            Discard(file);

            throw new GitPatchDownloadException(GitPatchDownloadFailure.TooLarge, $"Larger than {Limit} bytes.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutException or IOException
            or UnauthorizedAccessException)
        {
            Discard(file);

            throw new GitPatchDownloadException(GitPatchDownloadFailure.Failed, exception.Message, exception);
        }
    }

    public void Discard(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            if (Path.GetDirectoryName(path) is { } directory && _fileSystem.DirectoryExists(directory))
            {
                _fileSystem.DeleteDirectory(directory);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void ClearFolder()
    {
        try
        {
            if (_fileSystem.DirectoryExists(_folder))
            {
                _fileSystem.DeleteDirectory(_folder);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// The address's last segment when it is a plain file name, since git am names the stash
    /// after it. Anything else becomes a name of our own.
    /// </summary>
    private static string FileNameFor(WebAddress address)
    {
        var last = Uri.UnescapeDataString(address.Value.Segments.LastOrDefault() ?? string.Empty).Trim('/');

        var plain = last.Length is > 0 and <= 100
            && last.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')
            && !last.StartsWith('.');

        return plain ? last : "downloaded.patch";
    }

    /// <summary>Stops a download the moment it passes the limit, whatever the server said it was.</summary>
    private sealed class Ceiling(long limit, CancellationTokenSource stop) : IProgress<long>
    {
        public void Report(long value)
        {
            if (value > limit)
            {
                stop.Cancel();
            }
        }
    }
}
