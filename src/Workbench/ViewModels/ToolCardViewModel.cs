using Workbench.Core;

namespace Workbench.ViewModels;

/// <summary>A tool as the launcher lists it.</summary>
public sealed class ToolCardViewModel
{
    public ToolCardViewModel(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        Tool = tool;
    }

    public ITool Tool { get; }

    public string Name => Tool.Name;

    public string Description => Tool.Description;
}
