namespace Kitbash.Core.Settings.Schema;

/// <summary>
/// One row on a settings page. Almost every row is a <see cref="SettingDescriptor{T}"/>.
/// The other two are the escape hatch an app supplies for something no descriptor can
/// describe: <see cref="SettingsReadoutRow"/> shows it and <see cref="SettingsEditorRow"/>
/// edits it. Neither has a key, a default or a rule, so the schema never grows a way to
/// describe an action.
/// </summary>
public interface ISettingsRow
{
    string Name { get; }

    /// <summary>
    /// Drawn under the name. Required on a setting, so none arrives unexplained, and
    /// blank is allowed on a readout whose name says the whole of it.
    /// </summary>
    string Description { get; }

    /// <summary>Beside the name or under it. See <see cref="SettingsRowLayout"/>.</summary>
    SettingsRowLayout Layout { get; }
}

/// <summary>
/// A row that really is a setting, with its type erased so a reader can walk a page of
/// rows that are not all the same type.
/// </summary>
public interface ISettingDescriptor : ISettingsRow
{
    /// <summary>The dotted key this reads and writes.</summary>
    string Key { get; }

    Type ValueType { get; }

    /// <summary>What the setting is when no file says otherwise. Never null.</summary>
    object Default { get; }

    SettingEditor Editor { get; }

    /// <summary>Drawn but never written. The value still comes from a file.</summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Nothing already running reads this again, so a change to it means nothing until the
    /// app starts afresh. Say it here rather than in the description, since a window offers
    /// to restart on the strength of it.
    /// </summary>
    bool NeedsRestart { get; }

    /// <summary>Drawn after a number, such as px or ms. Null when there is none.</summary>
    string? Unit { get; }

    IReadOnlyList<ISettingRule> Rules { get; }

    ISettingProbe? Probe { get; }

    /// <summary>
    /// Converts a stored value and runs the rules over it. <paramref name="value"/> is
    /// the converted value, and is null unless the check came back usable.
    /// </summary>
    SettingCheck Check(object? raw, ISettingsValueConverter converter, out object? value);

    /// <summary>Whether a converted value is the one this setting would take anyway.</summary>
    bool IsDefault(object? value);

    /// <summary>
    /// The options to offer, from a choice source if there is one and from a closed
    /// <see cref="ChoiceRule{T}"/> otherwise. Empty when the setting is not a choice.
    /// Runs off the UI thread.
    /// </summary>
    Task<IReadOnlyList<SettingChoice>> Offer(CancellationToken token);
}
