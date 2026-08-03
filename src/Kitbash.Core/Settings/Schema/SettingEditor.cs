namespace Kitbash.Core.Settings.Schema;

/// <summary>
/// Which control edits a setting. <see cref="Derived"/> is what a descriptor takes when
/// it says nothing, and the descriptor works one out from its type and its options.
/// Name one only where that derivation is wrong.
/// </summary>
public enum SettingEditor
{
    /// <summary>Worked out from the value type and the options. The default.</summary>
    Derived = 0,

    /// <summary>A bool.</summary>
    Toggle = 1,

    /// <summary>A closed choice with few short options.</summary>
    Segment = 2,

    /// <summary>A choice, open or closed, drawn as a dropdown.</summary>
    Select = 3,

    /// <summary>A number, with its unit beside it.</summary>
    Number = 4,

    /// <summary>A single line of text.</summary>
    Text = 5,

    /// <summary>An array, drawn as a list.</summary>
    List = 6,

    /// <summary>A path to one file or folder, with a way to browse for it.</summary>
    Path = 7,
}
