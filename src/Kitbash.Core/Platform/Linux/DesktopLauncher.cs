namespace Kitbash.Core.Platform.Linux;

/// <summary>
/// A program that hands a target to the desktop. Some take the target directly, and
/// some need a leading argument such as <c>gio open</c>.
/// </summary>
internal sealed record DesktopLauncher(string FileName, IReadOnlyList<string> LeadingArguments)
{
    public IReadOnlyList<string> ArgumentsFor(string target) => [.. LeadingArguments, target];
}
