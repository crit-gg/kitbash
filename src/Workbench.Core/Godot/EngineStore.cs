using System.Globalization;
using Workbench.Core.IO;
using Workbench.Core.Platform;
using Workbench.Core.Settings;

namespace Workbench.Core.Godot;

internal sealed class EngineStore : IEngineStore
{
    private const string ImportedKey = "godot.engines.imported";

    private readonly IGodotSettings _settings;
    private readonly IApplicationState _state;
    private readonly IFileSystem _files;
    private readonly IEngineFiles _engineFiles;
    private readonly IProcessRunner _processes;
    private readonly ISettingsDocumentStore _documents;
    private readonly IPathRules _paths;
    private readonly TimeProvider _time;

    public EngineStore(
        IGodotSettings settings,
        IApplicationState state,
        IFileSystem files,
        IEngineFiles engineFiles,
        IProcessRunner processes,
        ISettingsDocumentStore documents,
        IPathRules paths,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(engineFiles);
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(time);

        _settings = settings;
        _state = state;
        _files = files;
        _engineFiles = engineFiles;
        _processes = processes;
        _documents = documents;
        _paths = paths;
        _time = time;
    }

    public async Task<IReadOnlyList<InstalledEngine>> ReadAsync(CancellationToken cancellationToken)
    {
        var found = new List<InstalledEngine>();
        var root = _settings.EngineDirectory;

        foreach (var directory in _files.EnumerateDirectories(root))
        {
            var engine = await ReadInstalledAsync(directory, cancellationToken).ConfigureAwait(false);

            if (engine is not null)
            {
                found.Add(engine);
            }
        }

        foreach (var directory in ReadImported())
        {
            // Already listed above if it now sits under the engine directory.
            if (found.Any(e => _paths.AreSame(e.Directory, directory)))
            {
                continue;
            }

            found.Add(await ReadImportedAsync(directory, cancellationToken).ConfigureAwait(false));
        }

        found.Sort(Order);

        return found;
    }

    public async Task<InstalledEngine?> ImportAsync(string directory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var full = Path.GetFullPath(directory);
        var probed = await ProbeAsync(full, cancellationToken).ConfigureAwait(false);

        if (probed is null)
        {
            return null;
        }

        var known = ReadImported();

        if (!known.Any(path => _paths.AreSame(path, full)))
        {
            _state.Set(SettingsScope.Global, ImportedKey, known.Append(full).ToArray());
        }

        return Describe(probed, full, imported: true);
    }

    public Task RemoveAsync(InstalledEngine engine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var known = ReadImported();
        var kept = known.Where(path => !_paths.AreSame(path, engine.Directory)).ToArray();

        if (kept.Length != known.Count)
        {
            _state.Set(SettingsScope.Global, ImportedKey, kept);
        }

        // An imported engine is somebody else's folder, so removing it only forgets it.
        if (!engine.IsImported)
        {
            _files.DeleteDirectory(engine.Directory);
        }

        return Task.CompletedTask;
    }

    public async Task<InstalledEngine> RegisterAsync(
        string directory,
        EngineBuild build,
        string checksum,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(build);

        var editor = _engineFiles.FindEditor(directory)
            ?? throw new EngineStoreException($"No Godot editor was found in {directory}.");

        _files.MakeExecutableFile(editor);

        var version = await AskVersionAsync(editor, cancellationToken).ConfigureAwait(false)
            ?? throw new EngineStoreException($"{editor} did not report a Godot version.");

        var record = new EngineRecord(
            build.Id,
            build.Platform,
            build.Architecture,
            version,
            Path.GetRelativePath(directory, editor),
            build.FileName,
            checksum ?? string.Empty,
            _time.GetUtcNow());

        Write(directory, record);

        return Describe(record, directory, imported: false);
    }

    /// <summary>
    /// An install Workbench made. Its record is read rather than probed, and a record that
    /// is missing, unreadable or pointing at nothing sends this back to probing and gets
    /// written again.
    /// </summary>
    private async Task<InstalledEngine?> ReadInstalledAsync(string directory, CancellationToken cancellationToken)
    {
        var record = Read(directory);

        if (record is not null && _files.FileExists(Path.Combine(directory, record.Executable)))
        {
            return Describe(record, directory, imported: false);
        }

        var probed = await ProbeAsync(directory, cancellationToken).ConfigureAwait(false);

        if (probed is null)
        {
            return null;
        }

        // What was lost with the record is lost. The source archive and its checksum are
        // not recoverable from a tree on disk, so they come back empty rather than invented.
        Write(directory, probed);

        return Describe(probed, directory, imported: false);
    }

