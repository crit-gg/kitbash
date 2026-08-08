using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Core.Settings.Schema;

namespace Kitbash.Ui.Settings;

/// <summary>One row of a settings page, whatever kind of row it is.</summary>
public abstract class SettingsRowViewModel : ObservableObject
{
    protected SettingsRowViewModel(ISettingsRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        Name = row.Name;
        Description = row.Description;
        IsBelow = row.Layout is SettingsRowLayout.Below;
    }

    public string Name { get; }

    public string Description { get; }

    /// <summary>A readout may leave it blank, and an empty line still takes its height.</summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>
    /// The editor is under the name rather than beside it. Fixed when the row is built,
    /// since it comes from the schema rather than from what was read.
    /// </summary>
    public bool IsBelow { get; }

    /// <summary>
    /// Where the two halves sit in the row's two by two grid. Below puts the name across
    /// both columns and the editor on the second line under it, and beside is the first
    /// line in both columns.
    /// </summary>
    public int HeadSpan => IsBelow ? 2 : 1;

    public int BodyRow => IsBelow ? 1 : 0;

    public int BodyColumn => IsBelow ? 0 : 1;

    public int BodySpan => IsBelow ? 2 : 1;

    /// <summary>The space under the note. Nothing beside a name, since the row is one line.</summary>
    public Thickness BodyGap => IsBelow ? new Thickness(0, 7, 0, 0) : default;

    /// <summary>
    /// The indent that lines a body with no gutter up with the editors above it. There is
    /// nothing to line up with under a name, so it goes.
    /// </summary>
    public Thickness BodyIndent => IsBelow ? BodyGap : new Thickness(66, 0, 0, 0);
}

/// <summary>
/// A row an app supplied for itself. Read only, so it stages nothing and saves nothing.
/// </summary>
public sealed partial class SettingsReadoutRowViewModel : SettingsRowViewModel
{
    [ObservableProperty]
    private IReadOnlyList<SettingsListEntry> _entries = [];

    public SettingsReadoutRowViewModel(SettingsReadoutRow row)
        : base(row)
    {
        ArgumentNullException.ThrowIfNull(row);

        IsList = row.Style == SettingsReadoutStyle.List;
    }

    /// <summary>
    /// Which of the two presentations the row asked for. Fixed when the row is built,
    /// since it comes from the schema rather than from what was read.
    /// </summary>
    public bool IsList { get; }
}

/// <summary>
/// A row an app supplied and edits itself. The page loads, stages and saves it with
/// every other row, and the app supplies the template that draws the editor.
/// </summary>
public sealed class SettingsEditorRowViewModel : SettingsRowViewModel
{
    public SettingsEditorRowViewModel(SettingsEditorRow row, ISettingsEditor editor)
        : base(row)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(editor);

        Editor = editor;
    }

    public ISettingsEditor Editor { get; }
}
