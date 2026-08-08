using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Core.Platform;
using Kitbash.Ui.Controls;
using Kitbash.Workspaces;

namespace Kitbash.ViewModels;

/// <summary>One glyph as the icon dropdown draws it.</summary>
public sealed record WorkspaceLinkIcon(IconGlyph Glyph, string Name);

/// <summary>
/// One row of the workspace links editor. It holds what a person typed, whether or not
/// it is usable, so a row that says nothing yet stops the save rather than disappearing.
/// </summary>
public sealed partial class WorkspaceLinkRowViewModel : ObservableObject
{
    private readonly Action _changed;
    private readonly Action<WorkspaceLinkRowViewModel> _remove;

    /// <summary>What the file spelled, kept until the dropdown is actually moved.</summary>
    private readonly string _stored;

    private bool _picked;

    [ObservableProperty]
    private string _label;

    [ObservableProperty]
    private string _address;

    [ObservableProperty]
    private WorkspaceLinkIcon _icon;

    [ObservableProperty]
    private bool _canEdit = true;

    public WorkspaceLinkRowViewModel(
        string label,
        string address,
        WorkspaceLinkIcon icon,
        string storedIcon,
        IReadOnlyList<WorkspaceLinkIcon> icons,
        Action changed,
        Action<WorkspaceLinkRowViewModel> remove)
    {
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(remove);

        _label = label;
        _address = address;
        _icon = icon;
        _stored = storedIcon;
        _changed = changed;
        _remove = remove;

        Icons = icons;
    }

    /// <summary>The whole set, since a glyph a workspace may name is any of them.</summary>
    public IReadOnlyList<WorkspaceLinkIcon> Icons { get; }

    public bool IsValid => Problem.Length == 0;

    public bool HasProblem => Problem.Length > 0 && CanEdit;

    /// <summary>
    /// Why this row cannot be written, in a person's words, or blank when it can be. The
    /// address is judged the way the launcher judges it, so a row that saves is a row the
    /// page will draw.
    /// </summary>
    public string Problem
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

    partial void OnLabelChanged(string value) => Announce();

    partial void OnAddressChanged(string value) => Announce();

    partial void OnIconChanged(WorkspaceLinkIcon value)
    {
        _picked = true;
        Announce();
    }

    partial void OnCanEditChanged(bool value) => OnPropertyChanged(nameof(HasProblem));

    [RelayCommand]
    private void Remove() => _remove(this);

    private void Announce()
    {
        OnPropertyChanged(nameof(Problem));
        OnPropertyChanged(nameof(HasProblem));
        OnPropertyChanged(nameof(IsValid));

        _changed();
    }
}