    /// <summary>
    /// An engine a person pointed at. No record is written for it, so it is probed on
    /// every refresh. Runs the editor, so call it off the UI thread.
    /// </summary>
    private async Task<InstalledEngine> ReadImportedAsync(string directory, CancellationToken cancellationToken)
    {
        var probed = await ProbeAsync(directory, cancellationToken).ConfigureAwait(false);

        if (probed is not null)
        {
            return Describe(probed, directory, imported: true);
        }

        // A folder that has gone, or no longer holds an engine. Kept in the list so that
        // removing it stays an explicit act.
        var name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar));
        var gone = new EngineRecord(
            default,
            _engineFiles.Platform,
            _engineFiles.Architecture,
            name,
            string.Empty,
            string.Empty,
            string.Empty,
            default);

        return new InstalledEngine(gone, directory, string.Empty, 0, IsMissing: true, IsImported: true);
    }

    /// <summary>
    /// Finds the editor and asks it what it is. The processor is not in the answer, so the
    /// host's is assumed, which is right for anything that can run here.
    /// </summary>
    private async Task<EngineRecord?> ProbeAsync(string directory, CancellationToken cancellationToken)
    {
        var editor = _engineFiles.FindEditor(directory);

        if (editor is null)
        {
            return null;
        }

        var printed = await AskVersionAsync(editor, cancellationToken).ConfigureAwait(false);

        if (printed is null
            || !EngineBuildString.TryParse(printed, out var build)
            || !build.TryGetId(out var id))
        {
            return null;
        }

        return new EngineRecord(
            id,
            _engineFiles.Platform,
            _engineFiles.Architecture,
            printed,
            Path.GetRelativePath(directory, editor),
            string.Empty,
            string.Empty,
            _files.GetLastWriteTime(editor) ?? _time.GetUtcNow());
    }

    private async Task<string?> AskVersionAsync(string editor, CancellationToken cancellationToken)
    {
        try
        {
            var output = await _processes
                .ReadAsync(ProcessRequest.Command(editor, "--version"), cancellationToken)
                .ConfigureAwait(false);

            var printed = output.StandardOutput.Trim();

            return output.Succeeded && printed.Length > 0 ? printed : null;
        }
        catch (ProcessStartException)
        {
            // A file that will not start is not an engine, which is an answer rather than
            // a failure. A folder full of anything else reads as no engine at all.
            return null;
        }
    }

    private InstalledEngine Describe(EngineRecord record, string directory, bool imported)
    {
        var editor = Path.Combine(directory, record.Executable);
        var missing = !_files.DirectoryExists(directory);

        var size = missing
            ? 0
            : _files.EnumerateFiles(directory, recursive: true).Sum(_files.GetFileLength);

        return new InstalledEngine(record, directory, editor, size, missing, imported);
    }

    private List<string> ReadImported() =>
    [
        .. _state.Global
            .Get(ImportedKey, Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path)),
    ];

    private EngineRecord? Read(string directory)
    {
        var path = Path.Combine(directory, EngineRecord.FileName);

        if (!_files.FileExists(path))
        {
            return null;
        }

        try
        {
            var document = _documents.Read(path);

            if (!TryText(document, "engine.id", out var id)
                || !EngineId.TryParse(id, out var parsed)
                || !TryText(document, "engine.executable", out var executable))
            {
                return null;
            }

            return new EngineRecord(
                parsed,
                TryText(document, "engine.platform", out var platform)
                    && Enum.TryParse<EnginePlatform>(platform, out var onPlatform)
                        ? onPlatform
                        : _engineFiles.Platform,
                TryText(document, "engine.architecture", out var architecture)
                    && EngineBuild.TryReadArchitecture(architecture, out var onProcessor)
                        ? onProcessor
                        : _engineFiles.Architecture,
                TryText(document, "engine.build", out var build) ? build : string.Empty,
                executable,
                TryText(document, "engine.source", out var source) ? source : string.Empty,
                TryText(document, "engine.checksum", out var checksum) ? checksum : string.Empty,
                TryText(document, "engine.installed", out var installed)
                    && DateTimeOffset.TryParse(installed, CultureInfo.InvariantCulture, out var at)
                        ? at
                        : default);
        }
        catch (Exception error) when (error is SettingsFileUnreadableException or IOException)
        {
            // Unreadable counts as absent. The caller probes and writes it again.
            return null;
        }
    }

    private void Write(string directory, EngineRecord record)
    {
        var path = Path.Combine(directory, EngineRecord.FileName);
        var document = new SettingsDocument();

        document.SetValue("engine.id", record.Id.ToString());
        document.SetValue("engine.platform", record.Platform.ToString());
        document.SetValue("engine.architecture", EngineBuild.TextFor(record.Architecture));
        document.SetValue("engine.build", record.BuildString);
        document.SetValue("engine.executable", record.Executable);
        document.SetValue("engine.source", record.SourceFileName);
        document.SetValue("engine.checksum", record.Checksum);
        document.SetValue("engine.installed", record.InstalledAt.ToString("O", CultureInfo.InvariantCulture));

        try
        {
            _documents.Write(path, document);
            _engineFiles.Hide(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The record is an optimisation. A folder that will not take one costs a probe
            // per refresh and nothing else.
        }
    }

    private static bool TryText(SettingsDocument document, string key, out string value)
    {
        if (document.TryGetValue(key, out var raw) && raw is string text)
        {
            value = text;

            return true;
        }

        value = string.Empty;

        return false;
    }

    private static int Order(InstalledEngine left, InstalledEngine right)
    {
        var by = right.Tag.CompareTo(left.Tag);

        return by != 0 ? by : left.Id.CompareTo(right.Id);
    }
}
