namespace Kitbash.Core.Platform.Openers;

/// <summary>
/// One thing inside the workspace a tool could open, such as a solution.
/// </summary>
/// <param name="Title">What the menu says, which is the path relative to the workspace.</param>
/// <param name="Arguments">Argv as passed, so nothing here needs quoting.</param>
public sealed record OpenChoice(string Title, IReadOnlyList<string> Arguments);
