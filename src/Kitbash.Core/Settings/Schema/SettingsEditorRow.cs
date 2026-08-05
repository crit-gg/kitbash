namespace Kitbash.Core.Settings.Schema;

/// <summary>
/// A row an app supplies and edits itself, the other half of
/// <see cref="SettingsReadoutRow"/>. It has no key, so the schema still describes no
/// action and the settings writer still refuses anything a page did not declare.
/// </summary>
public sealed class SettingsEditorRow : ISettingsRow
{
    private readonly string _name = string.Empty;

    public required string Name
    {
        get => _name;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _name = value;
        }
    }

    /// <summary>Optional here, as on a readout, since an app names its own row.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// The editor, which is also what the window draws. It is presented as content, so
    /// the app supplies a template for its own type.
    /// </summary>
    public required ISettingsEditor Editor { get; init; }
}
