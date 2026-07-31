namespace Workbench.Core;

/// <summary>A tool the launcher can list and open.</summary>
public interface ITool
{
    /// <summary>Stable identifier. Used for recents and stored state.</summary>
    string Id { get; }

    /// <summary>Name shown in the launcher list.</summary>
    string Name { get; }

    /// <summary>One line summary shown next to the name.</summary>
    string Description { get; }

    /// <summary>Grouping label for the launcher sidebar.</summary>
    string Category { get; }

    /// <summary>What happens when the tool is opened.</summary>
    IToolActivation Activation { get; }
}
