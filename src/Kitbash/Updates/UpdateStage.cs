namespace Kitbash.Updates;

/// <summary>
/// One moment of an update, in the three things the splash shows.
/// </summary>
/// <param name="Status">The line a person reads.</param>
/// <param name="Fraction">How far along, from 0 to 1, or null when there is no telling.</param>
/// <param name="Detail">The readout on the right, or blank.</param>
public sealed record UpdateStage(string Status, double? Fraction, string Detail);
