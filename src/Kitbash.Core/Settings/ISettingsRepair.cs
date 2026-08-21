namespace Kitbash.Core.Settings;

/// <summary>
/// Deals with a settings file that will not parse. An app reads its own files before it
/// has a window to report a failure in, so a broken one is replaced rather than carried.
/// </summary>
public interface ISettingsRepair
{
    /// <summary>
    /// Replaces a file whose content will not parse, leaving none in its place, so the
    /// next read sees a file that was never there. A file that reads, one that is missing,
    /// and one that would not open at all are all left exactly as they are.
    /// </summary>
    /// <param name="path">The file to check.</param>
    /// <returns>Where the broken file was put, or null when nothing was replaced.</returns>
    string? Replace(string path);
}
