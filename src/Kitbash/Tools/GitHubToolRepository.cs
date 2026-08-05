using System.Text.Json;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;

namespace Kitbash.Tools;

/// <summary>
/// GitHub releases. A release is a version, its tag names it, and the manifest and the
/// payloads are assets on it, so one call to the releases API answers both operations.
/// </summary>
public sealed class GitHubToolRepository : IToolRepository
{
    private const string Api = "https://api.github.com/repos";

    /// <summary>
    /// One page is every version anybody will have. A hundred releases of one tool is far
    /// past what the launcher would ever walk back through.
    /// </summary>
    private const int PageSize = 100;

    /// <summary>
    /// How long a release list is kept. GitHub allows 60 unauthenticated requests an hour
    /// per address, so a handful of repositories asked on every launch runs into that.
    /// </summary>
    private static readonly TimeSpan ListLife = TimeSpan.FromHours(3);

    private readonly string _owner;
    private readonly string _repository;
    private readonly IWebContent _web;
    private readonly IFileSystem _files;
    private readonly ApplicationPaths _paths;
    private readonly TimeProvider _time;

    private GitHubToolRepository(
        ToolRepositorySource source,
        string owner,
        string repository,
        IWebContent web,
        IFileSystem files,
        ApplicationPaths paths,
        TimeProvider time)
    {
        Source = source;
        _owner = owner;
        _repository = repository;
        _web = web;
        _files = files;
        _paths = paths;
        _time = time;
    }

    public ToolRepositorySource Source { get; }

    /// <summary>
    /// Null when the url does not name a repository on github.com. A self hosted forge is
    /// the same shape with a different API root and is a repository type of its own.
    /// </summary>
    public static GitHubToolRepository? For(
        ToolRepositorySource source,
        IWebContent web,
        IFileSystem files,
        ApplicationPaths paths,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Url.Value.Host is not ("github.com" or "www.github.com"))
        {
            return null;
        }

        var segments = source.Url.Value.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2)
        {
            return null;
        }

        // A url copied out of the address bar often ends in .git or a trailing slash.
        var name = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segments[1][..^4]
            : segments[1];

        return name.Length == 0
            ? null
            : new GitHubToolRepository(source, segments[0], name, web, files, paths, time);
    }

    public async Task<IReadOnlyList<ToolRelease>> ListAsync(bool refresh, CancellationToken cancellationToken)
    {
        var cache = _paths.CacheFileFor($"tool-releases-{Slug()}.json");
        var written = _files.GetLastWriteTime(cache);

        if (!refresh && written is not null && _time.GetUtcNow() - written.Value < ListLife)
        {
            try
            {
                return Releases(_files.ReadAllText(cache));
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
                // A cache that will not read is asked again rather than reported.
            }
        }

        var address = WebAddress.Parse($"{Api}/{_owner}/{_repository}/releases?per_page={PageSize}");

        try
        {
            var text = await _web.ReadTextAsync(address, cancellationToken).ConfigureAwait(false);
            var releases = Releases(text);

            Save(cache, text);

            return releases;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ToolRepositoryException($"{Source.Url} could not be listed.", exception);
        }
    }

    public async Task<string> ReadTextAsync(
        ToolRelease release,
        string asset,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentException.ThrowIfNullOrWhiteSpace(asset);

        if (!release.Assets.TryGetValue(asset, out var address))
        {
            throw new ToolRepositoryException($"{Source.Url} {release.Tag} has no {asset}.");
        }

        try
        {
            return await _web.ReadTextAsync(address, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ToolRepositoryException($"{asset} could not be read from {release.Tag}.", exception);
        }
    }

    public async Task FetchAsync(
        ToolRelease release,
        string asset,
        string path,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentException.ThrowIfNullOrWhiteSpace(asset);

        // A public asset downloads from the release url with no API involved, which is
        // what keeps this off the hourly request allowance.
        if (!release.Assets.TryGetValue(asset, out var address))
        {
            throw new ToolRepositoryException($"{Source.Url} {release.Tag} has no {asset}.");
        }

        try
        {
            await _web.DownloadAsync(address, path, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ToolRepositoryException($"{asset} could not be fetched from {release.Tag}.", exception);
        }
    }

    /// <summary>
    /// Newest first. A tag that is not a version is skipped rather than failing the whole
    /// repository, since a repository with unrelated tags in its history is ordinary.
    /// </summary>
    private static IReadOnlyList<ToolRelease> Releases(string text)
    {
        using var document = JsonDocument.Parse(text);

        if (document.RootElement.ValueKind is not JsonValueKind.Array)
        {
            throw new JsonException("The release list is not an array.");
        }

        List<ToolRelease> releases = [];

        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (!release.TryGetProperty("tag_name", out var tag)
                || tag.GetString() is not { } name
                || !ToolVersion.TryParse(name, out var version))
            {
                continue;
            }

            releases.Add(new ToolRelease(
                version,
                name,
                release.TryGetProperty("prerelease", out var preview) && preview.ValueKind is JsonValueKind.True,
                Assets(release)));
        }

        return [.. releases.OrderByDescending(release => release.Version)];
    }

    private static IReadOnlyDictionary<string, WebAddress> Assets(JsonElement release)
    {
        Dictionary<string, WebAddress> assets = new(StringComparer.Ordinal);

        if (!release.TryGetProperty("assets", out var list) || list.ValueKind is not JsonValueKind.Array)
        {
            return assets;
        }

        foreach (var asset in list.EnumerateArray())
        {
            if (!asset.TryGetProperty("name", out var name)
                || !asset.TryGetProperty("browser_download_url", out var url)
                || name.GetString() is not { } assetName
                || url.GetString() is not { } assetUrl)
            {
                continue;
            }

            try
            {
                assets[assetName] = WebAddress.Parse(assetUrl);
            }
            catch (ArgumentException)
            {
                // An asset the launcher cannot address is one it cannot fetch either.
            }
        }

        return assets;
    }

    /// <summary>
    /// A file name for this repository's cache. The host is in it, so the same owner and
    /// name on two forges are two caches.
    /// </summary>
    private string Slug()
    {
        var name = $"{Source.Url.Value.Host}-{_owner}-{_repository}".ToLowerInvariant();

        return string.Create(name.Length, name, (span, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                span[index] = source[index] is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'
                    ? source[index]
                    : '-';
            }
        });
    }

    private void Save(string path, string text)
    {
        try
        {
            _files.CreateDirectory(Path.GetDirectoryName(path)!);
            _files.WriteAllText(path, text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A cache that cannot be written costs a request next time and nothing else.
        }
    }
}
