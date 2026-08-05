using System.Security.Cryptography;
using System.Text;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;

namespace Kitbash.Tools;

/// <summary>
/// Walks each repository's versions back from the newest until one has a payload for this
/// machine, and offers that. Everything it cannot use is left out and written to the log.
/// </summary>
public sealed class ToolCatalogue : IToolCatalogue
{
    /// <summary>
    /// How far back to look for a version this machine can run. A repository that never
    /// matches then costs a fixed number of requests rather than its whole history.
    /// </summary>
    private const int VersionsTried = 5;

    private readonly IToolRepositoryList _list;
    private readonly IToolRepositoryFactory _repositories;
    private readonly IToolManifestReader _manifests;
    private readonly IToolRuntime _runtime;
    private readonly IFileSystem _files;
    private readonly ApplicationPaths _paths;
    private readonly ToolLog _log;

    public ToolCatalogue(
        IToolRepositoryList list,
        IToolRepositoryFactory repositories,
        IToolManifestReader manifests,
        IToolRuntime runtime,
        IFileSystem files,
        ApplicationPaths paths,
        ToolLog log)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(manifests);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(log);

        _list = list;
        _repositories = repositories;
        _manifests = manifests;
        _runtime = runtime;
        _files = files;
        _paths = paths;
        _log = log;
    }

    public async Task<IReadOnlyList<OfferedTool>> ReadAsync(bool refresh, CancellationToken cancellationToken)
    {
        // The global list in file order, then each workspace list. Whoever claims an id
        // first is the only one offering it, and that order is what makes the refusal
        // land on the same repository twice running.
        Dictionary<string, string> taken = new(StringComparer.Ordinal);

        List<OfferedTool> offered = [];

        foreach (var source in _list.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await OfferAsync(source, taken, refresh, cancellationToken).ConfigureAwait(false) is { } tool)
            {
                taken[tool.Id.Value] = source.Url.ToString();
                offered.Add(tool);
            }
        }

        return [.. offered.OrderBy(tool => tool.Name, StringComparer.CurrentCulture)];
    }

    private async Task<OfferedTool?> OfferAsync(
        ToolRepositorySource source,
        Dictionary<string, string> taken,
        bool refresh,
        CancellationToken cancellationToken)
    {
        if (_repositories.For(source) is not { } repository)
        {
            _log.Say($"'{source.Type}' in {source.Origin} is not a repository type Kitbash knows");
            return null;
        }

        IReadOnlyList<ToolRelease> releases;

        try
        {
            releases = await repository.ListAsync(refresh, cancellationToken).ConfigureAwait(false);
        }
        catch (ToolRepositoryException exception)
        {
            _log.Say($"{source.Url} could not be read", exception);
            return null;
        }

        // A prerelease is a version and is not offered until there is a preview channel.
        var candidates = releases.Where(release => !release.IsPrerelease).Take(VersionsTried);

        foreach (var release in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await ReadAsync(repository, release, cancellationToken).ConfigureAwait(false) is not { } found)
            {
                continue;
            }

            var (manifest, json) = found;

            if (manifest.Version != release.Version)
            {
                _log.Say($"{source.Url} {release.Tag} holds a manifest saying {manifest.Version}");
                continue;
            }

            // Per version, not per tool: a tool may carry a payload for this machine at one
            // version and drop it at the next, and then the older one is what is offered.
            if (_runtime.PayloadFor(manifest) is not { } payload)
            {
                continue;
            }

            var id = ToolId.For(source.Type, manifest.Id);

            if (taken.TryGetValue(id.Value, out var holder))
            {
                _log.Say($"{source.Url} offers {id}, which is already offered by {holder}");
                return null;
            }

            return new OfferedTool(id, manifest.Version, manifest, json, payload, release, source);
        }

        return null;
    }

    /// <summary>
    /// The manifest for one release with the text it was read from, or null when the
    /// release has none this launcher can use. A release asset never changes, so this is
    /// cached under the version it came from and only the version list is fetched again.
    /// </summary>
    private async Task<(ToolManifest Manifest, string Json)?> ReadAsync(
        IToolRepository repository,
        ToolRelease release,
        CancellationToken cancellationToken)
    {
        var cache = _paths.CacheFileFor(CacheName(repository.Source, release));

        if (_files.FileExists(cache))
        {
            try
            {
                var cached = _files.ReadAllText(cache);

                return (_manifests.Read(cached), cached);
            }
            catch (Exception exception) when (exception is IOException or ToolManifestException)
            {
                _log.Say($"the cached manifest for {release.Tag} could not be used", exception);
            }
        }

        try
        {
            var text = await repository
                .ReadTextAsync(release, ToolManifestReader.FileName, cancellationToken)
                .ConfigureAwait(false);

            var manifest = _manifests.Read(text);

            Save(cache, text);

            return (manifest, text);
        }
        catch (Exception exception) when (exception is ToolRepositoryException or ToolManifestException)
        {
            _log.Say($"{repository.Source.Url} {release.Tag} was skipped", exception);
            return null;
        }
    }

    /// <summary>
    /// One file per repository and version. The host and path are folded into a name that
    /// is legal on both platforms, so nothing here depends on what a tag is called.
    /// </summary>
    private static string CacheName(ToolRepositorySource source, ToolRelease release)
    {
        var key = $"{source.Url}#{release.Tag}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(key));

        return $"tool-manifest-{Convert.ToHexStringLower(digest)[..16]}.json";
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
