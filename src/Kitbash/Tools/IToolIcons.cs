namespace Kitbash.Tools;

/// <summary>
/// Where a tool's icon file is. An icon is decoration, so nothing here ever fails: a tool
/// without one, or one that could not be fetched, answers null and the card draws a letter.
/// </summary>
public interface IToolIcons
{
    /// <summary>The icon inside an installed version's folder, or null when it has none.</summary>
    string? For(InstalledTool tool);

    /// <summary>
    /// The icon for a version that is only offered, fetched once into the cache. A release
    /// asset never changes, so what is cached is never fetched again.
    /// </summary>
    Task<string?> ForAsync(OfferedTool tool, CancellationToken cancellationToken);

    /// <summary>
    /// Puts the icon in a version's folder while it is being installed, so an installed
    /// tool draws itself with nothing to download.
    /// </summary>
    Task FetchIntoAsync(OfferedTool tool, string directory, CancellationToken cancellationToken);
}
