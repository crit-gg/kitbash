namespace Workbench.Core.Platform;

/// <summary>
/// Reading things over http. The one seam between this app and a network, so nothing
/// above it names a network type.
/// </summary>
public interface IWebContent
{
    /// <summary>The body as text. For the small documents this app reads, never a build.</summary>
    Task<string> ReadTextAsync(WebAddress address, CancellationToken cancellationToken);

    /// <summary>
    /// How large the file at an address is, or null when the server will not say.
    /// </summary>
    Task<long?> MeasureAsync(WebAddress address, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches to a file, reporting bytes written as it goes. The file is written where it
    /// is asked for and nothing else is created, so a caller that wants a temporary name
    /// and a rename does that itself.
    /// </summary>
    Task DownloadAsync(
        WebAddress address,
        string path,
        IProgress<long>? progress,
        CancellationToken cancellationToken);
}
