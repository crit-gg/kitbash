namespace Kitbash.Core.IO;

/// <summary>The real watcher.</summary>
public sealed class DirectoryWatcher : IDirectoryWatcher
{
    /// <summary>
    /// Room for a burst before notifications start being dropped. The default is 8 KB, and
    /// this is the one knob that trades a little unpaged memory for not losing events when
    /// a program writes a folder all at once.
    /// </summary>
    private const int BufferBytes = 32 * 1024;

    private readonly object _gate = new();

    private List<FileSystemWatcher> _watchers = [];

    public event EventHandler<DirectoryChangedEventArgs>? Changed;

    public IReadOnlyList<string> Watching { get; private set; } = [];

    public void Watch(IReadOnlyList<string> directories)
    {
        ArgumentNullException.ThrowIfNull(directories);

        Stop();

        var started = new List<FileSystemWatcher>(directories.Count);
        var watching = new List<string>(directories.Count);

        foreach (var directory in directories)
        {
            if (Start(directory) is not { } watcher)
            {
                continue;
            }

            started.Add(watcher);
            watching.Add(directory);
        }

        lock (_gate)
        {
            _watchers = started;
            Watching = watching;
        }
    }

    public void Stop()
    {
        List<FileSystemWatcher> watchers;

        lock (_gate)
        {
            watchers = _watchers;
            _watchers = [];
            Watching = [];
        }

        foreach (var watcher in watchers)
        {
            watcher.Changed -= Raise;
            watcher.Created -= Raise;
            watcher.Deleted -= Raise;
            watcher.Renamed -= Raise;
            watcher.Error -= OnError;

            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
                // Already gone. Nothing to undo.
            }
        }
    }

    public void Dispose() => Stop();

    private FileSystemWatcher? Start(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        try
        {
            var watcher = new FileSystemWatcher(directory)
            {
                IncludeSubdirectories = false,
                InternalBufferSize = BufferBytes,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };

            watcher.Changed += Raise;
            watcher.Created += Raise;
            watcher.Deleted += Raise;
            watcher.Renamed += Raise;
            watcher.Error += OnError;

            watcher.EnableRaisingEvents = true;

            return watcher;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Out of watches, no permission, or a filesystem that cannot do this.
            return null;
        }
    }

    private void Raise(object sender, FileSystemEventArgs e) =>
        Changed?.Invoke(this, new DirectoryChangedEventArgs(e.Name));

    // Two things arrive here and both mean the same to a caller. An overflowed buffer lost
    // some changes, and a deleted directory lost all of them from now on. Either way the
    // answer is to read again, and letting go of every watch first means the caller's next
    // read puts them all back together.
    private void OnError(object sender, ErrorEventArgs e)
    {
        Stop();

        // No name, because whatever it was is exactly what was lost.
        Changed?.Invoke(this, new DirectoryChangedEventArgs(null));
    }
}
