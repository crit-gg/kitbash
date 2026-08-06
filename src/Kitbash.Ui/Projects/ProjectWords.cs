namespace Kitbash.Ui.Projects;

/// <summary>
/// What an app calls the things it opens. Foundry lists projects, Hoard lists libraries
/// and Splice lists repositories, and the window says whichever it was given.
/// </summary>
/// <param name="App">The app's own name, which the title is built from.</param>
/// <param name="Item">One of them, lower case. "project".</param>
/// <param name="Items">Several of them, lower case. "projects".</param>
public sealed record ProjectWords(string App, string Item, string Items)
{
    /// <summary>The window title. "Welcome to Foundry" unless the app says otherwise.</summary>
    public string Title { get; init; } = $"Welcome to {App}";

    /// <summary>
    /// The app's own icon, as an avares uri. It is drawn in the title bar and given to
    /// the desktop for the task bar. One that cannot be loaded leaves both empty.
    /// </summary>
    public Uri? Icon { get; init; }

    /// <summary>
    /// What the title bar reads after the title. Blank collapses the slot, which is what
    /// an app that does not say its version gets.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// The one filled button. Splice says Clone a repository here, so it is a whole
    /// label rather than a verb put in front of <see cref="Item"/>.
    /// </summary>
    public string NewLabel { get; init; } = $"New {Item}";

    /// <summary>The bordered button beside it, which browses for one already on disk.</summary>
    public string OpenLabel { get; init; } = "Open";

    public string SearchPlaceholder { get; init; } = $"Search {Items} by name or path";

    /// <summary>What the window says when nothing has ever been opened.</summary>
    public string EmptyTitle { get; init; } = $"No {Items} yet";

    public string EmptyNote { get; init; } =
        $"Anything you open shows up here, newest first, so getting back to a {Item} is one click.";
}
