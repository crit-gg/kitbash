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
    private readonly IEngineRepositoryList _repositories;

    public EngineRequirementReader(
        IGodotProjectReader projects,
        ISettingsDocumentStore store,
        WorkspaceGodotSettingsSchema schema,
        IEngineRepositoryList repositories)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(repositories);

        _projects = projects;
        _store = store;
        _schema = schema;
        _repositories = repositories;
    }

    public EngineRequirement Read(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var project = _projects.Find(workspaceRoot);
        var needsDotnet = project?.UsesDotnet ?? false;

        if (Text(workspaceRoot, _schema.Repository.Key, _ => true) is { } name)
        {
            return ReadForRepository(workspaceRoot, name.Trim(), project, needsDotnet);
        }

        if (Pinned(workspaceRoot, EngineVersionPattern.TryParse) is { } pinned)
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

    /// <summary>
    /// A workspace that names a repository. Its pin is read in the repository's own form,
    /// and a project that pins nothing follows the newest build of the version its
    /// <c>project.godot</c> names.
    /// </summary>
    private EngineRequirement ReadForRepository(
        string workspaceRoot, string name, GodotProject? project, bool needsDotnet)
    {
        var repository = _repositories
            .ReadFor(workspaceRoot)
            .FirstOrDefault(source => string.Equals(source.Name, name, StringComparison.OrdinalIgnoreCase));

        if (Pinned(workspaceRoot, EngineVersionPattern.TryParseForRepository) is { } pinned)
        {
            return new EngineRequirement
            {
                Version = pinned,
                NeedsDotnet = needsDotnet || pinned.NeedsDotnet,
                Source = EngineRequirementSource.Workspace,
                Project = project,
                RepositoryName = name,
                Repository = repository,
            };
        }

        if (project?.Version is { } named
            && EngineVersionPattern.TryParseForRepository(named.ToString(), out var followed))
        {
            return new EngineRequirement
            {
                Version = followed,
                NeedsDotnet = needsDotnet,
                Source = EngineRequirementSource.Project,
                Project = project,
                RepositoryName = name,
                Repository = repository,
            };
        }

        return project is null
            ? EngineRequirement.None
            : new EngineRequirement
            {
                NeedsDotnet = needsDotnet,
                Project = project,
                RepositoryName = name,
                Repository = repository,
            };
    }

    private delegate bool PatternParser(string? text, out EngineVersionPattern pattern);

    private EngineVersionPattern? Pinned(string workspaceRoot, PatternParser parse)
    {
        EngineVersionPattern found = default;

        // Blank is the default and means nothing was pinned, so it falls through to the
        // layer below rather than masking it.
        return Text(workspaceRoot, _schema.Engine.Key, text => parse(text, out found)) is not null
            ? found
            : null;
    }

    /// <summary>
    /// The first layer holding a value for a key that <paramref name="accept"/> takes,
    /// highest precedence first, so a person's own answer is read before the team's.
    /// </summary>
    private string? Text(string workspaceRoot, string key, Func<string, bool> accept)
    {
        var paths = new WorkspacePaths(workspaceRoot);

        foreach (var layer in new[] { SettingsLayer.User, SettingsLayer.TeamShared })
        {
            var file = _store.Open(paths.FileFor(SettingsScope.Global, layer));

            if (file.ParseError is null
                && file.Document.TryGetValue(key, out var raw)
                && raw is string text
                && !string.IsNullOrWhiteSpace(text)
                && accept(text))
            {
                return text;
            }
        }

        return null;
    }
}
