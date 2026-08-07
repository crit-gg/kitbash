using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;

namespace Kitbash.Tools;

/// <summary>
/// Downloads a payload, checks it, and puts the version directory in place whole. Nothing
/// in use is ever overwritten, so there is no partly updated state to recover from.
/// </summary>
public sealed class ToolInstaller : IToolInstaller
{
    /// <summary>
    /// Where a version is built before it takes its real name. The process id is part of
    /// it, so two launchers installing one tool at once build in separate directories and
    /// neither can delete the other's. The loser of the rename fails and installs nothing.
    /// </summary>
    private static readonly string IncomingSuffix =
        ".incoming-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture);

    /// <summary>The payload while it is being fetched. Named the same way, and why.</summary>
    private static readonly string DownloadName =
        "download-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ".part";

    /// <summary>The three execute bits of a Unix mode, owner, group and other.</summary>
    private const int Executable = 0b001_001_001;

    /// <summary>
    /// The most a payload may say it unpacks to. Far past any tool, so this refuses an
    /// absurd declaration without bounding a real one.
    /// </summary>
    private const long MostUnpacked = 4L * 1024 * 1024 * 1024;

    private readonly IToolRepositoryFactory _repositories;
    private readonly IInstalledTools _installed;
    private readonly IToolIcons _icons;
    private readonly IFileSystem _files;
    private readonly ApplicationPaths _paths;
    private readonly ToolLog _log;

    public ToolInstaller(
        IToolRepositoryFactory repositories,
        IInstalledTools installed,
        IToolIcons icons,
        IFileSystem files,
        ApplicationPaths paths,
        ToolLog log)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(installed);
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(log);

