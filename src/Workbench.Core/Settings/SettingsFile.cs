namespace Workbench.Core.Settings;

/// <summary>
/// One settings file read without throwing, so a caller can report what is wrong with
/// it rather than fail.
/// </summary>
internal sealed record SettingsFile(
    string Path,
    bool Exists,
    SettingsDocument Document,
    string? ParseError);
