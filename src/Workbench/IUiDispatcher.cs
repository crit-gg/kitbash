namespace Workbench;

/// <summary>
/// Moves work onto the thread that owns the views. Behind an interface so a view model can
/// be given one, rather than reaching for the running application.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Runs the action on the UI thread and returns without waiting. Safe to call from any
    /// thread, including the UI thread itself.
    /// </summary>
    void Post(Action action);
}
