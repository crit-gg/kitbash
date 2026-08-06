namespace Kitbash.Tools;

/// <summary>
/// One line a script wrote, read as progress. Every part is optional, so a line that only
/// moves the bar leaves the words where they were.
/// </summary>
public sealed record ToolProgressStep
{
    /// <summary>What is happening now, in the sentence a person reads.</summary>
    public string? Stage { get; init; }

    /// <summary>The mono line under it, such as a count or the file being worked on.</summary>
    public string? Detail { get; init; }

    /// <summary>How far through, from 0 to 100. Null leaves the bar as it was.</summary>
    public double? Progress { get; init; }

    /// <summary>The script asked for the spinner back, which is progress with no number.</summary>
    public bool IsIndeterminate { get; init; }

    /// <summary>A line for the log under Details.</summary>
    public string? Log { get; init; }

    /// <summary>The line is the script complaining rather than reporting.</summary>
    public bool IsError { get; init; }

    /// <summary>Nothing on this line was worth showing.</summary>
    public bool IsEmpty =>
        Stage is null && Detail is null && Progress is null && !IsIndeterminate && Log is null;
}
