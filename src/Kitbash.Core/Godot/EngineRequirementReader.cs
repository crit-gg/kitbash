using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;

namespace Kitbash.Core.Godot;

/// <summary>
/// Reads the engine a workspace asks for.
/// </summary>
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

                // A pin ending in mono asks for the .NET build as well. It only ever adds
                // to what the project needs, since a pin without it asks for nothing
                // either way and the project is what knows whether it has C# in it.
                NeedsDotnet = needsDotnet || pinned.NeedsDotnet,
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
