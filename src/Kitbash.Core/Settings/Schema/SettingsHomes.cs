namespace Kitbash.Core.Settings.Schema;

/// <summary>
/// The one file this user keeps on this machine. No layer, since there is nothing to
/// share it with, and one place, since there is only one of this machine.
/// </summary>
internal sealed class ApplicationSettingsHome : ISettingsHome
{
    private static readonly IReadOnlyList<SettingsLayer?> Unlayered = [null];
    private static readonly IReadOnlyList<SettingsPlace> Single = [SettingsPlace.Only];

    private readonly ApplicationPaths _paths;
    private readonly IApplicationSettings _settings;

    public ApplicationSettingsHome(ApplicationPaths paths, IApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(settings);

        _paths = paths;
        _settings = settings;
    }

    public SettingsHome Home => SettingsHome.Application;

    public bool IsReadOnly => false;

    public IReadOnlyList<SettingsLayer?> Layers => Unlayered;

    public IReadOnlyList<SettingsPlace> Places => Single;

    public string FileFor(SettingsPlace place, SettingsScope scope, SettingsLayer? layer) =>
        _paths.SettingsFileFor(scope);

    public void Apply(
        SettingsPlace place,
        SettingsScope scope,
        SettingsLayer? layer,
        IReadOnlyList<SettingsEdit> edits) =>
        _settings.Apply(scope, edits);
}

/// <summary>
/// What the app remembers for itself. Read only, because a person is not expected to
/// edit it and a settings window that offered to would be lying about who owns it.
/// </summary>
internal sealed class ApplicationStateHome : ISettingsHome
{
    private static readonly IReadOnlyList<SettingsLayer?> Unlayered = [null];
    private static readonly IReadOnlyList<SettingsPlace> Single = [SettingsPlace.Only];

    private readonly ApplicationPaths _paths;

    public ApplicationStateHome(ApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        _paths = paths;
    }

    public SettingsHome Home => SettingsHome.State;

    public bool IsReadOnly => true;

    public IReadOnlyList<SettingsLayer?> Layers => Unlayered;

    public IReadOnlyList<SettingsPlace> Places => Single;

    public string FileFor(SettingsPlace place, SettingsScope scope, SettingsLayer? layer) =>
        _paths.StateFileFor(scope);

    public void Apply(
        SettingsPlace place,
        SettingsScope scope,
        SettingsLayer? layer,
        IReadOnlyList<SettingsEdit> edits) =>
        throw new InvalidOperationException("Application state is read only. The app writes it, not a person.");
}

/// <summary>
/// The one workspace an app was started in, and the only home that layers. Its place
/// carries no name, so a tool with a single workspace draws no level for it.
/// </summary>
internal sealed class WorkspaceSettingsHome : ISettingsHome
{
    private static readonly IReadOnlyList<SettingsLayer?> LowestFirst =
        [SettingsLayer.TeamShared, SettingsLayer.User];

    private static readonly IReadOnlyList<SettingsPlace> Single = [SettingsPlace.Only];

    private readonly WorkspacePaths _paths;
    private readonly ISettingsService _settings;

    public WorkspaceSettingsHome(WorkspacePaths paths, ISettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(settings);

        _paths = paths;
        _settings = settings;
    }

    public SettingsHome Home => SettingsHome.Workspace;

    public bool IsReadOnly => false;

    public IReadOnlyList<SettingsLayer?> Layers => LowestFirst;

    public IReadOnlyList<SettingsPlace> Places => Single;

    public string FileFor(SettingsPlace place, SettingsScope scope, SettingsLayer? layer) =>
        _paths.FileFor(scope, Require(layer));

    public void Apply(
        SettingsPlace place,
        SettingsScope scope,
        SettingsLayer? layer,
        IReadOnlyList<SettingsEdit> edits) =>
        _settings.Apply(scope, Require(layer), edits);

    private static SettingsLayer Require(SettingsLayer? layer) =>
        layer ?? throw new ArgumentException(
            "A workspace file is team shared or personal, so a layer has to be named.",
            nameof(layer));
}
