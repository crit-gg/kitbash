using System.IO.Compression;
using Kitbash.Core.IO;

namespace Kitbash.Core.Godot;

internal sealed class EngineTemplates : IEngineTemplates
{
    /// <summary>Everything a <c>.tpz</c> holds sits under this folder.</summary>
    private const string Prefix = "templates/";

    /// <summary>What Godot reads first, and uses as the folder name. Read from export_template_manager.cpp.</summary>
    private const string VersionFile = "templates/version.txt";

    /// <summary>
    /// The most a templates archive may say it unpacks to. An official set is 1.9 GB, so
    /// this refuses an absurd declaration without bounding a genuine one.
    /// </summary>
    private const long MostUnpacked = 8L * 1024 * 1024 * 1024;

    private readonly IEngineRepositories _repositories;
    private readonly IEngineStore _store;
    private readonly EngineDownloads _downloads;
    private readonly IEngineFiles _engineFiles;
    private readonly IFileSystem _files;

    public EngineTemplates(
        IEngineRepositories repositories,
        IEngineStore store,
        EngineDownloads downloads,
        IEngineFiles engineFiles,
        IFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(downloads);
        ArgumentNullException.ThrowIfNull(engineFiles);
        ArgumentNullException.ThrowIfNull(files);

        _repositories = repositories;
        _store = store;
        _downloads = downloads;
        _engineFiles = engineFiles;
        _files = files;
    }

    public async Task<InstalledEngine> InstallAsync(
        InstalledEngine engine,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var root = _engineFiles.ExportTemplatesDirectory;

        if (!Path.IsPathRooted(root))
        {
            throw new EngineInstallException("There is no home folder to put export templates in.");
        }

        // An official install made before releases were recorded is named by its tag.
        var release = engine.Record.Release.Length > 0
            ? engine.Record.Release
            : engine.Id.IsOfficial ? engine.Tag.ToString() : null;

        if (release is null || engine.IsImported)
        {
            throw new EngineInstallException("Export templates can only be added to an engine Kitbash installed.");
        }

        progress?.Report(new EngineInstallProgress(EngineInstallStage.Queued));

        var manifest = await _repositories
            .For(engine.Repository)
            .ReadManifestAsync(release, cancellationToken)
            .ConfigureAwait(false);

        var file = manifest.TemplatesFor(engine.IsMono)
            ?? throw new EngineInstallException($"{release} published no export templates for this engine.");

        var checksum = manifest.ChecksumFor(file.FileName);

        if (string.IsNullOrWhiteSpace(checksum))
        {
            throw new EngineInstallException(
                $"{release} published no checksum for {file.FileName}, so it was not installed.");
        }

        var archive = await _downloads
            .FetchAsync(
                file.FileName,
                file.Address,
                checksum,
                manifest.ChecksumKind,
                (bytes, total) => progress?.Report(new EngineInstallProgress(EngineInstallStage.Downloading, bytes, total)),
                () => progress?.Report(new EngineInstallProgress(EngineInstallStage.Verifying)),
                cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(new EngineInstallProgress(EngineInstallStage.Extracting));

        var folder = Unpack(archive, root, cancellationToken);

        progress?.Report(new EngineInstallProgress(EngineInstallStage.Done));

        return _store.RecordTemplates(engine, folder);
    }

    public string? OwnedBy(InstalledEngine engine, IReadOnlyList<InstalledEngine> installed)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(installed);

        var folder = engine.Record.Templates;

        if (folder.Length == 0)
        {
            return null;
        }

        // Every build of one version reads the same folder, so another install of it still
        // uses the templates whether or not Kitbash put them there for that one.
        var shared = installed.Any(other =>
            other.Directory != engine.Directory
            && !other.IsMissing
            && EngineBuildString.TryParse(other.Record.BuildString, out var build)
            && string.Equals(build.ExportTemplateFolder, folder, StringComparison.Ordinal));

        return shared ? null : folder;
    }

    public long SizeOf(string folder)
    {
        var path = PathFor(folder);

        return path is not null && _files.DirectoryExists(path)
            ? _files.EnumerateFiles(path, recursive: true).Sum(_files.GetFileLength)
            : 0;
    }

    public void Remove(string folder)
    {
        if (PathFor(folder) is { } path)
        {
            _files.DeleteDirectory(path);
        }
    }

    /// <summary>
    /// A folder name read from a record, refused when it could name anything other than one
    /// folder directly under the templates directory.
    /// </summary>
    private string? PathFor(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)
            || folder is "." or ".."
            || folder.IndexOfAny(['/', '\\']) >= 0)
        {
            return null;
        }

        var root = _engineFiles.ExportTemplatesDirectory;

        return Path.IsPathRooted(root) ? Path.Combine(root, folder) : null;
    }

    /// <summary>
    /// Unpacks the way Godot's own template manager does: the folder is named by
    /// <c>version.txt</c>, and the contents of <c>templates/</c> are flattened into it.
    /// </summary>
    private string Unpack(string archive, string root, CancellationToken cancellationToken)
    {
        using var stream = _files.OpenRead(archive);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        var version = zip.GetEntry(VersionFile)
            ?? throw new EngineInstallException($"{Path.GetFileName(archive)} holds no version.txt, so it is not export templates.");

        string folder;

        using (var reader = new StreamReader(version.Open()))
        {
            folder = reader.ReadToEnd().Trim();
        }

        if (folder.Length == 0 || folder is "." or ".." || folder.IndexOfAny(['/', '\\']) >= 0
            || folder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new EngineInstallException($"{Path.GetFileName(archive)} names a version that is not a folder.");
        }

        if (zip.Entries.Sum(entry => entry.Length) > MostUnpacked)
        {
            throw new EngineInstallException(
                $"{Path.GetFileName(archive)} says it unpacks to more than any export templates, so it was not unpacked.");
        }

        var target = Path.GetFullPath(Path.Combine(root, folder));

        _files.DeleteDirectory(target);
        _files.CreateDirectory(target);

        try
        {
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!entry.FullName.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var name = entry.FullName[Prefix.Length..];

                if (name.Length == 0 || name.EndsWith('/'))
                {
                    continue;
                }

                var path = Path.GetFullPath(Path.Combine(target, name.Replace('/', Path.DirectorySeparatorChar)));

                if (!path.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    throw new EngineInstallException(
                        $"{Path.GetFileName(archive)} holds an entry that would write outside its folder.");
                }

                _files.CreateDirectory(Path.GetDirectoryName(path)!);

                using var source = entry.Open();
                using var into = _files.Create(path);

                source.CopyTo(into);
            }
        }
        catch (Exception error) when (error is InvalidDataException or IOException
                                          or OperationCanceledException or EngineInstallException)
        {
            // Half a set of templates is worse than none, since Godot would find the folder.
            _files.DeleteDirectory(target);

            if (error is OperationCanceledException or EngineInstallException)
            {
                throw;
            }

            throw new EngineInstallException($"{Path.GetFileName(archive)} could not be unpacked.", error);
        }

        return folder;
    }
}
