namespace Kitbash.Tools;

/// <summary>An installed tool the open workspace provides, and what provides it.</summary>
/// <param name="Workspaces">
/// Every workspace whose own list names the repository, which is what the mark on the card
/// says. Empty when the global list provides it, since then it is not a workspace's tool.
/// </param>
public sealed record ProvidedTool(InstalledTool Tool, IReadOnlyList<string> Workspaces);
