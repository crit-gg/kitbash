using Avalonia.Platform.Storage;

namespace Kitbash.Ui.Controls;

/// <summary>
/// One entry in a <see cref="PathField"/>'s filter list. A name a person reads and the
/// extensions it takes, written as ".json, .csv" or as "json csv".
/// </summary>
/// <example><code>&lt;ui:PathFilter Name="Data files" Extensions=".json, .csv" /&gt;</code></example>
public class PathFilter
{
    private static readonly char[] Separators = [',', ' ', ';'];

    /// <summary>What the dialog calls this filter.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Extensions, separated by commas or spaces. Blank takes any file.</summary>
    public string Extensions { get; set; } = string.Empty;

    /// <summary>The extensions, each with its leading dot. Empty means anything goes.</summary>
    public IReadOnlyList<string> Takes =>
    [
        .. Extensions
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => entry.StartsWith('.') ? entry : $".{entry}"),
    ];

    /// <summary>
    /// Whether this filter would take that path. Case is ignored on both platforms, since a
    /// filter describes the shape of a name rather than a file that is there.
    /// </summary>
    public bool Accepts(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var takes = Takes;

        return takes.Count == 0
            || takes.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The dialog's own form of this filter. Patterns only, which is what Windows and Linux
    /// both read. A macOS build would need uniform type identifiers as well.
    /// </summary>
    public FilePickerFileType ForDialog()
    {
        var takes = Takes;

        return new FilePickerFileType(string.IsNullOrWhiteSpace(Name) ? "Files" : Name)
        {
            Patterns = takes.Count == 0 ? ["*"] : [.. takes.Select(extension => $"*{extension}")],
        };
    }
}
