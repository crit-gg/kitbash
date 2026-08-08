namespace Kitbash.Workspaces;

/// <summary>
/// One row of <c>workspace.links</c> exactly as the file spells it, whether or not
/// Kitbash can use it. What the settings page edits, so a row it cannot draw as a link
/// is still a row rather than something a save would drop.
/// </summary>
public sealed record WorkspaceLinkEntry(string Label, string Url, string Icon);
