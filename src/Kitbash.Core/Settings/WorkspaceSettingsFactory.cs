using Kitbash.Core.IO;

namespace Kitbash.Core.Settings;

internal sealed class WorkspaceSettingsFactory : IWorkspaceSettingsFactory
{
    private readonly ISettingsDocumentStore _store;
    private readonly ISettingsValueConverter _converter;
    private readonly IFileSystem _fileSystem;

    public WorkspaceSettingsFactory(
        ISettingsDocumentStore store,
        ISettingsValueConverter converter,
        IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _store = store;
        _converter = converter;
        _fileSystem = fileSystem;
    }

    public ISettingsService For(WorkspacePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return new WorkspaceSettingsService(paths, _store, _converter, _fileSystem);
    }
}
