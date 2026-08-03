using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Kitbash.Core.IO;

namespace Kitbash.Core.Godot;

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

        // Windows archives hold the editor and a console executable side by side. The
        // console one is _console.cmd on 4.0-alpha1 and _console.exe from 4.0-stable on,
        // so the stem is matched to cover both.
        var candidates = _files
            .EnumerateFiles(directory, recursive: false)
            .Where(path => path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .Where(path => !Path.GetFileNameWithoutExtension(path).EndsWith("_console", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return candidates.FirstOrDefault();
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
