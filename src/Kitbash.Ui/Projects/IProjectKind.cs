using Avalonia.Controls;
using Kitbash.Core.Projects;

namespace Kitbash.Ui.Projects;

/// <summary>
/// What a project is, for one app. The window keeps a list of paths and knows nothing
/// about what is at the end of one, so everything an app decides is here.
/// </summary>
public interface IProjectKind
{
    /// <summary>What this app calls the things it opens.</summary>
    ProjectWords Words { get; }

    /// <summary>
    /// Whether opening one closes the window. False for an app whose shell is this
    /// window, true for one that opens a window of its own.
    /// </summary>
    bool ClosesOnOpen { get; }

    /// <summary>
    /// Anything else this app puts in the rail, under the list. Empty for an app that
    /// wants the list alone, which is most of them. Read once when the window is built.
    /// </summary>
    IReadOnlyList<ProjectPage> Pages { get; }

    /// <summary>
    /// What the row says beyond the name and the path. Called on the thread pool, once
    /// per entry per refresh, so it may read a disk.
    /// </summary>
    ProjectFacts Describe(RecentProject project);

    /// <summary>
    /// The filled button. Make one from nothing, usually in a dialog. Null when nothing
    /// was made, which includes a person changing their mind.
    /// </summary>
    Task<ProjectChoice?> CreateAsync(Window owner);

    /// <summary>
    /// The bordered button, and the way a lost entry is found again. Browse for one that
    /// already exists and say whether it qualifies.
    /// </summary>
    /// <param name="owner">The window to open the picker over.</param>
    /// <param name="replacing">
    /// The entry being pointed somewhere new, or null when this is the Open button. A
    /// picker can open near the old path and a refusal can name it.
    /// </param>
    Task<ProjectChoice?> BrowseAsync(Window owner, RecentProject? replacing);

    /// <summary>
    /// Opens one. Report a failure here, since the app knows what went wrong and the
    /// window does not.
    /// </summary>
    /// <param name="inNewWindow">
    /// The person asked for a second window rather than to move this one, so the list
    /// stays open whatever <see cref="ClosesOnOpen"/> says.
    /// </param>
    /// <returns>False leaves the window open and the entry where it is.</returns>
    Task<bool> OpenAsync(RecentProject project, bool inNewWindow, Window owner);
}
