namespace Workbench.Core.Settings.Schema;

/// <summary>Which layer a value came from, or that it came from none of them.</summary>
public enum SettingOrigin
{
    /// <summary>No file held a usable value, so the descriptor's own default is in force.</summary>
    Default = 0,

    /// <summary>The workspace file committed to git.</summary>
    TeamShared = 1,

    /// <summary>One person's file for this workspace.</summary>
    User = 2,

    /// <summary>The single file of a home that does not layer.</summary>
    File = 3,

    /// <summary>
    /// A file held a value and it could not be used, so the setting fell back. The
    /// message says why. This is the one origin that is a problem rather than a place.
    /// </summary>
    Invalid = 4,
}

/// <summary>One file behind a page, and whether it can be read.</summary>
/// <remarks>
/// A file that will not parse reads as empty, so every setting on the page falls back to
/// its default without saying so. <see cref="ParseError"/> is the only thing that tells
/// the difference, and it is also what stops the file being written back.
/// </remarks>
public sealed record SettingsFileView(
    SettingsLayer? Layer,
    string Path,
    bool Exists,
    string? ParseError)
{
    public bool CanBeWritten => ParseError is null;
}

/// <summary>What one layer holds for one setting, and how it fared.</summary>
public sealed record SettingLayerValue(SettingsLayer? Layer, object? Raw, SettingCheck Check);

/// <summary>
/// One setting as it stands right now. <see cref="Layers"/> is what makes the origin
/// worth drawing: it says which layer won, that a layer below also holds a value, and
/// that a stored value could not be used.
/// </summary>
public sealed record SettingValueView(
    ISettingDescriptor Row,
    object? Effective,
    SettingOrigin Origin,
    bool DiffersFromDefault,
    string? Problem,
    IReadOnlyList<SettingLayerValue> Layers)
{
    /// <summary>
    /// Whether resetting would do anything. Reset removes the key rather than writing
    /// the default, so there has to be a key somewhere to remove.
    /// </summary>
    public bool CanReset => Layers.Any(layer => layer.Check.Result is not SettingCheckResult.Absent);
}

/// <summary>
/// One page read off disk. Produced away from the UI thread, since every file behind the
/// page is opened to make it.
/// </summary>
/// <param name="IsAvailable">
/// False when nothing is composed for the page's home, such as a workspace page with no
/// workspace open. There are no files and no values in that case.
/// </param>
public sealed record SettingsPageView(
    SettingsPage Page,
    bool IsAvailable,
    IReadOnlyList<SettingsFileView> Files,
    IReadOnlyList<SettingValueView> Values)
{
    /// <summary>
    /// A file behind this page could not be read. Whether that stops the whole page or
    /// only its own layer is the reader's call. Both are answerable from
    /// <see cref="Files"/>, since the error is recorded per file.
    /// </summary>
    public bool HasParseError => Files.Any(file => file.ParseError is not null);

    /// <summary>Null when the page does not declare the key.</summary>
    public SettingValueView? Find(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return Values.FirstOrDefault(value => string.Equals(value.Row.Key, key, StringComparison.Ordinal));
    }
}
