using System.Diagnostics;
using Workbench.Core.IO;

namespace Workbench.Core.Git;

/// <summary>
/// Follows a repository by watching a few of its folders and reading it on a slow beat.
/// </summary>
public sealed class GitStatusMonitor : IGitStatusMonitor
{
    /// <summary>How long to wait for a burst to settle before reading.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// The least time between two reads that a watch asked for. A storm of writes cannot
    /// spawn more than one git per second whatever the debounce lets through.
    /// </summary>
    private static readonly TimeSpan Rest = TimeSpan.FromSeconds(1);

    /// <summary>The backstop for changes no watch reports. Runs git, so keep it slow.</summary>
    private static readonly TimeSpan Beat = TimeSpan.FromSeconds(5);

    /// <summary>How much a read is allowed to insist on happening.</summary>
    private enum Urgency
    {
        /// <summary>A watch said something changed. Respects the rest between reads, and gives up if one is running.</summary>
        Debounced,

        /// <summary>The beat came round. Skips the rest, still gives up if one is running.</summary>
        Beat,

        /// <summary>Someone asked. Skips the rest and waits its turn rather than giving up.</summary>
        Now,
    }

    private readonly IGitStatusReader _reader;
    private readonly IDirectoryWatcher _watcher;
    private readonly SemaphoreSlim _reading = new(1, 1);
    private readonly Stopwatch _sinceRead = Stopwatch.StartNew();

    private CancellationTokenSource? _life;
    private Timer? _debounce;
    private string? _root;
    private bool _active = true;
    private bool _disposed;

    public GitStatusMonitor(IGitStatusReader reader, IDirectoryWatcher watcher)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(watcher);

        _reader = reader;
        _watcher = watcher;
        _watcher.Changed += OnWatched;
    }

    public GitStatus? Status { get; private set; }

    public bool IsActive
    {
        get => _active;

        set
        {
            if (_active == value)
            {
                return;
            }

            _active = value;

            if (value)
            {
                // Coming back is the moment something is most likely to have changed while
                // nobody was reading, so this one skips the rate limit.
                _ = ReadAsync(CancellationToken.None, Urgency.Now);
            }
        }
    }

    public event EventHandler? Changed;

    public void Follow(string? root)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.Equals(root, _root, StringComparison.Ordinal))
        {
            return;
        }

        StopFollowing();
        _root = root;

        // Let go of the old repository's status before the new one has been read, so this
        // never describes a folder it is not following. The first read lands in a few
        // milliseconds, so nothing sits empty for long.
        Publish(null);

        if (root is null)
        {
            return;
        }

        _life = new CancellationTokenSource();
        _ = BeatAsync(_life.Token);

        // Nothing is watched yet. Where git keeps this repository is something only git can
        // answer, so the first read is what tells us what to watch.
        _ = ReadAsync(_life.Token, Urgency.Now);
    }

    public Task RefreshAsync(CancellationToken cancellation = default) => ReadAsync(cancellation, Urgency.Now);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.Changed -= OnWatched;
        StopFollowing();
        _watcher.Dispose();
        _reading.Dispose();
    }

    private void OnWatched(object? sender, DirectoryChangedEventArgs e)
    {
        if (e.Name is { } name && name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Schedule();
    }

    // A write to the git directory is usually several writes. Waiting for them to stop turns
    // a commit into one read rather than one per file git touched.
    private void Schedule()
    {
        if (_disposed || _root is null || !_active)
        {
            return;
        }

        _debounce ??= new Timer(_ => _ = ReadAsync(CancellationToken.None, Urgency.Debounced), null, Timeout.Infinite, Timeout.Infinite);

        try
        {
            _debounce.Change(Settle, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // Stopped following between the two lines above.
        }
    }

    private async Task BeatAsync(CancellationToken cancellation)
    {
        try
        {
            using var timer = new PeriodicTimer(Beat);

            while (await timer.WaitForNextTickAsync(cancellation).ConfigureAwait(false))
            {
                if (_active)
                {
                    await ReadAsync(cancellation, Urgency.Beat).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped following, or shut down.
        }
    }

    private async Task ReadAsync(CancellationToken cancellation, Urgency urgency)
    {
        var root = _root;

        if (_disposed || root is null)
        {
            return;
        }

        if (urgency == Urgency.Debounced && _sinceRead.Elapsed < Rest)
        {
            // Too soon after the last one. Come back rather than run git again, since a
            // burst of writes is one change to a person.
            Schedule();
            return;
        }

        try
        {
            if (urgency == Urgency.Now)
            {
                // Waits rather than gives up, because the read in flight is answering a
                // question nobody is asking any more. Following a different folder is the
                // case that matters: dropping this read would leave the new folder blank
                // until the beat came round.
                await _reading.WaitAsync(cancellation).ConfigureAwait(false);
            }
            else if (!await _reading.WaitAsync(0, cancellation).ConfigureAwait(false))
            {
                // Already reading. Whatever that read finds is at least as fresh as this
                // one would have been.
                return;
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
        {
            // Stopped following, or shut down, while waiting for a turn. Nothing was taken,
            // so there is nothing to give back.
            return;
        }

        try
        {
            // Read again after the wait. This may have queued behind a read for a folder
            // that is no longer the one being followed.
            root = _root;

            if (_disposed || root is null)
            {
                return;
            }

            var status = await _reader.ReadAsync(root, cancellation).ConfigureAwait(false);

            _sinceRead.Restart();

            // The folder may have changed while git ran.
            if (_disposed || !string.Equals(root, _root, StringComparison.Ordinal))
            {
                return;
            }

            Arm(status);
            Publish(status);
        }
        catch (OperationCanceledException)
        {
            // Stopped following while git was running.
        }
        finally
        {
            try
            {
                _reading.Release();
            }
            catch (ObjectDisposedException)
            {
                // Shut down while git was running. Nobody is waiting for this turn.
            }
        }
    }

    // Runs after every read rather than once, so a watch that never started and a watch that
    // died both come back, and moving to a worktree or a submodule moves the watches with it.
    // The folders match in the ordinary case, which makes this free.
    private void Arm(GitStatus? status)
    {
        if (status is null)
        {
            _watcher.Stop();
            return;
        }

        var wanted = status.Places.Watchable;

        if (!_watcher.Watching.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            _watcher.Watch(wanted);
        }
    }

    private void Publish(GitStatus? status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void StopFollowing()
    {
        if (_life is { } life)
        {
            life.Cancel();
            life.Dispose();
            _life = null;
        }

        _debounce?.Dispose();
        _debounce = null;
        _watcher.Stop();
    }
}
