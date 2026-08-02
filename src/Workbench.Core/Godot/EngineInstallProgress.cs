namespace Workbench.Core.Godot;

/// <summary>
/// How far one install has got, for a row to draw.
/// </summary>
/// <param name="Stage">What it is doing.</param>
/// <param name="Bytes">Bytes done in this stage, where that means anything.</param>
/// <param name="Total">Bytes expected, or zero when the server would not say.</param>
public sealed record EngineInstallProgress(EngineInstallStage Stage, long Bytes = 0, long Total = 0)
{
    /// <summary>
    /// Zero to one, or null when there is no total to measure against. A null draws an
    /// indeterminate bar rather than a bar stuck at nothing.
    /// </summary>
    public double? Fraction => Total > 0 ? Math.Clamp((double)Bytes / Total, 0, 1) : null;
}
