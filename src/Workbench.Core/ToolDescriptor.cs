namespace Workbench.Core;

/// <summary>
/// Plain description of a tool, pairing its launcher facing metadata with the
/// activation that opens it.
/// </summary>
public sealed record ToolDescriptor(
    string Id,
    string Name,
    string Description,
    string Category,
    IToolActivation Activation) : ITool;
