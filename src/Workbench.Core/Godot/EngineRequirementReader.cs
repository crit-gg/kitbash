using Workbench.Core.Settings;
using Workbench.Core.Settings.Schema;

namespace Workbench.Core.Godot;

/// <summary>
/// Reads the engine a workspace asks for.
/// </summary>
/// <remarks>
/// <para>
/// **The workspace's own config wins over the project file, and only when it is set.**
/// The two say different things. <c>config/features</c> is written by Godot and records
/// the version that last opened the project, so it drifts every time somebody opens it
/// in something newer. The <c>.workbench</c> key is written by a person and means this
/// is the version we use. So the deliberate answer beats the incidental one, and a blank
/// key is not an answer at all and falls through.
/// </para>
/// <para>
/// The .NET flag never comes from the config. Whether a project has C# in it is a fact
/// about the project, not a choice, so it is read off the project every time even when
/// the version came from the workspace.
/// </para>
/// <para>
/// Both layers of the config are read, personal over team shared, which is the same
/// order <see cref="ISettingsService"/> uses. A file that will not parse is passed over
/// rather than thrown, since this runs behind a status strip that has to draw something.
/// </para>
/// </remarks>
internal sealed class EngineRequirementReader : IEngineRequirementReader
{
    private readonly IGodotProjectReader _projects;
    private readonly ISettingsDocumentStore _store;
    private readonly WorkspaceGodotSettingsSchema _schema;

    public EngineRequirementReader(
        IGodotProjectReader projects,
        ISettingsDocumentStore store,
        WorkspaceGodotSettingsSchema schema)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(schema);

        _projects = projects;
        _store = store;
        _schema = schema;
    }

    public EngineRequirement Read(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var project = _projects.Find(workspaceRoot);
        var needsDotnet = project?.UsesDotnet ?? false;

        if (Pinned(workspaceRoot) is { } pinned)
        {
            return new EngineRequirement
            {
                Version = pinned,
                NeedsDotnet = needsDotnet,
                Source = EngineRequirementSource.Workspace,
                Project = project,
            };
        }

        if (project?.Version is { } named)
        {
            return new EngineRequirement
            {
                Version = named,
                NeedsDotnet = needsDotnet,
                Source = EngineRequirementSource.Project,
                Project = project,
            };
        }

        return project is null
            ? EngineRequirement.None
            : new EngineRequirement { NeedsDotnet = needsDotnet, Project = project };
    }

    private EngineVersionPattern? Pinned(string workspaceRoot)
    {
        var paths = new WorkspacePaths(workspaceRoot);

        // Highest precedence first, so a person's own answer is read before the team's.
        foreach (var layer in new[] { SettingsLayer.User, SettingsLayer.TeamShared })
        {
            var file = _store.Open(paths.FileFor(SettingsScope.Global, layer));

            if (file.ParseError is not null
                || !file.Document.TryGetValue(_schema.Engine.Key, out var raw)
                || raw is not string text)
            {
                continue;
            }

            // Blank is the default and means nothing was pinned, so it falls through to
            // the layer below rather than masking it.
            if (EngineVersionPattern.TryParse(text, out var pattern))
            {
                return pattern;
            }
        }

        return null;
    }
}
