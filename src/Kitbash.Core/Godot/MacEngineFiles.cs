using Kitbash.Core.IO;

namespace Kitbash.Core.Godot;

internal sealed class MacEngineFiles : IEngineFiles
{
    private const string BundleSuffix = ".app";

    private readonly IFileSystem _files;

    public MacEngineFiles(IFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(files);

        _files = files;
    }

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

    /// <summary>A leading dot already hides it here, so there is nothing to set.</summary>
    public void Hide(string path)
    {
    }

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
