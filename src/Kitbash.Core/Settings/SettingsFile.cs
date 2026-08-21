namespace Kitbash.Core.Settings;

/// <summary>
/// One settings file read without throwing, so a caller can report what is wrong with
/// it rather than fail.
/// </summary>
internal sealed record SettingsFile(
    string Path,
    bool Exists,
    SettingsDocument Document,
    string? ParseError)
{
    /// <summary>
    /// Whether the content is what failed. False for a file that would not open at all,
    /// which may be a good file behind a lock rather than a broken one.
    /// </summary>
    public bool WillNotParse { get; init; }
}
