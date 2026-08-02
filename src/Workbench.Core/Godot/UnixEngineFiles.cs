using System.Runtime.InteropServices;
using Workbench.Core.IO;

namespace Workbench.Core.Godot;

internal sealed class UnixEngineFiles : IEngineFiles
{
    private readonly IFileSystem _files;

    public UnixEngineFiles(IFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(files);

        _files = files;
    }

    public EnginePlatform Platform => EnginePlatform.Linux;

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

        // GodotSharp sits in a folder of its own, so nothing else at the top level of a
        // .NET install carries the executable bit.
        var candidates = _files
            .EnumerateFiles(directory, recursive: false)
            .Where(_files.IsExecutableFile)
            .Order(StringComparer.Ordinal)
            .ToList();

        return candidates.FirstOrDefault();
    }

    public void MakeExecutable(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!_files.FileExists(path))
        {
            return;
        }

        var mode = File.GetUnixFileMode(path);
        var wanted = mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

        if (mode != wanted)
        {
            File.SetUnixFileMode(path, wanted);
        }
    }

    /// <summary>A leading dot already hides it here, so there is nothing to set.</summary>
    public void Hide(string path)
    {
    }
}
