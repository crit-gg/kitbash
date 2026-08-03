using Kitbash.Core.Workspaces;

namespace Kitbash.Core.Settings.Schema;

/// <summary>
/// Every workspace a person has added, one place each, named the way the launcher names
/// them. This is the launcher's workspace home: its window lists them all rather than
/// following whichever one is open.
/// </summary>
internal sealed class KnownWorkspacesSettingsHome : ISettingsHome
{
    private static readonly IReadOnlyList<SettingsLayer?> LowestFirst =
        [SettingsLayer.TeamShared, SettingsLayer.User];

    private readonly IWorkspaceRegistry _workspaces;
    private readonly IWorkspaceScaffold _scaffold;
    private readonly ISettingsDocumentStore _store;

    public KnownWorkspacesSettingsHome(
        IWorkspaceRegistry workspaces,
        IWorkspaceScaffold scaffold,
        ISettingsDocumentStore store)
    {
        ArgumentNullException.ThrowIfNull(workspaces);
        ArgumentNullException.ThrowIfNull(scaffold);
        ArgumentNullException.ThrowIfNull(store);

        _workspaces = workspaces;
        _scaffold = scaffold;
        _store = store;
    }

    public SettingsHome Home => SettingsHome.Workspace;

    public bool IsReadOnly => false;

    public IReadOnlyList<SettingsLayer?> Layers => LowestFirst;

    /// <summary>
    /// The ones that are still on disk. A workspace whose folder has gone has nothing to
    /// change, and offering to write into a folder that is not there would only fail.
    /// </summary>
    public IReadOnlyList<SettingsPlace> Places =>
    [
        .. _workspaces.All
            .Where(workspace => workspace.Exists)
            .Select(workspace => new SettingsPlace(workspace.Root, workspace.Name)),
    ];

    public string FileFor(SettingsPlace place, SettingsScope scope, SettingsLayer? layer)
    {
        ArgumentNullException.ThrowIfNull(place);

        return new WorkspacePaths(place.Id).FileFor(scope, Require(layer));
    }

    public void Apply(
        SettingsPlace place,
        SettingsScope scope,
        SettingsLayer? layer,
        IReadOnlyList<SettingsEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(edits);

        var paths = new WorkspacePaths(place.Id);
        var chosen = Require(layer);

        _store.Apply(paths.FileFor(scope, chosen), edits);

        if (chosen is SettingsLayer.User)
        {
            _scaffold.EnsureUserLayerIgnored(paths.Root);
        }
    }

    private static SettingsLayer Require(SettingsLayer? layer) =>
        layer ?? throw new ArgumentException(
            "A workspace file is team shared or personal, so a layer has to be named.",
            nameof(layer));
}
