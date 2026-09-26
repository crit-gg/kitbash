using System.Text.Json;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;

namespace Kitbash.Core.Godot;

/// <summary>
/// An engine repository on GitHub releases. One call to the releases API lists every
/// release with its assets and their digests, so both operations are answered from it.
/// </summary>
internal sealed class GitHubEngineRepository : IEngineRepository
{
    private const string Api = "https://api.github.com/repos";

    /// <summary>One page holds more builds than anybody will walk back through.</summary>
    private const int PageSize = 100;

    /// <summary>
    /// GitHub allows 60 unauthenticated requests an hour per address, and a conditional
    /// request that comes back unchanged still costs one, so this is the protection.
    /// </summary>
    private static readonly TimeSpan ListLife = TimeSpan.FromMinutes(10);

    private readonly IWebContent _web;
    private readonly IFileSystem _files;
    private readonly ApplicationPaths _paths;
    private readonly TimeProvider _time;

    /// <summary>Every release's assets from the last list read, by release name.</summary>
    private volatile IReadOnlyDictionary<string, Published> _published = new Dictionary<string, Published>();

    public GitHubEngineRepository(
        EngineRepositoryAddress address,
        IWebContent web,
        IFileSystem files,
        ApplicationPaths paths,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(web);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(time);

        Address = address;
        _web = web;
        _files = files;
        _paths = paths;
        _time = time;
    }

    public EngineRepositoryAddress Address { get; }

    EngineRepositoryAddress? IEngineRepository.Address => Address;

    public async Task<EngineReleases> ReadReleasesAsync(bool refresh, CancellationToken cancellationToken)
    {
        var cache = _paths.CacheFileFor($"engine-releases-{Address.Kind}-{Address.Owner}-{Address.Name}.json");
        var written = _files.GetLastWriteTime(cache);

        if (!refresh && written is not null && _time.GetUtcNow() - written.Value < ListLife
            && TryRead(cache, out var fresh))
        {
            return fresh with { ReadAt = written.Value };
        }

        var address = WebAddress.Parse($"{Api}/{Address.Owner}/{Address.Name}/releases?per_page={PageSize}");

        try
        {
            var text = await _web.ReadTextAsync(address, cancellationToken).ConfigureAwait(false);
            var releases = Read(text) with { ReadAt = _time.GetUtcNow() };

            Save(cache, text);

            return releases;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Offline or over the hourly allowance, the last list answers however old it is.
            if (written is not null && TryRead(cache, out var stale))
            {
                return stale with { ReadAt = written.Value, IsStale = true };
            }

            throw new EngineCatalogueException($"The releases of {Address} could not be read.", error);
        }
    }

    public async Task<EngineManifest> ReadManifestAsync(string release, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(release);

        if (!_published.ContainsKey(release))
        {
            await ReadReleasesAsync(refresh: false, cancellationToken).ConfigureAwait(false);
        }

        if (!_published.TryGetValue(release, out var published))
        {
            throw new EngineCatalogueException($"{Address} has no release named {release}.");
        }

        var names = published.Assets.Keys.ToList();

        return new EngineManifest(
            published.Release.Tag,
            EngineBuild.ReadAll(published.Release, published.Assets),
            published.Digests,
            EngineBuild.PublishedTargets(published.Release.FileTag, names))
        {
            ChecksumKind = EngineChecksumKind.Sha256,
            Templates = EngineBuild.ReadTemplates(
                published.Release.FileTag,
                published.Assets.Select(asset => (asset.Key, asset.Value))),
        };
    }

    /// <summary>
    /// Reads a tag such as <c>4.7.2-slopworks-18d5d19</c> into the base version and the
    /// build, which is the last part. False for any tag that is not that shape.
    /// </summary>
    public static bool TryReadTag(string tag, out EngineTag number, out string build)
    {
        number = default;
        build = string.Empty;

        var parts = tag.Split('-');

        if (parts.Length < 3
            || parts[1..^1].Any(part => part.Length == 0)
            || !EngineTag.TryParseNumber(parts[0], out number)
            || !EngineId.IsBuild(parts[^1]))
        {
            return false;
        }

        build = parts[^1];

        return true;
    }

