namespace Kitbash.Ui.Toasts;

/// <summary>
/// The toast service. Eight regions, one timer, and no idea what any of it looks like.
/// </summary>
public sealed class ToastService : IToastService, IDisposable
{
    private readonly IToastScheduler _scheduler;
    private readonly ToastOptions _options;
    private readonly Dictionary<ToastAnchor, ToastRegion> _regions;

    /// <summary>
    /// Runs only while something has a reason to be counted. An application with no
    /// toast showing runs no timer, and one whose regions are all paused stops as well.
    /// </summary>
    private IDisposable? _ticker;
    private TimeSpan _ticked;
    private bool _disposed;

    public ToastService(IToastScheduler scheduler, ToastOptions options)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(options);

        _scheduler = scheduler;
        _options = options;
        _regions = Enum.GetValues<ToastAnchor>()
            .ToDictionary(anchor => anchor, anchor => new ToastRegion(anchor, options));

        foreach (var region in _regions.Values)
        {
            region.Changed = Pump;
        }
    }

    public Toast Show(ToastRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_scheduler.OnThread)
        {
            throw new InvalidOperationException(
                "Show a toast from the toast thread. Gather off it, report on it, or use Post.");
        }

        return Place(request);
    }

    public void Post(ToastRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _scheduler.Run(() => Place(request));
    }

    public IToastRegion Region(ToastAnchor anchor) => _regions[anchor];

    public void DismissAll() => _scheduler.Run(() =>
    {
        foreach (var region in _regions.Values)
        {
            region.DismissAll();
        }
    });

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ticker?.Dispose();
        _ticker = null;

        foreach (var region in _regions.Values)
        {
            region.Changed = null;
        }
    }

    private Toast Place(ToastRequest request)
    {
        var region = _regions[request.Anchor];

        // A repeat collapses onto the card already there and counts, rather than pushing
        // another one. Only onto the newest, so two different toasts alternating stay two
        // cards instead of one that changes its mind.
        if (_options.Groups
            && region.Newest is { } newest
            && newest.GroupKey == Toast.KeyFor(request))
        {
            newest.Repeat(request);
            Pump();
            return newest;
        }

        var toast = new Toast(request, DwellFor(request), _scheduler);
        region.Add(toast);

        return toast;
    }

    /// <summary>
    /// How long a toast stays. The caller's value wins, then the tier, then whether there
    /// is an action to press.
    /// </summary>
    private TimeSpan DwellFor(ToastRequest request)
    {
        if (request.Dwell is { } asked)
        {
            return asked > TimeSpan.Zero ? asked : TimeSpan.Zero;
        }

        if (request.Progress is not null)
        {
            return TimeSpan.Zero;
        }

        return request.Tier switch
        {
            ToastTier.Error or ToastTier.Busy => TimeSpan.Zero,
            _ => request.Actions.Count > 0 ? _options.DwellWithAction : _options.Dwell,
        };
    }

    /// <summary>Starts the timer when there is something to count, stops it when there is not.</summary>
    private void Pump()
    {
        if (_disposed)
        {
            return;
        }

        var wanted = false;

        foreach (var region in _regions.Values)
        {
            if (region.NeedsTick)
            {
                wanted = true;
                break;
            }
        }

        if (wanted && _ticker is null)
        {
            _ticked = _scheduler.Now;
            _ticker = _scheduler.Every(_options.Tick, OnTick);
            return;
        }

        if (!wanted && _ticker is not null)
        {
            _ticker.Dispose();
            _ticker = null;
        }
    }

    private void OnTick()
    {
        var now = _scheduler.Now;
        var by = now - _ticked;
        _ticked = now;

        foreach (var region in _regions.Values)
        {
            region.Advance(by);
        }

        Pump();
    }
}
