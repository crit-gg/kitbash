using Kitbash.Core.IO;
using Kitbash.Core.Settings;

namespace Kitbash.Tools;

/// <summary>
/// Checks a folder a person picked and remembers it. The manifest is the whole contract,
/// so a folder holding one and the program it names is a tool wherever it sits.
/// </summary>
public sealed class ToolFolderInstaller : IToolFolderInstaller
{
    private readonly IToolManifestReader _manifests;
    private readonly IToolRuntime _runtime;
    private readonly IInstalledTools _installed;
    private readonly IFileSystem _files;
    private readonly IPathRules _rules;
    private readonly ApplicationPaths _paths;

    public ToolFolderInstaller(
        IToolManifestReader manifests,
        IToolRuntime runtime,
        IInstalledTools installed,
        IFileSystem files,
        IPathRules rules,
        ApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(installed);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(paths);

        _manifests = manifests;
        _runtime = runtime;
        _installed = installed;
        _files = files;
        _rules = rules;
        _paths = paths;
    }

    public InstalledTool InstallFrom(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var root = Full(directory);

        if (!_files.DirectoryExists(root))
        {
            throw new ToolInstallException($"'{root}' is not a folder.");
        }

        // A folder Kitbash already manages is reached through the tool it belongs to. A
        // second card over the same files would offer to remove one and delete the other.
        if (_rules.AreSame(root, _paths.Tools) || _rules.Contains(_paths.Tools, root))
        {
            throw new ToolInstallException(
                "That folder is inside the folder Kitbash installs tools into, so it is already listed.");
        }

        var manifest = Read(root);
        var id = ToolId.For(ToolId.LocalSource, manifest.Id);

        if (_runtime.PayloadFor(manifest) is not { } payload)
        {
            throw new ToolInstallException(
                $"{manifest.Name} {manifest.Version} has nothing to run on {_runtime.Identifier}.");
        }

        var executable = payload.ExecutableIn(root);

        if (!_files.FileExists(executable))
        {
            throw new ToolInstallException($"That folder holds no executable named {payload.Executable}.");
        }

        // The same bit an unpacked payload gets. A build that wrote the file without it
        // would otherwise install and then fail to start with nothing saying why.
        _files.MakeExecutableFile(executable);

        _installed.Link(id, root);

        return new InstalledTool(id, manifest.Version, root, manifest, payload, IsLinked: true);
    }

    /// <summary>
    /// An absolute path with no trailing separator, so what is stored means one folder
    /// whichever process reads it back.
    /// </summary>
    private static string Full(string directory)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ToolInstallException($"'{directory}' is not a path.", exception);
        }
    }

    /// <summary>
    /// The manifest, with its refusal carried across as an install failure so the caller
    /// has one exception to catch. Its message is already written for a person.
    /// </summary>
    private ToolManifest Read(string root)
    {
        try
        {
            return _manifests.ReadFrom(root);
        }
        catch (ToolManifestException exception)
        {
            throw new ToolInstallException(exception.Message, exception);
        }
    }
}