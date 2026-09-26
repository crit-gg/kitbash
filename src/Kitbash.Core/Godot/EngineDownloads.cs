using System.Security.Cryptography;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;

namespace Kitbash.Core.Godot;

/// <summary>
/// Fetches a published file into the cache and checks it against the hash its source
/// published. A file that does not match never leaves the cache folder under its name.
/// </summary>
internal sealed class EngineDownloads
{
    private const string DoneSuffix = ".done";
    private const string PartSuffix = ".part";

    private readonly IWebContent _web;
    private readonly IFileSystem _files;
    private readonly ApplicationPaths _paths;

    public EngineDownloads(IWebContent web, IFileSystem files, ApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(web);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(paths);

        _web = web;
        _files = files;
        _paths = paths;
    }

    /// <summary>
    /// The file, from the cache when it is there and was verified, otherwise fetched and
    /// verified. Progress is bytes so far and the total, which is zero when the server
    /// would not say.
    /// </summary>
    /// <exception cref="EngineInstallException">The file did not match its hash.</exception>
    public async Task<string> FetchAsync(
        string fileName,
        WebAddress address,
        string checksum,
        EngineChecksumKind kind,
        Action<long, long>? progress,
        Action? verifying,
        CancellationToken cancellationToken)
    {
        var archive = _paths.CacheFileFor(fileName);
        var done = archive + DoneSuffix;

        if (_files.FileExists(archive) && _files.FileExists(done))
        {
            return archive;
        }

        var part = archive + PartSuffix;

        _files.CreateDirectory(_paths.Cache);
        _files.DeleteFile(part);

        var total = await _web.MeasureAsync(address, cancellationToken).ConfigureAwait(false) ?? 0;

        progress?.Invoke(0, total);

        try
        {
            await _web
                .DownloadAsync(address, part, new Progress<long>(bytes => progress?.Invoke(bytes, total)), cancellationToken)
                .ConfigureAwait(false);

            verifying?.Invoke();

            var actual = await HashAsync(part, kind, cancellationToken).ConfigureAwait(false);

            if (!string.Equals(actual, checksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new EngineInstallException(
                    $"{fileName} did not match the checksum its release published, so it was not installed.");
            }

            _files.MoveFile(part, archive, overwrite: true);
            _files.WriteAllText(done, actual);

            return archive;
        }
        catch
        {
            // Nothing half written survives, so the next attempt starts clean rather than
            // finding a part file it cannot tell apart from a good one.
            _files.DeleteFile(part);

            throw;
        }
    }

    private async Task<string> HashAsync(string path, EngineChecksumKind kind, CancellationToken cancellationToken)
    {
        await using var stream = _files.OpenRead(path);

        var hash = kind == EngineChecksumKind.Sha256
            ? await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)
            : await SHA512.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);

        return Convert.ToHexStringLower(hash);
    }
}
