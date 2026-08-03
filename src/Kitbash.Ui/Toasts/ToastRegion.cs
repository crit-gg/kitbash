using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Kitbash.Ui.Toasts;

/// <summary>
/// One region's stack. Everything here runs on the toast thread, because the service
/// only ever reaches it through <see cref="IToastScheduler.Run"/>.
/// </summary>
internal sealed class ToastRegion : IToastRegion
{
    private readonly ObservableCollection<Toast> _visible = [];

    /// <summary>Everything in the region, oldest first, including anything on its way out.</summary>
    private readonly List<Toast> _all = [];

    /// <summary>What is fading, and how long each has left to do it in.</summary>
    private readonly List<(Toast Toast, TimeSpan Left)> _leaving = [];

    private readonly ToastOptions _options;

    private bool _isPaused;
    private int _waiting;

    internal ToastRegion(ToastAnchor anchor, ToastOptions options)
    {
        Anchor = anchor;
        _options = options;
        Visible = new ReadOnlyObservableCollection<Toast>(_visible);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ToastAnchor Anchor { get; }

    public ReadOnlyObservableCollection<Toast> Visible { get; }

    public int Waiting
    {
        get => _waiting;
        private set
        {
            if (_waiting == value)
            {
                return;
            }

            _waiting = value;
            Raise(nameof(Waiting));
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (_isPaused == value)
            {
                return;
            }

            _isPaused = value;
            Raise(nameof(IsPaused));
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Something happened that may have changed whether this region has a reason to
    /// tick. The service listens, so a region that goes quiet stops the timer for the
    /// whole application and one that wakes up starts it again.
    /// </summary>
    internal Action? Changed { get; set; }

    /// <summary>
    /// The newest card that is still standing, which is the only one a repeat can
    /// collapse onto. A card already fading is not it, since collapsing onto one would
    /// bring it back from an exit that has started.
    /// </summary>
    internal Toast? Newest
    {
        get
        {
            for (var i = _all.Count - 1; i >= 0; i--)
            {
                if (!_all[i].IsDismissing)
                {
                    return _all[i];
                }
            }

            return null;
        }
    }

    /// <summary>
    /// There is something for the service's ticker to do here. An idle region says no,
    /// and an application whose regions all say no runs no timer at all.
    /// </summary>
    internal bool NeedsTick
    {
        get
        {
            if (_leaving.Count > 0)
            {
                return true;
            }

            if (_isPaused)
            {
                return false;
            }

            foreach (var toast in _all)
            {
                if (toast.IsCountingDown)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void DismissAll()
    {
        foreach (var toast in _all.ToArray())
        {
            toast.Dismiss();
        }
    }

    internal void Add(Toast toast)
    {
        _all.Add(toast);
        toast.PropertyChanged += OnToastChanged;
        Sync();
        Changed?.Invoke();
    }

    /// <summary>
    /// Moves every clock in the region on by one tick.
    /// </summary>
    internal void Advance(TimeSpan by)
    {
        for (var i = _leaving.Count - 1; i >= 0; i--)
        {
            var left = _leaving[i].Left - by;

            if (left > TimeSpan.Zero)
            {
                _leaving[i] = (_leaving[i].Toast, left);
                continue;
            }

            var done = _leaving[i].Toast;
            _leaving.RemoveAt(i);
            Close(done);
        }

        if (_isPaused)
        {
            return;
        }

        foreach (var toast in _all.ToArray())
        {
            if (toast.Advance(by))
            {
                toast.Dismiss();
            }
        }
    }

    private void OnToastChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Toast.IsDismissing) || sender is not Toast toast)
        {
            return;
        }

        if (!toast.IsDismissing)
        {
            return;
        }

        if (_options.Exit <= TimeSpan.Zero)
        {
            Close(toast);
            return;
        }

        _leaving.Add((toast, _options.Exit));
        Changed?.Invoke();
    }

    private void Close(Toast toast)
    {
        toast.PropertyChanged -= OnToastChanged;
        _all.Remove(toast);
        Sync();
        Changed?.Invoke();
        toast.RaiseClosed();
    }

    /// <summary>
    /// Brings the visible list into line with what should be showing, by the fewest
    /// edits that get there.
    /// </summary>
    private void Sync()
    {
        var limit = Math.Max(1, _options.VisiblePerRegion);
        var take = Math.Min(limit, _all.Count);

        // The newest are the ones that show, and they are at the end of the arrival list.
        var window = _all.GetRange(_all.Count - take, take);

        for (var i = _visible.Count - 1; i >= 0; i--)
        {
            if (!window.Contains(_visible[i]))
            {
                _visible.RemoveAt(i);
            }
        }

        for (var i = 0; i < window.Count; i++)
        {
            var at = _visible.IndexOf(window[i]);

            if (at < 0)
            {
                _visible.Insert(i, window[i]);
            }
            else if (at != i)
            {
                _visible.Move(at, i);
            }
        }

        for (var i = 0; i < window.Count; i++)
        {
            window[i].Depth = window.Count - 1 - i;
        }

        // Anything held back is deeper than everything showing. It has no card yet, so
        // this only matters when it gets one, but a toast whose depth still describes
        // where it used to be would arrive wearing the wrong tier of the fade.
        foreach (var held in _all)
        {
            if (!window.Contains(held))
            {
                held.Depth = window.Count;
            }
        }

        Waiting = _all.Count - take;
    }

    private void Raise(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