    private bool TryRead(string path, out EngineReleases releases)
    {
        try
        {
            releases = Read(_files.ReadAllText(path));

            return true;
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        {
            releases = null!;

            return false;
        }
    }

    /// <summary>
    /// Every release a newest pin could follow or an exact pin could name. A draft is left
    /// out and a tag that is not a repository build is counted rather than listed.
    /// </summary>
    private EngineReleases Read(string text)
    {
        using var document = JsonDocument.Parse(text);

        if (document.RootElement.ValueKind is not JsonValueKind.Array)
        {
            throw new JsonException("The release list is not an array.");
        }

        var releases = new List<EngineRelease>();
        var published = new Dictionary<string, Published>(StringComparer.Ordinal);
        var skipped = 0;

        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("draft", out var draft) && draft.ValueKind is JsonValueKind.True)
            {
                continue;
            }

            if (!item.TryGetProperty("tag_name", out var tagName)
                || tagName.GetString() is not { Length: > 0 } name
                || !TryReadTag(name, out var number, out var build))
            {
                skipped++;
                continue;
            }

            var at = item.TryGetProperty("published_at", out var date)
                && date.TryGetDateTimeOffset(out var parsed)
                    ? parsed
                    : default;

            var release = new EngineRelease(number, DateOnly.FromDateTime(at.UtcDateTime), Page(item))
            {
                Repository = Address,
                Name = name,
                Build = build,
                IsPrerelease = item.TryGetProperty("prerelease", out var preview)
                    && preview.ValueKind is JsonValueKind.True,
                PublishedAt = at,
            };

            releases.Add(release);
            published[name] = Assets(release, item);
        }

        _published = published;

        return new EngineReleases(
            [.. releases.OrderByDescending(release => release.PublishedAt)],
            default,
            IsStale: false)
        {
            Skipped = skipped,
        };
    }

    private static WebAddress? Page(JsonElement item)
    {
        if (!item.TryGetProperty("html_url", out var url) || url.GetString() is not { Length: > 0 } text)
        {
            return null;
        }

        try
        {
            return WebAddress.Parse(text);
        }
        catch (Exception error) when (error is ArgumentException or FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Every asset GitHub can address, with the digest it states. A digest that is not
    /// SHA 256 is left out, so a build without one is refused at install.
    /// </summary>
    private static Published Assets(EngineRelease release, JsonElement item)
    {
        var assets = new Dictionary<string, WebAddress>(StringComparer.Ordinal);
        var digests = new Dictionary<string, string>(StringComparer.Ordinal);

        if (item.TryGetProperty("assets", out var list) && list.ValueKind is JsonValueKind.Array)
        {
            foreach (var asset in list.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var name)
                    || !asset.TryGetProperty("browser_download_url", out var url)
                    || name.GetString() is not { Length: > 0 } assetName
                    || url.GetString() is not { Length: > 0 } assetUrl)
                {
                    continue;
                }

                try
                {
                    assets[assetName] = WebAddress.Parse(assetUrl);
                }
                catch (Exception error) when (error is ArgumentException or FormatException)
                {
                    continue;
                }

                if (asset.TryGetProperty("digest", out var digest)
                    && digest.GetString() is { } stated
                    && stated.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                {
                    digests[assetName] = stated["sha256:".Length..].ToLowerInvariant();
                }
            }
        }

        return new Published(release, assets, digests);
    }

    private void Save(string path, string text)
    {
        try
        {
            _files.CreateDirectory(_paths.Cache);
            _files.WriteAllText(path, text);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A cache that cannot be written costs a request next time and nothing else.
        }
    }

    private sealed record Published(
        EngineRelease Release,
        IReadOnlyDictionary<string, WebAddress> Assets,
        IReadOnlyDictionary<string, string> Digests);
}
