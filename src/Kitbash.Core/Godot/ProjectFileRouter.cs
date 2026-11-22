using Kitbash.Core.IO;
using Kitbash.Core.Platform;

namespace Kitbash.Core.Godot;

/// <summary>Routes a <c>project.godot</c> to a workspace or to the engine it asks for.</summary>
internal sealed class ProjectFileRouter : IProjectFileRouter
{
    private readonly IFileSystem _files;
    private readonly IPathRules _paths;
    private readonly IGodotProjectReader _projects;
    private readonly IEngineStore _engines;
    private readonly IEngineResolver _resolver;

    public ProjectFileRouter(
        IFileSystem files,
        IPathRules paths,
        IGodotProjectReader projects,
        IEngineStore engines,
        IEngineResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(resolver);

        _files = files;
        _paths = paths;
        _projects = projects;
        _engines = engines;
        _resolver = resolver;
    }

    public async Task<ProjectFileRoute> RouteAsync(
        ProjectFileArgument file,
        IReadOnlyList<string> workspaceRoots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(workspaceRoots);

        if (!_files.FileExists(file.Path))
        {
            return new ProjectFileRoute(ProjectFileRouteKind.Gone);
        }

        // Known means the project that workspace would open. Any other project.godot inside
        // a workspace is treated as a project of its own.
        foreach (var root in workspaceRoots)
        {
            if (_projects.Find(root) is { } owned && _paths.AreSame(owned.File, file.Path))
            {
                return new ProjectFileRoute(ProjectFileRouteKind.Workspace) { WorkspaceRoot = root, Project = owned };
            }
        }

        var project = _projects.Read(file.Path);

        if (!project.IsSupported)
        {
            return new ProjectFileRoute(ProjectFileRouteKind.Unsupported) { Project = project };
        }

        if (project.Version is not { } version)
        {
            return new ProjectFileRoute(ProjectFileRouteKind.NoVersion) { Project = project };
        }

        var requirement = new EngineRequirement
        {
            Version = version,
            NeedsDotnet = project.UsesDotnet,
            Source = EngineRequirementSource.Project,
            Project = project,
        };

        var installed = await _engines.ReadAsync(cancellationToken).ConfigureAwait(false);

        // No default is passed, so only the version and runtime asked for can answer.
        var resolution = _resolver.Resolve(requirement, installed, theDefault: null);

        return resolution is { Match: EngineMatch.Matched, Engine: { } engine }
            ? new ProjectFileRoute(ProjectFileRouteKind.Engine) { Engine = engine, Project = project, Requirement = requirement }
            : new ProjectFileRoute(ProjectFileRouteKind.NotInstalled) { Project = project, Requirement = requirement };
    }
}
