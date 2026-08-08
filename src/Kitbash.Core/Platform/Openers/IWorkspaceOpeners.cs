namespace Kitbash.Core.Platform.Openers;

/// <summary>What a workspace can be opened in, and how to open it.</summary>
public interface IWorkspaceOpeners
{
    /// <summary>
    /// Everything on offer, detected first and then a person's own. What is installed is
    /// looked up once and held, since a program is not installed while the app runs. What
    /// a person set is read every call, since the settings window changes it.
    /// </summary>
    Task<IReadOnlyList<WorkspaceOpener>> ReadAsync(CancellationToken cancellation = default);

    /// <summary>
    /// The same list with the hidden ones still in it, for the page that hides them. Every
    /// other caller wants <see cref="ReadAsync"/>.
    /// </summary>
    Task<IReadOnlyList<WorkspaceOpener>> ReadAllAsync(CancellationToken cancellation = default);

    /// <summary>
    /// The files in this workspace one tool would open. Walks a disk, so it runs wherever
    /// its caller runs and never on the UI thread.
    /// </summary>
    IReadOnlyList<OpenChoice> ChoicesFor(WorkspaceOpener opener, string workspaceRoot);

    /// <summary>Starts it. A null choice opens the workspace folder itself.</summary>
    /// <exception cref="ProcessStartException">The process could not be started.</exception>
    void Open(WorkspaceOpener opener, OpenChoice? choice, string workspaceRoot);
}
