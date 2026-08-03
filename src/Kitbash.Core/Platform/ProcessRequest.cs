namespace Kitbash.Core.Platform;

/// <summary>
/// A process to start, described without reference to System.Diagnostics so the
/// runner can be replaced.
/// </summary>
/// <param name="WorkingDirectory">
/// Where the program runs. Null leaves it to the operating system. A program asked about
/// a directory should be run in it rather than told about it in an argument, since that
/// is the one form every program agrees on.
/// </param>
/// <param name="Environment">
/// Variables added to the ones the program would have inherited. An empty value unsets a
/// variable rather than setting it to nothing, which is how a program is told to stop doing
/// something its environment would otherwise ask for.
/// </param>
public sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    bool UseShellExecute,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null)
{
    /// <summary>Hands the target to its registered handler.</summary>
    public static ProcessRequest Shell(string target) => new(target, [], UseShellExecute: true);

    /// <summary>Runs a named program with arguments passed separately, so no quoting is needed.</summary>
    public static ProcessRequest Command(string fileName, params string[] arguments) =>
        new(fileName, arguments, UseShellExecute: false);

    /// <summary>The same, run inside a directory.</summary>
    public static ProcessRequest CommandIn(string workingDirectory, string fileName, params string[] arguments) =>
        new(fileName, arguments, UseShellExecute: false, workingDirectory);

    /// <summary>The same again, with variables set for this run only.</summary>
    public ProcessRequest With(IReadOnlyDictionary<string, string> environment) =>
        this with { Environment = environment };
}
