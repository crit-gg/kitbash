namespace Kitbash.Core.Platform;

/// <summary>
/// What a process left behind. Held as text rather than streams, so a caller reads a
/// result instead of managing a pipe.
/// </summary>
/// <param name="ExitCode">Zero when the program says it succeeded.</param>
/// <param name="StandardOutput">Everything the program wrote to output.</param>
/// <param name="StandardError">Everything it wrote to error, kept even on success.</param>
public sealed record ProcessOutput(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>Output split into lines, with the trailing blank one dropped.</summary>
    public IReadOnlyList<string> Lines =>
        StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToArray();
}
