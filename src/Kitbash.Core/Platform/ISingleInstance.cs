namespace Kitbash.Core.Platform;

/// <summary>
/// Keeps one copy of an application running for one person, and gives a second copy a way
/// to bring the first one forward instead of opening.
/// </summary>
public interface ISingleInstance : IDisposable
{
    /// <summary>
    /// True when this copy is the only one and may carry on. False means another copy
    /// already holds it and has been asked to come forward, so this one should exit
    /// without drawing anything.
    /// </summary>
    /// <remarks>
    /// A copy that cannot hand over is told to carry on, since a second window is a better
    /// answer than an app that will not open.
    /// </remarks>
    bool TryHold();

    /// <summary>
    /// A second copy asked this one to come forward. Raised on a background thread, so a
    /// handler that touches a window marshals for itself.
    /// </summary>
    event EventHandler? AskedToComeForward;
}
