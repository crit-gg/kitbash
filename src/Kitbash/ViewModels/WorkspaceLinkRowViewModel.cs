using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Core.Platform;
using Kitbash.Ui.Controls;
using Kitbash.Workspaces;

namespace Kitbash.ViewModels;

/// <summary>One glyph as the icon dropdown draws it.</summary>
public sealed record WorkspaceLinkIcon(IconGlyph Glyph, string Name);

/// <summary>
/// One row of the workspace links editor. It holds what a person typed, whether or not it
/// is usable, so a row the launcher cannot draw is still a row here.
/// </summary>
public sealed partial class WorkspaceLinkRowViewModel : SettingsListRow
{
    /// <summary>What the file spelled, kept until the dropdown is actually moved.</summary>
    private string _stored;

    private bool _picked;

    private (string Label, string Address, WorkspaceLinkIcon Icon, string Stored, bool Picked)? _before;

    [ObservableProperty]
    private string _label;

    [ObservableProperty]
    private string _address;

    [ObservableProperty]
    private WorkspaceLinkIcon _icon;

    public WorkspaceLinkRowViewModel(
        string label,
        string address,
        WorkspaceLinkIcon icon,
        string storedIcon,
        IReadOnlyList<WorkspaceLinkIcon> icons)
    {
        ArgumentNullException.ThrowIfNull(icons);

        _label = label;
        _address = address;
        _icon = icon;
        _stored = storedIcon;

        Icons = icons;
    }

    /// <summary>The whole set, since a glyph a workspace may name is any of them.</summary>
    public IReadOnlyList<WorkspaceLinkIcon> Icons { get; }

    /// <summary>
    /// Why this row cannot be kept, in a person's words. The address is judged the way the
    /// launcher judges it, so a row that is kept is a row the page will draw.
    /// </summary>
    public override string Problem
    {
        get
        {
            if (Label.Trim().Length == 0)
            {
                return "Give this link a name.";
            }

            if (Address.Trim().Length == 0)
            {
                return "Give this link an address.";
            }

            try
            {
                WebAddress.Parse(Address.Trim());
                return string.Empty;
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return "This has to be a whole web address, starting with http or https.";
            }
        }
    }

    /// <summary>
    /// What the file gets, trimmed the way it is judged. A row nobody repicked keeps the
    /// icon exactly as the file spelled it, blank included, so a save writes no key in
    /// where there was none.
    /// </summary>
    public WorkspaceLinkEntry Entry => new(Label.Trim(), Address.Trim(), _picked ? Icon.Name : _stored);

    public override void BeginEdit() => _before = (Label, Address, Icon, _stored, _picked);

    public override void CancelEdit()
    {
        if (_before is not { } before)
        {
            return;
        }

        Label = before.Label;
        Address = before.Address;
        Icon = before.Icon;
        _stored = before.Stored;
        _picked = before.Picked;
        _before = null;
    }

    public override void EndEdit() => _before = null;

    partial void OnLabelChanged(string value) => Announce();

    partial void OnAddressChanged(string value) => Announce();

    partial void OnIconChanged(WorkspaceLinkIcon value)
    {
        _picked = true;
        Announce();
    }
}
