namespace Workbench.Core.Settings;

/// <summary>
/// One settings file read without throwing, so a caller can report what is wrong with
/// it rather than fail.
/// </summary>
/// <remarks>
/// A file that would not parse comes back with an empty <see cref="Document"/> and a
/// <see cref="ParseError"/>. Those two look identical to a file that is genuinely
/// empty, which is why nothing may write a document back without checking
/// <see cref="ParseError"/> first. Doing so would replace a file full of hand written
/// values with almost nothing.
/// </remarks>
internal sealed record SettingsFile(
    string Path,
    bool Exists,
    SettingsDocument Document,
    string? ParseError);
