using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Workbench.Core.IO;

namespace Workbench.Core.Godot;

internal sealed class WindowsEngineFiles : IEngineFiles
{
    private const string ConsoleSuffix = "_console.exe";

    private readonly IFileSystem _files;

    public WindowsEngineFiles(IFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(files);

        _files = files;
    }

    public EnginePlatform Platform => EnginePlatform.Windows;

    public EngineArchitecture Architecture => RuntimeInformation.OSArchitecture switch
    {
        System.Runtime.InteropServices.Architecture.X64 => EngineArchitecture.X64,
        System.Runtime.InteropServices.Architecture.X86 => EngineArchitecture.X86,
        System.Runtime.InteropServices.Architecture.Arm64 => EngineArchitecture.Arm64,
        System.Runtime.InteropServices.Architecture.Arm => EngineArchitecture.Arm32,
        _ => EngineArchitecture.X64,
    };

    public string? FindEditor(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!_files.DirectoryExists(directory))
        {
            return null;
        }

        // Windows ships the editor and a console executable side by side, which is why the
        // standard archive holds two loose files. Measured: 4.0-alpha1 spelled the console
        // one _console.cmd and every release from 4.0-stable on spells it _console.exe, so
        // matching on the stem covers both.
        var candidates = _files
            .EnumerateFiles(directory, recursive: false)
            .Where(path => path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .Where(path => !Path.GetFileNameWithoutExtension(path).EndsWith("_console", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return candidates.FirstOrDefault();
    }

    /// <summary>Nothing here decides whether a file runs, so there is nothing to set.</summary>
    public void MakeExecutable(string path)
    {
    }

    [SupportedOSPlatform("windows")]
    public void Hide(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!_files.FileExists(path))
        {
            return;
        }

        // A leading dot means nothing here, so the attribute is what does it. Untested on
        // this machine, which is Linux.
        var attributes = File.GetAttributes(path);

        if (!attributes.HasFlag(FileAttributes.Hidden))
        {
            File.SetAttributes(path, attributes | FileAttributes.Hidden);
        }
    }
}
