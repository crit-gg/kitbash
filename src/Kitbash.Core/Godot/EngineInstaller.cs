using System.IO.Compression;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;

namespace Kitbash.Core.Godot;

internal sealed class EngineInstaller : IEngineInstaller, IDisposable
{
    /// <summary>
    /// How many run at once. A judgment rather than a measurement: enough that a person
    /// clicking down a card is not waiting on one at a time, few enough that twenty
    /// downloads do not each crawl. It lives here and nowhere else.
    /// </summary>
    private const int AtOnce = 3;

    /// <summary>The three execute bits of a Unix mode, owner, group and other.</summary>
    private const int Executable = 0b001_001_001;

    /// <summary>
    /// The most an editor archive may say it unpacks to. Real archives are around 220 MB,
    /// so this refuses an absurd declaration without bounding a genuine one.
    /// </summary>
    private const long MostUnpacked = 2L * 1024 * 1024 * 1024;

    private readonly IEngineRepositories _repositories;
    private readonly IEngineStore _store;
    private readonly EngineDownloads _downloads;
    private readonly IFileSystem _files;
    private readonly IGodotSettings _settings;
    private readonly SemaphoreSlim _slots = new(AtOnce, AtOnce);

    public EngineInstaller(
        IEngineRepositories repositories,
        IEngineStore store,
        EngineDownloads downloads,
        IFileSystem files,
        IGodotSettings settings)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(downloads);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(settings);

