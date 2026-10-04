using Kitbash.Core.IO;

namespace Kitbash.Core.Platform;

/// <summary>
/// The per user bin folder on Linux and macOS, where a command is a symbolic link to the
/// program. Whether PATH reaches the folder is asked of the person's own login shell.
/// </summary>
internal sealed class UnixCommandFolder : ICommandFolder
{
    // An interactive shell can run anything a person put in their profile, so it is not
    // waited on for long.
    private static readonly TimeSpan ShellWait = TimeSpan.FromSeconds(5);

    // Marks the line holding PATH, since a profile can print anything else around it.
    private const string Marker = "KITBASH_PATH=";

    // path_helper builds a macOS login PATH from these. Linux has neither.
    private const string SystemPaths = "/etc/paths";
    private const string SystemPathsDirectory = "/etc/paths.d";

    private readonly IFileSystem _files;
    private readonly IEnvironment _environment;
    private readonly IBundleEnvironment _bundle;
    private readonly IProcessRunner _processes;
    private readonly IPathRules _paths;
    private readonly IPathRequest _request;

    public UnixCommandFolder(
        IFileSystem files,
        IEnvironment environment,
        IBundleEnvironment bundle,
        IProcessRunner processes,
        IPathRules paths,
        IPathRequest request)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(request);

        _files = files;
        _environment = environment;
        _bundle = bundle;
        _processes = processes;
        _paths = paths;
        _request = request;
    }

    // XDG_BIN_HOME is not in the base directory spec, which names ~/.local/bin, but uv and
    // pipx both honour it. A relative value is ignored, as the spec says of its own.
    public string? Directory
    {
        get
        {
            var configured = _environment.GetVariable("XDG_BIN_HOME");

            if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured))
            {
                return configured;
            }

            var home = _environment.GetHomeDirectory();

            return string.IsNullOrWhiteSpace(home) || !Path.IsPathRooted(home)
                ? null
                : Path.Combine(home, ".local", "bin");
        }
    }

    public bool CanWrite => Directory is not null;

    public string? PathOf(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Directory is { } directory ? Path.Combine(directory, name) : null;
    }

    public CommandEntry Read(string name)
    {
        if (PathOf(name) is not { } path)
        {
            return CommandEntry.Absent;
        }

        if (_files.ReadLinkTarget(path) is { } target)
        {
            return new CommandEntry(true, Path.GetFullPath(target, Path.GetDirectoryName(path)!));
        }

        return _files.FileExists(path) || _files.DirectoryExists(path)
            ? new CommandEntry(true, null)
            : CommandEntry.Absent;
    }

    public void Write(string name, string program)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(program);

        var path = PathOf(name) ?? throw new InvalidOperationException("There is no folder to write a command into.");
        var directory = Path.GetDirectoryName(path)!;
        var temporary = Path.Combine(directory, "." + name + ".kitbash");

        _files.CreateDirectory(directory);
        _files.DeleteFile(temporary);
        _files.CreateSymbolicLink(temporary, program);

        // A rename replaces the old link in one step, so a terminal never finds no command.
        _files.MoveFile(temporary, path, overwrite: true);
    }

    public void Remove(string name)
    {
        if (PathOf(name) is { } path)
        {
            _files.DeleteFile(path);
        }
    }

    public async Task<CommandReach> ReachAsync(string name, bool mayAsk, CancellationToken cancellation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (Directory is not { } directory)
        {
            return CommandReach.Unknown;
        }

        var search = await SearchPathAsync(cancellation).ConfigureAwait(false);

        var reach = search.Any(entry => _paths.AreSame(entry, directory))
            ? PathReach.OnPath
            : mayAsk
                ? await _request.AskAsync(directory, cancellation).ConfigureAwait(false)
                : PathReach.NotOnPath;

        // path_helper puts a paths.d entry after the system folders, so that is where the
        // folder lands for a terminal opened from now on.
        if (reach == PathReach.Added)
        {
            search = [.. search, directory];
        }

        return new CommandReach(reach, Shadow(search, directory, name));
    }

    // Linux edits no PATH, and a paths.d entry on macOS may serve other commands in the
    // folder, so there is nothing here to take back.
    public void Unreach()
    {
    }

    public void Forget(string name)
    {
        try
        {
            Remove(name);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing runs an uninstaller here, and a link left behind names its own target.
        }
    }

    private string? Shadow(IReadOnlyList<string> search, string directory, string name)
    {
        foreach (var entry in search)
        {
            if (_paths.AreSame(entry, directory))
            {
                return null;
            }

            var candidate = Path.Combine(entry, name);

            if (_files.IsExecutableFile(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// The PATH a terminal opened now would have. The login shell's when it answers, since a
    /// desktop session often starts the app with less than a profile sets.
    /// </summary>
    private async Task<IReadOnlyList<string>> SearchPathAsync(CancellationToken cancellation)
    {
        if (await LoginShellPathAsync(cancellation).ConfigureAwait(false) is { } shell)
        {
            return shell;
        }

        var inherited = _bundle.Outside.TryGetValue("PATH", out var corrected)
            ? corrected
            : _environment.GetVariable("PATH");

        return [.. Rooted(inherited), .. SystemPathEntries()];
    }

    private async Task<IReadOnlyList<string>?> LoginShellPathAsync(CancellationToken cancellation)
    {
        var shell = _environment.GetVariable("SHELL");

        if (string.IsNullOrWhiteSpace(shell) || !Path.IsPathRooted(shell) || !_files.IsExecutableFile(shell))
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(ShellWait);

        // Interactive as well as login, since zsh reads .zshrc only then and PATH is often set
        // there. Fish joins a quoted path variable with colons, so the line reads the same in
        // fish. Input is closed so a profile that reads it cannot wait.
        var request = ProcessRequest.Command(shell, "-l", "-i", "-c", "printf '\\n" + Marker + "%s\\n' \"$PATH\"")
            with { StandardInput = string.Empty };

        try
        {
            var output = await _processes.ReadAsync(request, timeout.Token).ConfigureAwait(false);
            var line = output.Lines.LastOrDefault(line => line.StartsWith(Marker, StringComparison.Ordinal));

            if (line is null)
            {
                return null;
            }

            var entries = Rooted(line[Marker.Length..]);

            return entries.Count == 0 ? null : entries;
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            return null;
        }
        catch (ProcessStartException)
        {
            return null;
        }
    }

    private IEnumerable<string> SystemPathEntries()
    {
        var files = _files.FileExists(SystemPaths) ? [SystemPaths] : Array.Empty<string>();

        foreach (var file in files.Concat(_files.EnumerateFiles(SystemPathsDirectory, recursive: false)))
        {
            string text;

            try
            {
                text = _files.ReadAllText(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var entry in text.Split('\n').Select(line => line.Trim()).Where(Path.IsPathRooted))
            {
                yield return entry;
            }
        }
    }

    // A relative entry means the working folder, which says nothing about a terminal.
    private static IReadOnlyList<string> Rooted(string? value) =>
        [.. PathVariable.Parse(value, ':').Entries.Where(Path.IsPathRooted)];
}
