using System.IO.Compression;
using System.Security.Cryptography;
using Workbench.Core.IO;
using Workbench.Core.Platform;
using Workbench.Core.Settings;

namespace Workbench.Core.Godot;

internal sealed class EngineInstaller : IEngineInstaller, IDisposable
{
    private const string DoneSuffix = ".done";
    private const string PartSuffix = ".part";

    /// <summary>
    /// How many run at once. A judgment rather than a measurement: enough that a person
    /// clicking down a card is not waiting on one at a time, few enough that twenty
    /// downloads do not each crawl. It lives here and nowhere else.
    /// </summary>
    private const int AtOnce = 3;

    /// <summary>The three execute bits of a Unix mode, owner, group and other.</summary>
    private const int Executable = 0b001_001_001;

    /// <summary>
    /// The most an editor archive may say it unpacks to. Measured across the archives in
    /// the sweep, the largest tree is about 220 MB, so this is roughly ten times the real
    /// answer and still small enough to refuse something absurd.
    /// </summary>
    private const long MostUnpacked = 2L * 1024 * 1024 * 1024;

    private readonly IEngineCatalogue _catalogue;
    private readonly IEngineStore _store;
    private readonly IEngineFiles _engineFiles;
    private readonly IWebContent _web;
    private readonly IFileSystem _files;
    private readonly IGodotSettings _settings;
    private readonly ApplicationPaths _paths;
    private readonly SemaphoreSlim _slots = new(AtOnce, AtOnce);

    public EngineInstaller(
        IEngineCatalogue catalogue,
        IEngineStore store,
        IEngineFiles engineFiles,
        IWebContent web,
        IFileSystem files,
        IGodotSettings settings,
        ApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(engineFiles);
        ArgumentNullException.ThrowIfNull(web);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(paths);

        _catalogue = catalogue;
        _store = store;
        _engineFiles = engineFiles;
        _web = web;
        _files = files;
        _settings = settings;
        _paths = paths;
    }

    public async Task<InstalledEngine> InstallAsync(
        EngineBuild build,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);

        progress?.Report(new EngineInstallProgress(EngineInstallStage.Queued));

        await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await RunAsync(build, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _slots.Release();
        }
    }

    public void Dispose() => _slots.Dispose();

    private async Task<InstalledEngine> RunAsync(
        EngineBuild build,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var manifest = await _catalogue.ReadManifestAsync(build.Tag, cancellationToken).ConfigureAwait(false);
        var checksum = manifest.ChecksumFor(build);

        // GodotEnv's posture, and the right one. A missing checksum is a refusal rather
        // than a shrug, since the whole point is that nothing unverified is unpacked.
        if (string.IsNullOrWhiteSpace(checksum))
        {
            throw new EngineInstallException(
                $"Godot {build.Tag} published no checksum for {build.FileName}, so it was not installed.");
        }

        var archive = await FetchAsync(build, checksum, progress, cancellationToken).ConfigureAwait(false);
        var directory = Path.Combine(_settings.EngineDirectory, build.Id.DirectoryName);

        progress?.Report(new EngineInstallProgress(EngineInstallStage.Extracting));

        // One install per version, so an install of this id already here is replaced.
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
                .RegisterAsync(directory, build, checksum, cancellationToken)
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
    /// The archive, from the cache when it is there and complete, otherwise fetched and
    /// verified.
    /// </summary>
    /// <remarks>
    /// **A marker file beside the archive is what makes it complete**, not the archive
    /// being present. A half written one from a killed process would otherwise look
    /// cached. The download goes to a part file and is only named as the archive after it
    /// verifies, so the two guards agree.
    /// </remarks>
    private async Task<string> FetchAsync(
        EngineBuild build,
        string checksum,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var archive = _paths.CacheFileFor(build.FileName);
        var done = archive + DoneSuffix;

        if (_files.FileExists(archive) && _files.FileExists(done))
        {
            return archive;
        }

        var part = archive + PartSuffix;

        _files.CreateDirectory(_paths.Cache);
        _files.DeleteFile(part);

        var total = await _web.MeasureAsync(build.Address, cancellationToken).ConfigureAwait(false) ?? 0;

        progress?.Report(new EngineInstallProgress(EngineInstallStage.Downloading, 0, total));

        try
        {
            await _web
                .DownloadAsync(
                    build.Address,
                    part,
                    new Progress<long>(bytes =>
                        progress?.Report(new EngineInstallProgress(EngineInstallStage.Downloading, bytes, total))),
                    cancellationToken)
                .ConfigureAwait(false);

            progress?.Report(new EngineInstallProgress(EngineInstallStage.Verifying));

            var actual = await HashAsync(part, cancellationToken).ConfigureAwait(false);

            if (!string.Equals(actual, checksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new EngineInstallException(
                    $"{build.FileName} did not match the checksum Godot published, so it was not installed.");
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

    private async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = _files.OpenRead(path);

        var hash = await SHA512.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);

        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Unpacks with the two guards and the prefix strip.
    /// </summary>
    /// <remarks>
    /// <para>
    /// **The prefix strip has an exception.** A single top level directory is stripped so
    /// every install comes out the same shape, which is gdvm's idea and a good one, but a
    /// name ending in <c>.app</c> is a macOS bundle and lifting its contents to the root
    /// destroys it. Measured: Linux standard is one loose file, Windows standard is two,
    /// both .NET archives wrap, and both macOS archives are a bundle.
    /// </para>
    /// <para>
    /// **Two guards, and not the two gdvm has.** An entry whose path escapes the target
    /// after the strip is refused, which is the same one. The second is a ceiling on what
    /// the archive says it will unpack to, rather than gdvm's check that an entry produces
    /// more bytes than it declared.
    /// </para>
    /// <para>
    /// That second one was written first and it can never fire here. Measured: given an
    /// entry declaring 16 bytes over a megabyte of real content, <c>ZipArchive</c> hands
    /// back 16 bytes and stops, so the runtime already enforces the declared length and a
    /// lying header is neutralised before this sees it. What the runtime does not bound is
    /// an honest header declaring something enormous, so that is what is bounded here.
    /// </para>
    /// </remarks>
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

            // **Unpacking by hand does not carry the mode across.** ZipFile.ExtractToDirectory
            // does, which is what the research measured, but the prefix strip and the two
            // guards mean entries are copied one at a time here. So the recorded mode is
            // read back and the executable bit put on, or a Linux install comes out with a
            // 0644 editor that nothing can find or run. Measured: without this the install
            // succeeded and held no editor.
            //
            // A zip made on Windows records DOS attributes instead, which read as no mode
            // at all, so nothing is marked and the guard costs nothing.
            if ((entry.ExternalAttributes >> 16 & Executable) != 0)
            {
                _engineFiles.MakeExecutable(target);
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

            // Belt and braces. ZipArchive stops an entry at its declared length on its own,
            // measured, so this is here for the day that changes rather than for today.
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

            // A relative segment is not a wrapper folder, whatever it looks like. Taking
            // one off would quietly turn an archive that tries to escape into a well
            // behaved one, which reads as the guard working when it never ran. Measured:
            // an entry named ../escaped.txt was stripped to escaped.txt and unpacked
            // happily, and the path check below never saw it.
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
