using Kitbash.Core.IO;

namespace Kitbash.Core.Godot;

internal sealed class MacEngineFiles : IEngineFiles
{
    private const string BundleSuffix = ".app";

    private readonly IFileSystem _files;
    private readonly IEnvironment _environment;

    public MacEngineFiles(IFileSystem files, IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(environment);

        _files = files;
        _environment = environment;
    }

    // Godot's data directory is ~/Library/Application Support/Godot. Our own user
    // directories put a subfolder under it, so the path is built here.
    public string ExportTemplatesDirectory =>
        Path.Combine(DataDirectory, "export_templates");

    // Godot also looks beside the bundle, since the bundle itself is read only.
    public string DataDirectoryFor(string editor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(editor);

        var folder = Path.GetDirectoryName(editor);

        if (folder is null)
        {
            return DataDirectory;
        }

        if (IsSelfContained(folder))
        {
            return Path.Combine(folder, "editor_data");
        }

        var contents = Path.GetDirectoryName(folder);

        if (Path.GetFileName(folder) == "MacOS"
            && Path.GetFileName(contents) == "Contents"
            && Path.GetDirectoryName(Path.GetDirectoryName(contents)) is { } beside
            && IsSelfContained(beside))
        {
            return Path.Combine(beside, "editor_data");
        }

        return DataDirectory;
    }

    private string DataDirectory =>
        Path.Combine(_environment.GetHomeDirectory(), "Library", "Application Support", "Godot");

    public EnginePlatform Platform => EnginePlatform.MacOS;

    /// <summary>
    /// Godot publishes one macOS build carrying every processor, so this is not read from
    /// the host. Answering Arm64 would leave every release reporting no native build.
    /// </summary>
    public EngineArchitecture Architecture => EngineArchitecture.Universal;

    public string? FindEditor(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!_files.DirectoryExists(directory))
        {
            return null;
        }

        // Importing an existing engine means pointing at Godot.app itself.
        if (IsBundle(directory) && Inside(directory) is { } own)
        {
            return own;
        }

        // An installed one extracts as Godot.app or Godot_mono.app, since the installer
        // leaves a top level directory alone when it is a bundle.
        foreach (var child in _files.EnumerateDirectories(directory))
        {
            if (IsBundle(child) && Inside(child) is { } found)
            {
                return found;
            }
        }

        // A loose binary somebody put in a folder, which is the Linux shape.
        return _files
            .EnumerateFiles(directory, recursive: false)
            .Where(_files.IsExecutableFile)
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
    }

    // The program inside the bundle. Godot takes its own path from proc_pidpath, which
    // follows a link, so a link to it still finds the bundle.
    public string CommandFor(string editor) => editor;

    /// <summary>A leading dot already hides it here, so there is nothing to set.</summary>
    public void Hide(string path)
    {
    }

    private bool IsSelfContained(string folder) =>
        _files.FileExists(Path.Combine(folder, "._sc_")) || _files.FileExists(Path.Combine(folder, "_sc_"));

    private static bool IsBundle(string path) =>
        path.TrimEnd('/').EndsWith(BundleSuffix, StringComparison.OrdinalIgnoreCase);

    private string? Inside(string bundle)
    {
        var executables = Path.Combine(bundle, "Contents", "MacOS");

        if (!_files.DirectoryExists(executables))
        {
            return null;
        }

        return _files
            .EnumerateFiles(executables, recursive: false)
            .Where(_files.IsExecutableFile)
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
