namespace Kitbash.Core.Platform;

/// <summary>
/// A desktop with no colour picking of its own. Windows takes this: it has no portal, so the
/// gesture would have to be a full screen window over a capture of the desktop, which is not
/// built.
/// </summary>
public sealed class NoScreenColour : IScreenColour
{
    public bool CanPick => false;

    public Task<ScreenColour?> PickAsync(CancellationToken cancellation = default) =>
        Task.FromResult<ScreenColour?>(null);
}
