namespace Kitbash.Ui.Projects;

/// <summary>
/// A path an app has accepted, on its way to the recent list. Returned by creating one
/// and by browsing for one, so the app decides what qualifies and the store remembers it.
/// </summary>
/// <param name="Path">The full path. What it points at is the app's business.</param>
/// <param name="Name">What the list should call it.</param>
public sealed record ProjectChoice(string Path, string Name);