        _repositories = repositories;
        _installed = installed;
        _icons = icons;
        _files = files;
        _paths = paths;
        _log = log;
    }

    public async Task<InstalledTool> InstallAsync(
        OfferedTool tool,
        IProgress<ToolInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (_repositories.For(tool.Source) is not { } repository)
        {
            throw new ToolInstallException($"{tool.Name} comes from a kind of repository Kitbash cannot read.");
        }

        if (tool.Payload.Asset is not { } asset)
        {
            throw new ToolInstallException($"{tool.Name} {tool.Version} names no file to download.");
        }

        // Never extract and run something that was not checked. A repository is a web
        // server and a payload is a program, so an unhashed one is refused outright.
        if (tool.Payload.Sha256 is not { } expected)
        {
            throw new ToolInstallException($"{tool.Name} {tool.Version} published no checksum, so it was not installed.");
        }

        var root = _paths.ToolDirectoryFor(tool.Id.Value);
        var version = Path.Combine(root, tool.Version.ToString());
        var incoming = version + IncomingSuffix;
        var download = Path.Combine(root, DownloadName);

        _files.CreateDirectory(root);
        _files.DeleteFile(download);
        _files.DeleteDirectory(incoming);

        try
        {
            var total = tool.Payload.Size;

            progress?.Report(new ToolInstallProgress(ToolInstallStage.Downloading, 0, total));

            await repository
                .FetchAsync(
                    tool.Release,
                    asset,
                    download,
                    new Progress<long>(bytes =>
                        progress?.Report(new ToolInstallProgress(ToolInstallStage.Downloading, bytes, total))),
                    cancellationToken)
                .ConfigureAwait(false);

            progress?.Report(new ToolInstallProgress(ToolInstallStage.Verifying));

            await CheckAsync(tool, download, expected, cancellationToken).ConfigureAwait(false);

            progress?.Report(new ToolInstallProgress(ToolInstallStage.Extracting));

            Unpack(asset, download, incoming, cancellationToken);

            // The release's manifest is what describes this version, whatever the payload
            // happened to carry, so it is written over the top of anything unpacked.
            _files.WriteAllText(Path.Combine(incoming, ToolManifestReader.FileName), tool.ManifestJson);

            // The icon travels with the version, so an installed tool draws itself with
            // nothing to fetch. It is decoration, so a repository that does not serve it
            // leaves the card its letter rather than failing an install that worked.
            await _icons.FetchIntoAsync(tool, incoming, cancellationToken).ConfigureAwait(false);

            Runnable(tool, incoming);

            // A version already here is a reinstall of the same one, so it is replaced.
            // Nothing running is at risk, since a running tool holds the old directory.
            _files.DeleteDirectory(version);
            _files.MoveDirectory(incoming, version);

            _installed.SetActiveVersion(tool.Id, tool.Version);

            progress?.Report(new ToolInstallProgress(ToolInstallStage.Done));

            return new InstalledTool(
                tool.Id,
                tool.Version,
                version,
                tool.Manifest,
                ToolCommand.For(tool.Payload, version));
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not ToolInstallException)
        {
            throw new ToolInstallException($"{tool.Name} {tool.Version} could not be installed.", exception);
        }
        finally
        {
            // A failed install leaves a temporary file and nothing else, and not even that.
            _files.DeleteFile(download);
            _files.DeleteDirectory(incoming);
        }
    }

    public void Uninstall(InstalledTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        // A linked tool runs from a folder the person owns, so removing it forgets the
        // path. Deleting somebody's own build output because they took the tool off the
        // list would destroy work Kitbash never put there.
        if (tool.IsLinked)
        {
            _installed.Unlink(tool.Id);
            return;
        }

        var root = _paths.ToolDirectoryFor(tool.Id.Value);

        foreach (var directory in _files.EnumerateDirectories(root))
        {
            _files.DeleteDirectory(directory);
        }

        // Any launcher's leftover payload, not just this one's, since a kill during a
        // download is what leaves one behind.
        foreach (var file in _files.EnumerateFiles(root, recursive: false))
        {
            if (Path.GetFileName(file).StartsWith("download-", StringComparison.Ordinal))
            {
                _files.DeleteFile(file);
            }
        }
    }

    public void SweepOldVersions(IReadOnlyList<InstalledTool> installed)
    {
        ArgumentNullException.ThrowIfNull(installed);

        foreach (var tool in installed)
        {
            // A linked tool's folder is outside the tools directory, so every version
            // folder here would look like an old one and be swept.
            if (tool.IsLinked)
            {
                continue;
            }

            var root = _paths.ToolDirectoryFor(tool.Id.Value);

            foreach (var directory in _files.EnumerateDirectories(root))
            {
                if (string.Equals(directory, tool.Directory, StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    _files.DeleteDirectory(directory);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Windows refuses to delete a directory a running program has open, so
                    // an old version stays until a launch when nothing is using it.
                    _log.Say($"{directory} could not be removed", exception);
                }
            }
        }
    }

    private async Task CheckAsync(
        OfferedTool tool,
        string file,
        string expected,
        CancellationToken cancellationToken)
    {
        var length = _files.GetFileLength(file);

        if (tool.Payload.Size > 0 && length != tool.Payload.Size)
        {
            throw new ToolInstallException(
                $"{tool.Payload.Asset} is {length} bytes and the manifest says {tool.Payload.Size}, "
                + "so it was not installed.");
        }

        await using var stream = _files.OpenRead(file);

        var hash = Convert.ToHexStringLower(
            await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));

        if (!hash.Equals(expected.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ToolInstallException(
                $"{tool.Payload.Asset} does not match the checksum the manifest published, "
                + "so it was not installed.");
        }
    }

    /// <summary>
    /// A zip carries no Unix mode when it was made on Windows, so the file the manifest
    /// names is marked runnable whatever the archive said. Without it a Linux payload
    /// unpacks to something that cannot be started and nothing says why.
    /// </summary>
    private void Runnable(OfferedTool tool, string directory)
    {
        var executable = tool.Payload.ExecutableIn(directory);

        if (!_files.FileExists(executable))
        {
            throw new ToolInstallException(
                $"{tool.Payload.Asset} does not hold {tool.Payload.Executable}, so it was not installed.");
        }

        _files.MakeExecutableFile(executable);
    }

    /// <summary>
    /// tar.gz or zip, chosen by what the asset is called rather than by the platform, so a
    /// payload marked <c>any</c> unpacks the same way everywhere.
    /// </summary>
    private void Unpack(string asset, string archive, string directory, CancellationToken cancellationToken)
    {
        _files.CreateDirectory(directory);

        if (asset.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            UnpackZip(archive, directory, cancellationToken);
            return;
        }

        if (asset.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
            || asset.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            UnpackTar(archive, directory, cancellationToken);
            return;
        }

        throw new ToolInstallException($"{asset} is not a zip or a tar.gz, so it was not installed.");
    }

    private void UnpackZip(string archive, string directory, CancellationToken cancellationToken)
    {
        using var stream = _files.OpenRead(archive);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        Bound(zip.Entries.Sum(entry => entry.Length));

        foreach (var entry in zip.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A directory entry carries no content and is made by the file under it.
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                continue;
            }

            using var source = entry.Open();

            Write(directory, entry.FullName, source, (entry.ExternalAttributes >> 16 & Executable) != 0);
        }
    }

    private void UnpackTar(string archive, string directory, CancellationToken cancellationToken)
    {
        using var file = _files.OpenRead(archive);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);

        var declared = 0L;

        while (tar.GetNextEntry() is { } entry)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Anything that is not a plain file is skipped. A link inside a payload could
            // point anywhere, and nothing published here has needed one.
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile)
                || entry.DataStream is not { } source)
            {
                continue;
            }

            declared += entry.Length;
            Bound(declared);

            Write(directory, entry.Name, source, ((int)entry.Mode & Executable) != 0);
        }
    }

    /// <summary>
    /// Writes one entry, refusing any path that would land outside the directory being
    /// built. An archive comes off the internet and this is what stops it writing anywhere.
    /// </summary>
    private void Write(string directory, string name, Stream source, bool executable)
    {
        var root = Path.GetFullPath(directory);
        var target = Path.GetFullPath(
            Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar)));

        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ToolInstallException(
                $"The payload holds '{name}', which would be written outside the tool's folder.");
        }

        _files.CreateDirectory(Path.GetDirectoryName(target)!);

        using (var into = _files.Create(target))
        {
            source.CopyTo(into);
        }

        if (executable)
        {
            _files.MakeExecutableFile(target);
        }
    }

    private static void Bound(long declared)
    {
        if (declared > MostUnpacked)
        {
            throw new ToolInstallException(
                $"The payload says it unpacks to {declared / 1024 / 1024} MB, "
                + "which is larger than any tool, so it was not installed.");
        }
    }
}
