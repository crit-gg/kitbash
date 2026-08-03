namespace Kitbash.Core.Settings;

internal sealed class ApplicationSettings : IApplicationSettings
{
    private readonly ScopedDocuments _documents;

    public ApplicationSettings(
        ApplicationPaths paths,
        ISettingsDocumentStore store,
        ISettingsValueConverter converter)
    {
        ArgumentNullException.ThrowIfNull(paths);

        _documents = new ScopedDocuments(paths.SettingsFileFor, store, converter);
    }

    public ISettings Global => _documents.For(SettingsScope.Global);

    public ISettings ForTool(string toolId) => _documents.For(SettingsScope.ForTool(toolId));

    public void Set<T>(SettingsScope scope, string key, T value)
        where T : notnull =>
        _documents.Set(scope, key, value);

    public void Apply(SettingsScope scope, IReadOnlyList<SettingsEdit> edits) =>
        _documents.Apply(scope, edits);

    public void Reload() => _documents.Reload();
}
