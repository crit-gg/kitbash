namespace Kitbash.Updates;

/// <summary>
/// How Kitbash replaces itself with a newer copy. The launcher's alone, since a tool
/// never checks for its own update.
/// </summary>
public interface IApplicationUpdates
{
    /// <summary>Where releases are read from, for showing. Blank when nowhere.</summary>
    string Feed { get; }

    /// <summary>
    /// What the feed offers, or null when it offers nothing, the feed is blank, this copy
    /// was not installed, the check failed, or <paramref name="cancellation"/> ran out.
    /// **Never throws, cancellation included**, because the app has to start either way.
    /// Reads the network, so call it off the UI thread.
    /// </summary>
    Task<AvailableUpdate?> CheckAsync(CancellationToken cancellation = default);

    /// <summary>
    /// Fetches it, reporting whole percentages.
    /// </summary>
    /// <exception cref="InvalidOperationException">The download failed.</exception>
    Task DownloadAsync(
        AvailableUpdate update, IProgress<int> progress, CancellationToken cancellation = default);

    /// <summary>
    /// Swaps in what was downloaded and starts the new copy. Does not return when it
    /// works.
    /// </summary>
    /// <exception cref="InvalidOperationException">Nothing was applied.</exception>
    void ApplyAndRestart(AvailableUpdate update);
}
