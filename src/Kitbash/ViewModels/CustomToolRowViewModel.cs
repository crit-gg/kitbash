using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Core.Platform.Openers;

namespace Kitbash.ViewModels;

/// <summary>One tool a person added, while it is being edited.</summary>
public sealed partial class CustomToolRowViewModel : SettingsListRow
{
    private (string Name, string Path, string Arguments)? _before;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _path;

    [ObservableProperty]
    private string _arguments;

    public CustomToolRowViewModel(string name, string path, string arguments)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(arguments);

        _name = name;
        _path = path;
        _arguments = arguments;
    }

    /// <summary>The tool this row would be written as, or null while it says nothing usable.</summary>
    public CustomOpener? Opener =>
        string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Path)
            ? null
            : new CustomOpener(Name.Trim(), Path.Trim(), Arguments.Trim());

    /// <summary>Said under the row, so a person is told why it cannot be kept.</summary>
    public override string Problem
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return "Give this tool a name.";
            }

            return string.IsNullOrWhiteSpace(Path) ? "Say which program to run." : string.Empty;
        }
    }

    public override void BeginEdit() => _before = (Name, Path, Arguments);

    public override void CancelEdit()
    {
        if (_before is not { } before)
        {
            return;
        }

        Name = before.Name;
        Path = before.Path;
        Arguments = before.Arguments;
        _before = null;
    }

    public override void EndEdit() => _before = null;

    partial void OnNameChanged(string value) => Announce();

    partial void OnPathChanged(string value) => Announce();

    partial void OnArgumentsChanged(string value) => Announce();
}
