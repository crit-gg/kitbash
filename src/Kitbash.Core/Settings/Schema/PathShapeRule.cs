namespace Kitbash.Core.Settings.Schema;

/// <summary>What a path setting points at.</summary>
public enum PathKind
{
    /// <summary>Either, so only the shape of the string is checked.</summary>
    Any = 0,

    File = 1,

    Directory = 2,
}

/// <summary>
/// The string has to look like a path. Shape only. Whether anything is there is an
/// <see cref="ISettingProbe"/>, since the answer changes after the value is written.
/// </summary>
public sealed class PathShapeRule : ISettingRule<string>
{
    /// <param name="allowEmpty">
    /// For a setting where blank is an answer rather than a gap, such as an override that
    /// falls back to whatever the app would have worked out on its own.
    /// </param>
    public PathShapeRule(PathKind kind = PathKind.Any, bool mustBeRooted = true, bool allowEmpty = false)
    {
        Kind = kind;
        MustBeRooted = mustBeRooted;
        AllowEmpty = allowEmpty;
    }

    public PathKind Kind { get; }

    /// <summary>Rooted, so the value never depends on which directory a process started in.</summary>
    public bool MustBeRooted { get; }

    /// <summary>Blank is allowed, and means the setting is not answering.</summary>
    public bool AllowEmpty { get; }

    public string Summary => AllowEmpty ? $"{Shape}, or blank" : Shape;

    private string Shape => (Kind, MustBeRooted) switch
    {
        (PathKind.File, true) => "A full path to a file",
        (PathKind.File, false) => "A path to a file",
        (PathKind.Directory, true) => "A full path to a folder",
        (PathKind.Directory, false) => "A path to a folder",
        (_, true) => "A full path",
        _ => "A path",
    };

    public bool Allows(string value, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value))
        {
            if (AllowEmpty)
            {
                reason = null;
                return true;
            }

            reason = "This cannot be empty.";
            return false;
        }

        if (value.AsSpan().IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            reason = $"'{value}' holds characters that cannot appear in a path.";
            return false;
        }

        if (MustBeRooted && !Path.IsPathRooted(value))
        {
            reason = $"'{value}' has to be a full path.";
            return false;
        }

        if (Kind is PathKind.File && string.IsNullOrEmpty(Path.GetFileName(value)))
        {
            reason = $"'{value}' names a folder rather than a file.";
            return false;
        }

        reason = null;
        return true;
    }
}
