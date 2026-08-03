namespace Kitbash.Core.Settings.Schema;

/// <summary>One option a setting offers, as a schema declares it.</summary>
public sealed record SettingChoice<T>(T Value, string Label, string? Description = null)
    where T : notnull;

/// <summary>
/// One option a setting offers, with its type erased. This is what a reader gets, since
/// a page draws rows of several types at once.
/// </summary>
public sealed record SettingChoice(object Value, string Label, string? Description = null);
