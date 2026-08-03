namespace Workbench.Core.Settings.Schema;

/// <summary>
/// One line of something a page shows without editing it. Used by a
/// <see cref="SettingsReadoutRow"/> and by a setting whose value is an array.
/// </summary>
/// <param name="IsCurrent">
/// Marked out from the rest, such as the workspace that is open. Only a
/// <see cref="SettingsReadoutStyle.List"/> draws the mark, since a single value has
/// nothing to stand out from.
/// </param>
/// <param name="Full">
/// The whole value, when <paramref name="Text"/> was shortened to fit the row. Null when
/// the two are the same, which is most of them.
/// </param>
public sealed record SettingsListEntry(string Text, bool IsCurrent = false, string? Full = null)
{
    /// <summary>
    /// What copying puts on the clipboard. An elided path is worth reading and worth
    /// nothing at all pasted somewhere, so a row that shortens for display says so and
    /// this hands back what it shortened.
    /// </summary>
    public string Copied => string.IsNullOrEmpty(Full) ? Text : Full;
}
