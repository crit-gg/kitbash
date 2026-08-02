namespace Workbench.Ui.Toasts;

/// <summary>
/// How anything in the app says that something just happened.
/// </summary>
/// <remarks>
/// Taken through a constructor, the way a file system is. There is no static entry point
/// and no ambient host, so a view model that reports progress is handed this and can be
/// handed something else instead.
/// <para>
/// One of these owns one set of eight regions. The application has one, and anything
/// that wants its own, such as a tool panel reporting inside itself, asks
/// <see cref="IToastServiceFactory"/> for another.
/// </para>
/// <para>
/// This knows nothing about a window or a visual tree. It owns toasts, regions, dwell
/// and grouping, and a <c>ToastHost</c> watches it and draws. That split is what makes
/// the timing checkable with no window anywhere.
/// </para>
/// </remarks>
public interface IToastService
{
    /// <summary>
    /// Raises a toast and hands it back, so the caller can update it or take it away.
    /// </summary>
    /// <remarks>
    /// Call it from the toast thread, which is the UI thread in a running application.
    /// Reporting is the last step of work done somewhere else, and the answer depends on
    /// what is already showing, so there is nothing to hand back until this reaches that
    /// thread. Use <see cref="Post"/> from anywhere else.
    /// </remarks>
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
