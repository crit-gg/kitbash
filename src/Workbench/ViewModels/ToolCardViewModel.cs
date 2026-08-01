using Workbench.Core;

namespace Workbench.ViewModels;

/// <summary>A tool as the launcher lists it.</summary>
/// <remarks>
/// Everything here comes from the registry. A card says what a tool is and offers one
/// action. It does not report the tool's state or its contents.
/// <para>
/// There is no version and no mark, because <see cref="ITool"/> carries neither. The
/// design draws a letter in the card's square and that is a placeholder for the tool's
/// own icon rather than a lettermark to build, so the square stays empty until a tool
/// supplies one. Adding a version and an icon to the contract changes
/// <see cref="ITool"/> and belongs in its own commit.
/// </para>
/// </remarks>
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
