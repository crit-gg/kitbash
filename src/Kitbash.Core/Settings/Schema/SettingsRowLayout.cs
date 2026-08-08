namespace Kitbash.Core.Settings.Schema;

/// <summary>
/// Where a row's editor sits. There is no derived option, since the answer must not change
/// with the data: a list that happened to hold one line would otherwise move.
/// </summary>
public enum SettingsRowLayout
{
    /// <summary>Right of the name and its note, in the column every row shares.</summary>
    Beside,

    /// <summary>
    /// Under them, across the whole row. For an editor too wide to read at a third of the
    /// page, such as a table of its own.
    /// </summary>
    Below,
}
