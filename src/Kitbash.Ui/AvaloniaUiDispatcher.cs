using Avalonia.Threading;

namespace Kitbash.Ui;

/// <summary>The real one, over Avalonia's dispatcher.</summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Dispatcher.UIThread.Post(action);
    }
}
