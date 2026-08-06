using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;

namespace Kitbash.Tools;

/// <summary>
/// Finds and fetches the file a manifest's icon names. An installed version keeps its own
/// beside its manifest and an offered one goes to the cache.
/// </summary>
public sealed class ToolIcons : IToolIcons
{
    /// <summary>
    /// The most an icon may be. A tile is 40 pixels, so this is far past any art for one
    /// and still refuses a server that answers with something else entirely.
    /// </summary>
    private const long MostBytes = 4L * 1024 * 1024;

    /// <summary>
    /// The icon while it is being fetched. The process id is part of it, so two launchers
    /// fetching one icon at once write to separate files.
    /// </summary>
    private static readonly string PartSuffix =
        ".part-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture);

    private readonly IToolRepositoryFactory _repositories;
    private readonly IFileSystem _files;
    private readonly ApplicationPaths _paths;
    private readonly ToolLog _log;

    public ToolIcons(
        IToolRepositoryFactory repositories,
        IFileSystem files,
        ApplicationPaths paths,
        ToolLog log)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(log);

        _repositories = repositories;
        _files = files;
        _paths = paths;
        _log = log;
    }

    public string? For(InstalledTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (tool.Manifest.Icon is not { } name)
        {
            return null;
        }

        var file = Path.Combine(tool.Directory, name);

        return _files.FileExists(file) ? file : null;
    }

    public async Task<string?> ForAsync(OfferedTool tool, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (tool.Manifest.Icon is not { } name)
        {
            return null;
        }

        var cache = _paths.CacheFileFor(CacheName(tool, name));

        if (_files.FileExists(cache))
        {
            return cache;
        }

        return await FetchAsync(tool, name, cache, cancellationToken).ConfigureAwait(false) ? cache : null;
    }

    public Task FetchIntoAsync(OfferedTool tool, string directory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        return tool.Manifest.Icon is { } name
            ? FetchAsync(tool, name, Path.Combine(directory, name), cancellationToken)
            : Task.CompletedTask;
    }

    /// <summary>
    /// One file per repository, version and name, folded into something legal on both
    /// platforms so nothing here depends on what a tag or an asset is called.
    /// </summary>
    private static string CacheName(OfferedTool tool, string name)
    {
        var key = $"{tool.Source.Url}#{tool.Release.Tag}#{name}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(key));

        return $"tool-icon-{Convert.ToHexStringLower(digest)[..16]}{Path.GetExtension(name)}";
    }

    /// <summary>
    /// Fetches one icon to one path, true when it is there afterwards. Every failure is
    /// logged and swallowed, since nothing a person asked for depends on the art arriving.
    /// </summary>
    private async Task<bool> FetchAsync(
        OfferedTool tool,
        string name,
        string path,
        CancellationToken cancellationToken)
    {
        if (_repositories.For(tool.Source) is not { } repository)
        {
            return false;
        }

        // Nothing declares an icon's size and nothing hashes it, so the download is
        // stopped as it runs rather than measured once it is already on the disk.
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var part = path + PartSuffix;

        try
        {
            _files.CreateDirectory(Path.GetDirectoryName(path)!);

            await repository
                .FetchAsync(
                    tool.Release,
                    name,
                    part,
                    new Progress<long>(bytes =>
                    {
                        if (bytes > MostBytes)
                        {
                            bound.Cancel();
                        }
                    }),
                    bound.Token)
                .ConfigureAwait(false);

            _files.MoveFile(part, path, overwrite: true);

            return true;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _log.Say($"{tool.Name} {tool.Version} could not fetch '{name}'", exception);

            return false;
        }
        finally
        {
            _files.DeleteFile(part);
        }
    }
}
