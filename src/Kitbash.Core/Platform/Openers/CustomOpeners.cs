using Kitbash.Core.Settings;

namespace Kitbash.Core.Platform.Openers;

/// <summary>
/// <c>tools.custom</c> is an array of tables, which no descriptor can describe, so this
/// reads and writes it directly the way the tool repository list does.
/// </summary>
internal sealed class CustomOpeners : ICustomOpeners
{
    private const string Key = "tools.custom";

    private const string NameKey = "name";
    private const string PathKey = "path";
    private const string ArgumentsKey = "arguments";

    private readonly IApplicationSettings _settings;

    public CustomOpeners(IApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    public IReadOnlyList<CustomOpener> Read()
    {
        // The file has not been read since the last write from here, so it is read again.
        _settings.Reload();

        if (!_settings.Global.TryGet<object[]>(Key, out var rows))
        {
            return [];
        }

        List<CustomOpener> openers = [];

        // An array of tables reads back as an array of tables, so a row is a dictionary
        // rather than a type the converter knows. A row with no name or no path is skipped.
        foreach (var row in rows.OfType<IReadOnlyDictionary<string, object?>>())
        {
            if (Text(row, NameKey) is not { } name || Text(row, PathKey) is not { } path)
            {
                continue;
            }

            openers.Add(new CustomOpener(name, path, Text(row, ArgumentsKey) ?? string.Empty));
        }

        return openers;
    }

    public void Write(IReadOnlyList<CustomOpener> openers)
    {
        ArgumentNullException.ThrowIfNull(openers);

        var rows = openers
            .Select(opener => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [NameKey] = opener.Name,
                [PathKey] = opener.Path,
                [ArgumentsKey] = opener.Arguments,
            })
            .ToArray();

        _settings.Apply(SettingsScope.Global, [SettingsEdit.Set(Key, rows)]);
    }

    private static string? Text(IReadOnlyDictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var value) && value is string text && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;
}
