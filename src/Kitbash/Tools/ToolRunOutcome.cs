namespace Kitbash.Tools;

/// <summary>How a script run ended.</summary>
/// <param name="ExitCode">Zero when the script says it worked.</param>
public sealed record ToolRunOutcome(int ExitCode)
{
    public bool Worked => ExitCode == 0;
}
