using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Settings;
using Kitbash.Tools;

namespace Kitbash.ViewModels;

/// <summary>One installed tool and what starting it does to the launcher.</summary>
public sealed partial class ToolActionRowViewModel : ObservableObject
{
    private readonly Action _changed;

    /// <summary>The row is marking its own options, so a mark is not a person's choice.</summary>
    private bool _marking;

    private AfterLaunchAction? _action;

    [ObservableProperty]
    private bool _canEdit = true;

    public ToolActionRowViewModel(ToolId id, string name, AfterLaunchAction? action, Action changed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(changed);

        Id = id;
        Name = name;
        _changed = changed;

        Options =
        [
            Option(null, "Default"),
            Option(AfterLaunchAction.DoNothing, "Do nothing"),
            Option(AfterLaunchAction.Minimize, "Minimize"),
            Option(AfterLaunchAction.Close, "Close"),
        ];

        _action = action;
        Mark();
    }

    public ToolId Id { get; }

    public string Name { get; }

    /// <summary>Four options, and the first of them is following the default.</summary>
    public ObservableCollection<ToolActionOptionViewModel> Options { get; }

    /// <summary>Null follows the default, which is what a tool with no row of its own does.</summary>
    public AfterLaunchAction? Action
    {
        get => _action;
        set
        {
            if (_action == value)
            {
                return;
            }

            _action = value;
            Mark();
            _changed();
        }
    }

    private ToolActionOptionViewModel Option(AfterLaunchAction? action, string label)
    {
        var option = new ToolActionOptionViewModel(action, label);

        option.PropertyChanged += OnOptionChanged;

        return option;
    }

    private void OnOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_marking
            || e.PropertyName is not nameof(ToolActionOptionViewModel.IsChosen)
            || sender is not ToolActionOptionViewModel { IsChosen: true } option)
        {
            return;
        }

        Action = option.Action;
    }

    private void Mark()
    {
        _marking = true;

        try
        {
            foreach (var option in Options)
            {
                option.IsChosen = option.Action == _action;
            }
        }
        finally
        {
            _marking = false;
        }
    }
}

/// <summary>One segment in a tool's row. Null is the segment that follows the default.</summary>
public sealed partial class ToolActionOptionViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isChosen;

    public ToolActionOptionViewModel(AfterLaunchAction? action, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        Action = action;
        Label = label;
    }

    public AfterLaunchAction? Action { get; }

    public string Label { get; }

    public override string ToString() => Label;
}
