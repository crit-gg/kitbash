using System.Globalization;
using System.Text.Json;
using Workbench.Core.IO;
using Workbench.Core.Platform;
using Workbench.Core.Settings;

namespace Workbench.Core.Godot;

internal sealed class EngineCatalogue : IEngineCatalogue
{
    private const string Feed = "https://godotengine.org/versions.json";
    private const string Manifests = "https://raw.githubusercontent.com/godotengine/godot-builds/main/releases";
    private const string FeedCacheName = "godot-versions.json";
    // The feed is a static file on a website, not a CDN edge, so it is slow to answer.
    private static readonly TimeSpan FeedLife = TimeSpan.FromHours(48);

    private readonly IWebContent _web;
    private readonly IFileSystem _files;
    private readonly ApplicationPaths _paths;
    private readonly TimeProvider _time;

    public EngineCatalogue(
        IWebContent web,
        IFileSystem files,
        ApplicationPaths paths,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(web);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(time);

        _web = web;
        _files = files;
        _paths = paths;
        _time = time;
    }

    public async Task<EngineReleases> ReadReleasesAsync(bool refresh, CancellationToken cancellationToken)
    {
        var cache = _paths.CacheFileFor(FeedCacheName);
        var written = _files.GetLastWriteTime(cache);

        if (!refresh && written is not null && _time.GetUtcNow() - written.Value < FeedLife)
        {
            if (TryReadCached(cache, ReadReleases, out var fresh))
            {
                return new EngineReleases(fresh, written.Value, IsStale: false);
            }
        }

        try
        {
            var text = await _web
                .ReadTextAsync(WebAddress.Parse(Feed), cancellationToken)
                .ConfigureAwait(false);

            var releases = ReadReleases(text);

            Save(cache, text);

            return new EngineReleases(releases, _time.GetUtcNow(), IsStale: false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // A list from yesterday beats an error, so the cache answers however old it is
            // and the page says it is stale.
            if (written is not null && TryReadCached(cache, ReadReleases, out var stale))
            {
                return new EngineReleases(stale, written.Value, IsStale: true);
            }

            throw new EngineCatalogueException(
                "The Godot version list could not be read and nothing was cached.",
                error);
        }
    }

    public async Task<EngineManifest> ReadManifestAsync(EngineTag tag, CancellationToken cancellationToken)
    {
        var cache = _paths.CacheFileFor($"godot-{tag}.json");

        if (_files.FileExists(cache) && TryReadCached(cache, text => ReadManifest(tag, text), out var cached))
        {
            return cached;
        }

        try
        {
            var text = await _web
                .ReadTextAsync(WebAddress.Parse($"{Manifests}/godot-{tag}.json"), cancellationToken)
                .ConfigureAwait(false);

            var manifest = ReadManifest(tag, text);

            Save(cache, text);

            return manifest;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw new EngineCatalogueException($"The file list for Godot {tag} could not be read.", error);
        }
    }

    /// <summary>
    /// Reads the feed, which is an array of version entries each holding releases. A tag
    /// that does not parse is skipped. Dates are invariant day month year.
    /// </summary>
    private static IReadOnlyList<EngineRelease> ReadReleases(string text)
    {
        using var document = JsonDocument.Parse(text);
        var releases = new List<EngineRelease>();

        foreach (var version in document.RootElement.EnumerateArray())
        {
            if (!version.TryGetProperty("name", out var name)
                || !version.TryGetProperty("releases", out var list))
            {
                continue;
            }

            foreach (var release in list.EnumerateArray())
            {
                var built = ReadRelease(name.GetString(), release);

                if (built is not null)
                {
                    releases.Add(built);
                }
            }
        }

        // By date, not by version. A patch of an older line can ship after a newer minor.
        // Version order breaks the tie, since a date is a day and several land on one.
        releases.Sort((left, right) =>
        {
            var by = right.Released.CompareTo(left.Released);

            return by != 0 ? by : right.Tag.CompareTo(left.Tag);
        });

        return releases;
    }

    private static EngineRelease? ReadRelease(string? version, JsonElement release)
    {
        if (!release.TryGetProperty("name", out var label))
        {
            return null;
        }

        // Anything the grammar refuses is older than Godot 4, so dropping it costs nothing
        // and saves the rest of this from ever seeing a shape it cannot read.
        if (!EngineTag.TryParse($"{version}-{label.GetString()}", out var tag) || !tag.IsSupported)
        {
            return null;
        }

        var released = release.TryGetProperty("release_date", out var date)
            && DateOnly.TryParseExact(
                date.GetString(),
                "d MMMM yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
                ? parsed
                : default;

        WebAddress? notes = null;

        if (release.TryGetProperty("release_notes", out var url)
            && !string.IsNullOrWhiteSpace(url.GetString())
            && Uri.TryCreate(url.GetString(), UriKind.Absolute, out var address))
        {
            try
            {
                notes = WebAddress.Parse(address);
            }
            catch (ArgumentException)
            {
                notes = null;
            }
        }

        return new EngineRelease(tag, released, notes);
    }

    private static EngineManifest ReadManifest(EngineTag tag, string text)
    {
        using var document = JsonDocument.Parse(text);

        var names = new List<string>();
        var checksums = new Dictionary<string, string>(StringComparer.Ordinal);

        if (document.RootElement.TryGetProperty("files", out var files))
        {
            foreach (var file in files.EnumerateArray())
            {
                if (!file.TryGetProperty("filename", out var name) || name.GetString() is not { } fileName)
                {
                    continue;
                }

                names.Add(fileName);

                if (file.TryGetProperty("checksum", out var checksum) && checksum.GetString() is { } hash)
                {
                    checksums[fileName] = hash;
                }
            }
        }

        return new EngineManifest(
            tag,
            EngineBuild.ReadAll(tag, names),
            checksums,
            EngineBuild.PublishedTargets(tag, names));
    }

    /// <summary>
    /// A cached copy that will not parse is treated as absent rather than as a failure,
    /// since the answer is to fetch it again and overwrite it.
    /// </summary>
    private bool TryReadCached<T>(string path, Func<string, T> read, out T value)
    {
        try
        {
            value = read(_files.ReadAllText(path));

            return true;
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        {
            value = default!;

            return false;
        }
    }

    /// <summary>
    /// The cache is an optimisation, so failing to write one is never worth failing a read
    /// over. A full disk costs a request next time and nothing else.
    /// </summary>
    private void Save(string path, string text)
    {
        try
        {
            _files.CreateDirectory(_paths.Cache);
            _files.WriteAllText(path, text);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }
}
