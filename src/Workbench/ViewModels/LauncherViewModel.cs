using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Workbench.Core;

namespace Workbench.ViewModels;

public partial class LauncherViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenSelectedCommand))]
    private ITool? _selectedTool;

    [ObservableProperty]
    private string _status = "Select a tool to begin.";

    public LauncherViewModel(IToolRegistry registry)
    {
        Tools = new ObservableCollection<ITool>(registry.Tools);
        SelectedTool = Tools.FirstOrDefault();
    }

    /// <summary>Parameterless overload so the XAML previewer has something to bind to.</summary>
    public LauncherViewModel() : this(new ToolRegistry())
    {
    }

    public ObservableCollection<ITool> Tools { get; }

    public string Title => "Workbench";

    private bool CanOpenSelected() => SelectedTool is not null;

    // The launcher hands off to the tool's activation and reports what came back.
    // It deliberately does not know whether that started a process, ran a script or
    // opened a web page.
    [RelayCommand(CanExecute = nameof(CanOpenSelected))]
    private async Task OpenSelectedAsync(CancellationToken cancellationToken)
    {
        var tool = SelectedTool!;
        var result = await tool.Activation.ActivateAsync(cancellationToken);

        if (result.Message is { Length: > 0 } message)
        {
            Status = message;
        }
        else if (result.Succeeded)
        {
            Status = $"Opened '{tool.Name}'.";
        }
        else
        {
            Status = $"Could not open '{tool.Name}'.";
        }
    }
}
