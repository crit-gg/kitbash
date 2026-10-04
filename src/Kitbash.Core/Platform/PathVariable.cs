namespace Kitbash.Core.Platform;

/// <summary>
/// A PATH style list of folders, held as written. Adding and removing leave every other
/// entry exactly as it was, unexpanded variables and odd spellings included.
/// </summary>
public sealed class PathVariable
{
    private readonly IReadOnlyList<string> _entries;
    private readonly char _separator;

    private PathVariable(IReadOnlyList<string> entries, char separator)
    {
        _entries = entries;
        _separator = separator;
    }

    /// <summary>The entries in order, with empty ones dropped.</summary>
    public IReadOnlyList<string> Entries => _entries;

    /// <param name="value">The variable as stored. Null or blank is an empty list.</param>
    /// <param name="separator">Semicolon on Windows, colon elsewhere.</param>
    public static PathVariable Parse(string? value, char separator)
    {
        var entries = (value ?? string.Empty)
            .Split(separator)
            .Where(entry => entry.Trim().Length > 0)
            .ToList();

        return new PathVariable(entries, separator);
    }

    /// <param name="same">Whether an entry, as written, names the folder.</param>
    public bool Contains(Func<string, bool> same)
    {
        ArgumentNullException.ThrowIfNull(same);

        return _entries.Any(same);
    }

    /// <summary>The list with the folder on the end, or this list when it is there already.</summary>
    public PathVariable With(string folder, Func<string, bool> same)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        return Contains(same) ? this : new PathVariable([.. _entries, folder], _separator);
    }

    /// <summary>The list without any entry naming the folder.</summary>
    public PathVariable Without(Func<string, bool> same)
    {
        ArgumentNullException.ThrowIfNull(same);

        return new PathVariable([.. _entries.Where(entry => !same(entry))], _separator);
    }

    public override string ToString() => string.Join(_separator, _entries);
}
