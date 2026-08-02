namespace Workbench.Core.Settings.Schema;

/// <summary>A group of rows under one heading.</summary>
public sealed record SettingsSection
{
    public SettingsSection(string title, IReadOnlyList<ISettingsRow> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            throw new ArgumentException($"Section '{title}' has no rows.", nameof(rows));
        }

        foreach (var row in rows)
        {
            ArgumentNullException.ThrowIfNull(row);
        }

        Title = title;
        Rows = rows;
    }

    public string Title { get; }

    public IReadOnlyList<ISettingsRow> Rows { get; }
}
