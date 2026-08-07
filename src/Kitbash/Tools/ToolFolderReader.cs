using Kitbash.Core.IO;

namespace Kitbash.Tools;

/// <summary>
/// Reads a folder somebody pointed at. It may hold the loose form, or a published manifest,
/// or neither, in which case a single project in it is enough to run.
/// </summary>
public sealed class ToolFolderReader : IToolFolderReader
{
    /// <summary>The project file a folder is read as when it declares nothing at all.</summary>
    private const string ProjectKind = ".csproj";

    /// <summary>What builds and runs a project, looked up on PATH like any other program.</summary>
    private const string ProjectProgram = "dotnet";

    private readonly IToolManifestReader _manifests;
    private readonly IToolRuntime _runtime;
    private readonly IFileSystem _files;

    public ToolFolderReader(IToolManifestReader manifests, IToolRuntime runtime, IFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(files);

        _manifests = manifests;
        _runtime = runtime;
        _files = files;
    }

    public InstalledTool Read(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var manifest = Declared(directory) ?? Inferred(directory);

        return new InstalledTool(
            ToolId.For(ToolId.LocalSource, manifest.Id),
            manifest.Version,
            directory,
            manifest,
            Command(directory, manifest),
            IsLinked: true);
    }

    /// <summary>
    /// What the folder says it is, or null when it says nothing. The loose form wins, so a
    /// tool can keep the manifest it publishes and still be run from where it is built.
    /// </summary>
    private ToolManifest? Declared(string directory)
    {
        if (_files.FileExists(Path.Combine(directory, ToolManifestReader.DevelopmentFileName)))
        {
            return _manifests.ReadDevelopmentFrom(directory);
        }

        return _files.FileExists(Path.Combine(directory, ToolManifestReader.FileName))
            ? _manifests.ReadFrom(directory)
            : null;
    }

    /// <summary>
    /// A folder holding one project and nothing else Kitbash reads. The project's own name
    /// is the tool's, and building it is what running it means.
    /// </summary>
    private ToolManifest Inferred(string directory)
    {
        List<string> projects =
        [
            .. _files
                .EnumerateFiles(directory, recursive: false)
                .Where(file => ProjectKind.Equals(Path.GetExtension(file), StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal),
        ];

        if (projects.Count == 0)
        {
            throw new ToolManifestException(
                $"'{directory}' holds no {ToolManifestReader.DevelopmentFileName}, "
                + $"no {ToolManifestReader.FileName} and no project.");
        }

        if (projects.Count > 1)
        {
            throw new ToolManifestException(
                $"'{directory}' holds {projects.Count} projects, so add a "
                + $"{ToolManifestReader.DevelopmentFileName} naming the one to run.");
        }

        var name = Path.GetFileNameWithoutExtension(projects[0]);
        var id = ToolManifestReader.Slug(name);

        if (id.Length == 0)
        {
            throw new ToolManifestException(
                $"'{name}' cannot be a tool id, so add a "
                + $"{ToolManifestReader.DevelopmentFileName} giving one.");
        }

        return new ToolManifest(
            _manifests.NewestFormat,
            id,
            name,
            string.Empty,
            string.Empty,
            Icon: null,
            ToolManifestReader.DevelopmentVersion,
            Required: false,
            Payloads: [],
            ToolKind.App)
        {
            // The last word is what stops dotnet reading the launcher's own arguments as
            // options of its own.
            Command = [ProjectProgram, "run", "--project", projects[0], "--"],
        };
    }

    /// <summary>
    /// What gets run. A command names its own program and anything else runs the payload
    /// this machine matches, the same way an installed version does.
    /// </summary>
    private ToolCommand Command(string directory, ToolManifest manifest)
    {
        if (manifest.Command.Count == 0)
        {
            return _runtime.PayloadFor(manifest) is { } payload
                ? ToolCommand.For(payload, directory)
                : throw new ToolManifestException(
                    $"{manifest.Name} {manifest.Version} has nothing to run on {_runtime.Identifier}.");
        }

        return new ToolCommand(Program(directory, manifest.Command[0]), [.. manifest.Command.Skip(1)]);
    }

    /// <summary>
    /// A program carrying a separator, or one a file in the folder is named after, is that
    /// file. Anything else is a name the operating system looks up on PATH.
    /// </summary>
    private string Program(string directory, string program)
    {
        if (Path.IsPathRooted(program) || program.AsSpan().IndexOfAny('/', '\\') >= 0)
        {
            return Path.GetFullPath(Path.Combine(directory, program));
        }

        var beside = Path.Combine(directory, program);

        return _files.FileExists(beside) ? beside : program;
    }
}
