namespace Kitbash.Tools;

/// <summary>Where an install has got to. The order is the order they happen in.</summary>
public enum ToolInstallStage
{
    Downloading,

    /// <summary>Hashing the payload against what the manifest published.</summary>
    Verifying,

    Extracting,

    Done,
}

/// <summary>
/// How far one install has got, for a card to draw.
/// </summary>
/// <param name="Bytes">Bytes done in this stage, where that means anything.</param>
/// <param name="Total">Bytes expected, or zero when the manifest did not say.</param>
public sealed record ToolInstallProgress(ToolInstallStage Stage, long Bytes = 0, long Total = 0)
{
    /// <summary>
    /// Zero to one, or null when there is no total to measure against. A null draws an
    /// indeterminate bar rather than a bar stuck at nothing.
    /// </summary>
    public double? Fraction => Total > 0 ? Math.Clamp((double)Bytes / Total, 0, 1) : null;
}
