namespace Workbench.Core;

/// <summary>
/// A single tool that the launcher can list and open.
/// </summary>
public interface ITool
{
    /// <summary>Stable identifier, used for recents and persisted state.</summary>
    string Id { get; }

    /// <summary>Name shown in the launcher list.</summary>
    string Name { get; }

    /// <summary>One line summary shown next to the name.</summary>
    string Description { get; }

    /// <summary>Grouping label for the launcher sidebar.</summary>
    string Category { get; }

    /// <summary>
    /// What happens when the tool is opened. The launcher does not know or care what
    /// this does, only that it can be asked to do it.
    /// </summary>
    IToolActivation Activation { get; }
}
