namespace Workbench.Ui.Toasts;

/// <summary>
/// Time, and the one thread toasts are changed on. The whole seam between the toast
/// service and a running application.
/// </summary>
/// <remarks>
/// Everything that makes a toast behave the way it does is timing: how long it dwells,
/// what a hovered region pauses, and how a repeat collapses. None of that is testable
/// against a real clock, so the service asks for time here rather than reading it, and a
/// test drives dwell without waiting for any of it.
/// <para>
/// It carries the thread as well because a toast is usually raised from work that is
/// deliberately off the UI thread. A caller should not have to marshal a status message
/// by hand, so <see cref="Run"/> does it and every mutation goes through it.
/// </para>
/// </remarks>
public interface IToastScheduler
{
    /// <summary>
    /// How long the application has been running. Monotonic, so it cannot go backwards
    /// when the wall clock is corrected, which would otherwise leave a toast dwelling
    /// for an hour.
    /// </summary>
    TimeSpan Now { get; }

    /// <summary>
    /// The caller is already on the toast thread, so it can read a region and be told
    /// the answer rather than being told the work has been queued.
    /// </summary>
    bool OnThread { get; }

    /// <summary>
    /// Runs the work on the toast thread. Immediately when the caller is already there,
    /// so ordering is what it reads like, and queued when it is not.
    /// </summary>
    void Run(Action work);

    /// <summary>
    /// Calls back on the toast thread until the result is disposed. The service starts
    /// one of these while a toast is counting down and stops it when none is, so an idle
    /// application ticks nothing at all.
    /// </summary>
    IDisposable Every(TimeSpan interval, Action tick);
}
