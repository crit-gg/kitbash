namespace Workbench.Ui.Toasts;

/// <summary>
/// How anything in the app says that something just happened.
/// </summary>
public interface IToastService
{
    /// <summary>
    /// Raises a toast and hands it back, so the caller can update it or take it away.
    /// </summary>
    /// <exception cref="InvalidOperationException">Called from another thread.</exception>
    Toast Show(ToastRequest request);

    /// <summary>
    /// Raises a toast from any thread without handing one back. For a worker that has
    /// something to say and nothing to follow up on.
    /// </summary>
    void Post(ToastRequest request);

    /// <summary>One of the eight stacks. The same instance every time it is asked for.</summary>
    IToastRegion Region(ToastAnchor anchor);

    /// <summary>Clears every region.</summary>
    void DismissAll();
}
