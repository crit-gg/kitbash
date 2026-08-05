using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Core.Platform.Openers;

namespace Kitbash.ViewModels;

/// <summary>One tool a person added, while it is being edited.</summary>
public sealed partial class CustomToolRowViewModel : ObservableObject
{
    private readonly Action _changed;
    private readonly Action<CustomToolRowViewModel> _remove;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _path;

    [ObservableProperty]
    private string _arguments;

    [ObservableProperty]
    private bool _canEdit = true;

    public CustomToolRowViewModel(
        string name,
        string path,
        string arguments,
        Action changed,
        Action<CustomToolRowViewModel> remove)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(remove);

        _name = name;
        _path = path;
        _arguments = arguments;
        _changed = changed;
        _remove = remove;
    }

    /// <summary>The tool this row would be written as, or null while it says nothing usable.</summary>
    public CustomOpener? Opener =>
        string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Path)
            ? null
            : new CustomOpener(Name.Trim(), Path.Trim(), Arguments.Trim());

    public bool IsValid => Opener is not null;

    /// <summary>Said under the row, so a person is told why Save will not go.</summary>
    public string Problem
    {
        get
        {
            // A row nobody has typed in yet still stops the save, and it says so by being
            // empty rather than by going red the moment it appears.
            if (IsValid || (string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Path)))
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(Name)
                ? "Give this tool a name."
                : "Say which program to run.";
        }
    }

    public bool HasProblem => Problem.Length > 0;

    [RelayCommand]
    private void Remove() => _remove(this);

    partial void OnNameChanged(string value) => Announce();

    partial void OnPathChanged(string value) => Announce();

    partial void OnArgumentsChanged(string value) => Announce();

    private void Announce()
    {
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(Problem));
        OnPropertyChanged(nameof(HasProblem));
        _changed();
    }
}
