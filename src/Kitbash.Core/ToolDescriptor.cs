namespace Kitbash.Core;

/// <summary>Tool metadata paired with the activation that opens it.</summary>
public sealed record ToolDescriptor(
    string Id,
    string Name,
    string Description,
    string Category,
    IToolActivation Activation) : ITool;
