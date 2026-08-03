using System.Diagnostics;
using Avalonia.Threading;

namespace Kitbash.Ui.Toasts;

/// <summary>
/// The scheduler a running application uses: Avalonia's UI thread, and a stopwatch.
/// </summary>
public sealed class ToastScheduler : IToastScheduler
{
    // Started once rather than read from DateTime, so a clock correction cannot make a
    // toast dwell backwards.
    private readonly Stopwatch _since = Stopwatch.StartNew();

    public TimeSpan Now => _since.Elapsed;

    public bool OnThread => Dispatcher.UIThread.CheckAccess();

    public void Run(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (OnThread)
        {
            work();
            return;
        }

        Dispatcher.UIThread.Post(work);
    }

    public IDisposable Every(TimeSpan interval, Action tick)
    {
        ArgumentNullException.ThrowIfNull(tick);

        return DispatcherTimer.Run(
            () =>
            {
                tick();
                return true;
            },
            interval);
    }
}
