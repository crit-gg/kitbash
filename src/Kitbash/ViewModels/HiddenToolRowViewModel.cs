using CommunityToolkit.Mvvm.ComponentModel;

namespace Kitbash.ViewModels;

/// <summary>One tool Kitbash found, and whether the Open in menu offers it.</summary>
public sealed partial class HiddenToolRowViewModel : ObservableObject
{
    private readonly Action _changed;

    private bool _isOffered = true;

    [ObservableProperty]
    private bool _canEdit = true;

    public HiddenToolRowViewModel(string id, string name, string program, bool isOffered, Action changed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(changed);

        Id = id;
        Name = name;
        Program = program;
        _isOffered = isOffered;
        _changed = changed;
    }

    public string Id { get; }

    public string Name { get; }

    /// <summary>The program behind the name, so two builds of one IDE are told apart.</summary>
    public string Program { get; }

    /// <summary>Off keeps this tool out of the menu, and its id goes in the hidden list.</summary>
    public bool IsOffered
    {
        get => _isOffered;
        set
        {
            if (_isOffered == value)
            {
                return;
            }

            _isOffered = value;
            OnPropertyChanged();
            _changed();
        }
    }
}
