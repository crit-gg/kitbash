using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Core.Platform;
using Kitbash.Tools;

namespace Kitbash.ViewModels;

/// <summary>One repository in the settings window, while it is being edited.</summary>
public sealed partial class ToolRepositoryRowViewModel : ObservableObject
{
    private readonly Action _changed;
    private readonly Action<ToolRepositoryRowViewModel> _remove;

    [ObservableProperty]
    private string _kind;

    [ObservableProperty]
    private string _address;

    [ObservableProperty]
    private bool _canEdit = true;

    /// <param name="kind">
    /// The type as the file spells it. A type Kitbash does not know is still offered, so
    /// a file naming a forge this copy cannot read survives being saved.
    /// </param>
    public ToolRepositoryRowViewModel(
        string kind,
        string address,
        Action changed,
        Action<ToolRepositoryRowViewModel> remove)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(remove);

        _kind = kind;
        _address = address;
        _changed = changed;
        _remove = remove;

        Kinds = kind.Length > 0 && !ToolRepositorySource.Kinds.Contains(kind, StringComparer.Ordinal)
            ? [.. ToolRepositorySource.Kinds, kind]
            : ToolRepositorySource.Kinds;
    }

    /// <summary>What the type dropdown offers, which is every kind plus this row's own.</summary>
    public IReadOnlyList<string> Kinds { get; }

    /// <summary>The entry this row would be written as, or null while it says nothing usable.</summary>
    public ToolRepositorySource? Source
    {
        get
        {
            if (Kind.Length == 0 || string.IsNullOrWhiteSpace(Address))
            {
                return null;
            }

            try
            {
                return new ToolRepositorySource(Kind, WebAddress.Parse(Address.Trim()), string.Empty);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return null;
            }
        }
    }

    public bool IsValid => Source is not null;

    /// <summary>Said under the field, so a person is told why Save will not go.</summary>
    public string Problem =>
        IsValid || string.IsNullOrWhiteSpace(Address)
            ? string.Empty
            : "This is not a web address. Use http or https.";

    public bool HasProblem => Problem.Length > 0;

    [RelayCommand]
    private void Remove() => _remove(this);

    partial void OnKindChanged(string value) => Announce();

    partial void OnAddressChanged(string value) => Announce();

    private void Announce()
    {
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(Problem));
        OnPropertyChanged(nameof(HasProblem));
        _changed();
    }
}
