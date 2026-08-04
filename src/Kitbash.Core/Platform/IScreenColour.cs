namespace Kitbash.Core.Platform;

/// <summary>One colour read off the screen. Each channel is 0 to 1.</summary>
public sealed record ScreenColour(double Red, double Green, double Blue);

/// <summary>
/// Picking a colour off the screen. The gesture belongs to the desktop rather than to the
/// app, since a screen is not ours to read: Wayland refuses it outright and hands the job to
/// the portal instead.
/// </summary>
public interface IScreenColour
{
    /// <summary>
    /// Whether this desktop can run the gesture at all. A caller draws no eyedropper when it
    /// cannot, rather than one that fails when it is pressed.
    /// </summary>
    bool CanPick { get; }

    /// <summary>
    /// Lets a person point at something on screen and gives back what they pointed at, or
    /// null when they dropped it or the desktop refused.
    /// </summary>
    Task<ScreenColour?> PickAsync(CancellationToken cancellation = default);
}
