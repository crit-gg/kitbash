namespace Workbench.Core.Platform;

/// <summary>
/// A process to start, described without reference to System.Diagnostics so the
/// runner can be replaced.
/// </summary>
public sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    bool UseShellExecute)
{
    /// <summary>Hands the target to its registered handler.</summary>
    public static ProcessRequest Shell(string target) => new(target, [], UseShellExecute: true);

    /// <summary>Runs a named program with arguments passed separately, so no quoting is needed.</summary>
    public static ProcessRequest Command(string fileName, params string[] arguments) =>
        new(fileName, arguments, UseShellExecute: false);
}
