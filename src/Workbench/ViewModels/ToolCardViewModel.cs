namespace Workbench.ViewModels;

/// <summary>An installed tool as the launcher lists it.</summary>
public sealed class ToolCardViewModel
{
    public required string Mark { get; init; }

    public required string Name { get; init; }

    public required string State { get; init; }

    public required string Version { get; init; }

    public required string Description { get; init; }
}
