namespace Workbench.Core;

/// <summary>
/// Source of the tools the launcher offers. Registration is explicit rather than
/// assembly scanned, so adding a tool is a visible code change.
/// </summary>
public interface IToolRegistry
{
    IReadOnlyList<ITool> Tools { get; }
}

public sealed class ToolRegistry : IToolRegistry
{
    private readonly List<ITool> _tools = [];

    public IReadOnlyList<ITool> Tools => _tools;

    public ToolRegistry Add(ITool tool)
    {
        _tools.Add(tool);
        return this;
    }
}
