using System.ComponentModel;

namespace Kitbash.Ui.Toasts;

/// <summary>
/// One toast, live. What <see cref="IToastService.Show"/> hands back, so whatever raised
/// it can keep it up to date and take it away again.
/// </summary>
public sealed class Toast : INotifyPropertyChanged
{
    private readonly IToastScheduler _scheduler;

    private string _title;
    private string? _body;
    private double? _progress;
    private int _count = 1;
    private int _depth;
    private bool _isDismissing;

    /// <summary>How long it was given, and how much of that is left.</summary>
    private readonly TimeSpan _dwell;
    private TimeSpan _left;

    internal Toast(ToastRequest request, TimeSpan dwell, IToastScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title, nameof(request));

        if (request.Actions.Count > 2)
        {
            throw new ArgumentException("A toast carries at most two actions.", nameof(request));
        }

        if (request.Progress is { } start && (start < 0 || start > 1))
        {
            throw new ArgumentOutOfRangeException(nameof(request), start, "Progress runs from 0 to 1.");
        }

        _scheduler = scheduler;
        _title = request.Title;
        _body = request.Body;
        _progress = request.Progress;

        Tier = request.Tier;
        Form = request.Form;
        Anchor = request.Anchor;
        Actions = [.. request.Actions];
        GroupKey = KeyFor(request);

        _dwell = dwell;
        _left = dwell;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// It has gone. Raised once, after the exit has run, whether it timed out, was
    /// pressed or was taken away by whatever raised it.
    /// </summary>
    public event EventHandler? Closed;

    public ToastTier Tier { get; }

    public ToastForm Form { get; }

    public ToastAnchor Anchor { get; }

    public IReadOnlyList<ToastAction> Actions { get; }

    /// <summary>What another toast has to match to collapse onto this one.</summary>
    public string GroupKey { get; }

    /// <inheritdoc cref="ToastRequest.Title"/>
    public string Title
    {
        get => _title;
        set => _scheduler.Run(() => Set(ref _title, value ?? string.Empty, nameof(Title)));
    }

    /// <inheritdoc cref="ToastRequest.Body"/>
    public string? Body
    {
        get => _body;
        set => _scheduler.Run(() => Set(ref _body, value, nameof(Body), nameof(HasBody)));
    }

    public bool HasBody => !string.IsNullOrWhiteSpace(_body);

    /// <inheritdoc cref="ToastRequest.Progress"/>
    public double? Progress
    {
        get => _progress;
        set => _scheduler.Run(() => Set(
            ref _progress,
            value is { } at ? Math.Clamp(at, 0, 1) : null,
            nameof(Progress), nameof(HasMeter), nameof(Meter)));
    }

    /// <summary>
    /// How many times this toast has fired. One until it repeats, and the card shows a
    /// count from two.
    /// </summary>
    public int Count => _count;

    /// <summary>It has fired more than once and says so.</summary>
    public bool IsGrouped => _count > 1;

    /// <summary>
    /// How far back in its stack this card sits, 0 being the newest and nearest the
    /// anchored edge. The region writes it, and the card fades and scales by it.
    /// </summary>
    public int Depth
    {
        get => _depth;
        internal set => Set(ref _depth, value, nameof(Depth));
    }

    /// <summary>
    /// It is on its way out. The card fades for <see cref="ToastOptions.Exit"/> while
    /// this is true, and then it is gone.
    /// </summary>
    public bool IsDismissing
    {
        get => _isDismissing;
        private set => Set(ref _isDismissing, value, nameof(IsDismissing));
    }

    /// <summary>
    /// There is a bar along the foot, and what it is saying. Work done when this toast
    /// reports progress, otherwise time left.
    /// </summary>
    public bool HasMeter => _progress is not null || _dwell > TimeSpan.Zero;

    /// <inheritdoc cref="HasMeter"/>
    public double Meter => _progress ?? (_dwell > TimeSpan.Zero ? _left / _dwell : 0);

    /// <summary>
    /// It is still counting down, so the service has a reason to keep ticking. False for
    /// an error, for anything busy, for anything reporting progress and for anything
    /// already leaving.
    /// </summary>
    internal bool IsCountingDown => _dwell > TimeSpan.Zero && _progress is null && !_isDismissing;

    /// <summary>
    /// Takes it away. Doing it twice is doing it once, so a card pressed while it is
    /// already fading does not restart anything.
    /// </summary>
    public void Dismiss() => _scheduler.Run(() =>
    {
        if (_isDismissing)
        {
            return;
        }

        _left = TimeSpan.Zero;
        IsDismissing = true;
    });

    /// <summary>
    /// Runs an action and closes the toast when that action says to. The card calls this
    /// rather than invoking the action itself, so what a button does and what it does to
    /// the card it sits on are decided in one place.
    /// </summary>
    public void Run(ToastAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        action.Invoke();

        if (action.Dismisses)
        {
            Dismiss();
        }
    }

    /// <summary>The same toast has fired again, so this one counts it instead.</summary>
    internal void Repeat(ToastRequest request)
    {
        Set(ref _body, request.Body, nameof(Body), nameof(HasBody));

        if (request.Progress is { } at)
        {
            Set(ref _progress, Math.Clamp(at, 0, 1), nameof(Progress), nameof(HasMeter), nameof(Meter));
        }

        Set(ref _count, _count + 1, nameof(Count), nameof(IsGrouped));

        _left = _dwell;
        Raise(nameof(Meter));
    }

    /// <summary>
    /// Moves the clock on. Returns true once there is nothing left, which is the
    /// service's cue to take it away.
    /// </summary>
    internal bool Advance(TimeSpan by)
    {
        if (!IsCountingDown)
        {
            return false;
        }

        _left -= by;

        if (_left < TimeSpan.Zero)
        {
            _left = TimeSpan.Zero;
        }

        Raise(nameof(Meter));

        return _left <= TimeSpan.Zero;
    }

    /// <summary>Said once, after the card has gone.</summary>
    internal void RaiseClosed() => Closed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// The tier, the form and the title, so two of the same thing collapse without
    /// anyone naming a key. A caller that needs them apart gives one of them a key of
    /// its own.
    /// </summary>
    internal static string KeyFor(ToastRequest request) =>
        request.GroupKey ?? $"{request.Tier}{request.Form}{request.Title}";

    private void Set<T>(ref T field, T value, params string[] raise)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;

        foreach (var name in raise)
        {
            Raise(name);
        }
    }

    private void Raise(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