        _repositories = repositories;
        _store = store;
        _downloads = downloads;
        _files = files;
        _settings = settings;
    }

    private EngineLayout Layout => new(_settings.EngineDirectory);

    public Task<InstalledEngine> InstallAsync(
        EngineBuild build,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);

        return QueueAsync(build, Layout.DirectoryFor(build), string.Empty, progress, cancellationToken);
    }

    public Task<InstalledEngine> StageAsync(
        EngineBuild build,
        string slot,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);

        var address = build.Id.Repository
            ?? throw new ArgumentException("Only a repository build has a slot.", nameof(build));

        return QueueAsync(build, Layout.StagingDirectory(address, slot), slot, progress, cancellationToken);
    }

    public InstalledEngine? Staged(EngineRepositoryAddress address, string slot)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);

        return _store.ReadAt(Layout.StagingDirectory(address, slot));
    }

    public InstalledEngine? Place(EngineRepositoryAddress address, string slot)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);

        var layout = Layout;
        var staged = layout.StagingDirectory(address, slot);

        if (_store.ReadAt(staged) is null)
        {
            return null;
        }

        var target = layout.SlotDirectory(address, slot);
        var retired = layout.RetiredDirectory(address, slot);

        _files.DeleteDirectory(retired);

        // Two renames, so a failure part way leaves one whole build in the slot. A rename
        // of a folder whose editor is running fails on Windows, which is what the caller
        // is told and why nothing is deleted before both have happened.
        if (_files.DirectoryExists(target))
        {
            _files.CreateDirectory(Path.GetDirectoryName(retired)!);
            _files.MoveDirectory(target, retired);
        }

        try
        {
            _files.CreateDirectory(Path.GetDirectoryName(target)!);
            _files.MoveDirectory(staged, target);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            if (_files.DirectoryExists(retired) && !_files.DirectoryExists(target))
            {
                _files.MoveDirectory(retired, target);
            }

            throw;
        }

        try
        {
            _files.DeleteDirectory(retired);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The next placing clears it. The slot already holds the new build.
        }

        return _store.ReadAt(target);
    }

    public void Dispose() => _slots.Dispose();

    private async Task<InstalledEngine> QueueAsync(
        EngineBuild build,
        string directory,
        string slot,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new EngineInstallProgress(EngineInstallStage.Queued));

        await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await RunAsync(build, directory, slot, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _slots.Release();
        }
    }

    private async Task<InstalledEngine> RunAsync(
        EngineBuild build,
        string directory,
        string slot,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var manifest = await _repositories
            .For(build.Id.Repository)
            .ReadManifestAsync(build.Release, cancellationToken)
            .ConfigureAwait(false);

        var checksum = manifest.ChecksumFor(build);

        // GodotEnv's posture, and the right one. A missing checksum is a refusal rather
        // than a shrug, since the whole point is that nothing unverified is unpacked.
        if (string.IsNullOrWhiteSpace(checksum))
        {
            throw new EngineInstallException(
                $"{build.Release} published no checksum for {build.FileName}, so it was not installed.");
        }

        var archive = await _downloads
            .FetchAsync(
                build.FileName,
                build.Address,
                checksum,
                manifest.ChecksumKind,
                (bytes, total) => progress?.Report(new EngineInstallProgress(EngineInstallStage.Downloading, bytes, total)),
                () => progress?.Report(new EngineInstallProgress(EngineInstallStage.Verifying)),
                cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(new EngineInstallProgress(EngineInstallStage.Extracting));

        // One install per folder, so whatever is already in this one is replaced.
        _files.DeleteDirectory(directory);
        _files.CreateDirectory(directory);

        try
        {
            Unpack(archive, directory, cancellationToken);
        }
        catch (Exception error) when (error is InvalidDataException or IOException)
        {
            _files.DeleteDirectory(directory);

            throw new EngineInstallException($"{build.FileName} could not be unpacked.", error);
        }
        catch (OperationCanceledException)
        {
            _files.DeleteDirectory(directory);

            throw;
        }

        progress?.Report(new EngineInstallProgress(EngineInstallStage.Registering));

        try
        {
            var engine = await _store
                .RegisterAsync(directory, build, checksum, manifest.ChecksumKind, slot, cancellationToken)
                .ConfigureAwait(false);

            progress?.Report(new EngineInstallProgress(EngineInstallStage.Done));

            return engine;
        }
        catch (EngineStoreException error)
        {
            _files.DeleteDirectory(directory);

            throw new EngineInstallException($"{build.FileName} did not contain a Godot editor.", error);
        }
    }

    /// <summary>
    /// Unpacks with the two guards and the prefix strip.
    /// </summary>
    private void Unpack(string archive, string directory, CancellationToken cancellationToken)
    {
        using var stream = _files.OpenRead(archive);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        var strip = CommonPrefix(zip);
        var root = Path.GetFullPath(directory);
        var declared = zip.Entries.Sum(entry => entry.Length);

        if (declared > MostUnpacked)
        {
            throw new EngineInstallException(
                $"{Path.GetFileName(archive)} says it unpacks to "
                + $"{declared / 1024 / 1024} MB, which is larger than any Godot editor, "
                + "so it was not unpacked.");
        }

        foreach (var entry in zip.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = entry.FullName;

            if (strip is not null)
            {
                name = name[strip.Length..];
            }

            // A directory entry, which carries no content and is made by the file under it.
            if (name.Length == 0 || name.EndsWith('/') || name.EndsWith('\\'))
            {
                continue;
            }

            var target = Path.GetFullPath(Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar)));

            if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new EngineInstallException(
                    $"{Path.GetFileName(archive)} holds an entry that would write outside the install directory.");
            }

            _files.CreateDirectory(Path.GetDirectoryName(target)!);

            using (var source = entry.Open())
            using (var into = _files.Create(target))
            {
                Copy(source, into, entry.Length, Path.GetFileName(archive), cancellationToken);
            }

            // Copying an entry by hand does not carry its Unix mode, so the executable
            // bit is put back here. Without it a Linux install has a 0644 editor that
            // cannot be run. A zip made on Windows records DOS attributes, which read as
            // no mode, so nothing is marked.
            if ((entry.ExternalAttributes >> 16 & Executable) != 0)
            {
                _files.MakeExecutableFile(target);
            }
        }
    }

    private static void Copy(
        Stream source,
        Stream target,
        long declared,
        string archive,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        var written = 0L;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var read = source.Read(buffer, 0, buffer.Length);

            if (read == 0)
            {
                return;
            }

            written += read;

            // ZipArchive already stops an entry at its declared length. This guards the
            // day that changes.
            if (written > declared)
            {
                throw new EngineInstallException(
                    $"{archive} holds an entry larger than it declares, so it was not unpacked.");
            }

            target.Write(buffer, 0, read);
        }
    }

    /// <summary>
    /// The one top level directory every entry sits under, or null when there is not one.
    /// </summary>
    private static string? CommonPrefix(ZipArchive zip)
    {
        string? prefix = null;

        foreach (var entry in zip.Entries)
        {
            var slash = entry.FullName.IndexOf('/', StringComparison.Ordinal);

            // Anything at the top level means there is no common directory to take off,
            // which is the standard Linux archive and the two file Windows one.
            if (slash <= 0)
            {
                return null;
            }

            var head = entry.FullName[..(slash + 1)];

            // A relative segment is not a wrapper folder. Stripping one would turn an
            // escaping entry into a harmless looking name and the path check below would
            // never see it.
            if (head is "./" or "../")
            {
                return null;
            }

            if (prefix is null)
            {
                prefix = head;
            }
            else if (!string.Equals(prefix, head, StringComparison.Ordinal))
            {
                return null;
            }
        }

        // A bundle is the shape, not a wrapper around it.
        return prefix is not null
               && prefix.TrimEnd('/').EndsWith(".app", StringComparison.OrdinalIgnoreCase)
            ? null
            : prefix;
    }
}
