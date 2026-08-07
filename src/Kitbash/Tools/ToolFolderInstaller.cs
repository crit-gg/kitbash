using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;

namespace Kitbash.Tools;

/// <summary>
/// Checks a folder a person picked and remembers it. What the folder says it is is the
/// whole contract, so a folder describing a tool is one wherever it sits.
/// </summary>
public sealed class ToolFolderInstaller : IToolFolderInstaller
{
    private readonly IToolFolderReader _folders;
    private readonly IInstalledTools _installed;
    private readonly IFileSystem _files;
    private readonly IExecutableFinder _programs;
    private readonly IPathRules _rules;
    private readonly ApplicationPaths _paths;

    public ToolFolderInstaller(
        IToolFolderReader folders,
        IInstalledTools installed,
        IFileSystem files,
        IExecutableFinder programs,
        IPathRules rules,
        ApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(installed);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(programs);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(paths);

        _folders = folders;
        _installed = installed;
        _files = files;
        _programs = programs;
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

        var tool = Read(root);

        Runnable(tool);

        _installed.Link(tool.Id, root);

        return tool;
    }

    /// <summary>
    /// What can be told before the tool is on the list. A build that has not happened yet
    /// is no reason to refuse a folder somebody is working in, so only a payload has to
    /// have its file there already.
    /// </summary>
    private void Runnable(InstalledTool tool)
    {
        if (!tool.Command.IsFile)
        {
            if (_programs.Find(tool.Command.Program) is null)
            {
                throw new ToolInstallException(
                    $"'{tool.Command.Program}' is not installed, so {tool.Name} has nothing to run it.");
            }

            return;
        }

        if (!_files.FileExists(tool.Executable))
        {
            if (tool.Manifest.Command.Count > 0)
            {
                return;
            }

            throw new ToolInstallException(
                "That folder holds no executable named "
                + $"{Path.GetRelativePath(tool.Directory, tool.Executable)}.");
        }

        // The same bit an unpacked payload gets. A build that wrote the file without it
        // would otherwise install and then fail to start with nothing saying why.
        _files.MakeExecutableFile(tool.Executable);
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
    /// What the folder holds, with its refusal carried across as an install failure so the
    /// caller has one exception to catch. Its message is already written for a person.
    /// </summary>
    private InstalledTool Read(string root)
    {
        try
        {
            return _folders.Read(root);
        }
        catch (ToolManifestException exception)
        {
            throw new ToolInstallException(exception.Message, exception);
        }
    }
}