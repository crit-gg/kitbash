namespace Workbench.Core.IO;

/// <summary>The real filesystem.</summary>
public sealed class FileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool IsExecutableFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        const UnixFileMode executable =
            UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

        return (File.GetUnixFileMode(path) & executable) != 0;
    }

    public void MakeExecutableFile(string path)
    {
        if (!File.Exists(path) || OperatingSystem.IsWindows())
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

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite) =>
        File.Move(sourcePath, destinationPath, overwrite);

    public DateTimeOffset? GetLastWriteTime(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    public IReadOnlyList<string> EnumerateDirectories(string path)
    {
        if (!Directory.Exists(path))
        {
            return [];
        }

        try
        {
            return [.. Directory.EnumerateDirectories(path).Order(StringComparer.Ordinal)];
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // A folder we cannot read is a folder with nothing in it, for our purposes.
            return [];
        }
    }

    public IReadOnlyList<string> EnumerateFiles(string path, bool recursive)
    {
        if (!Directory.Exists(path))
        {
            return [];
        }

        var depth = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        try
        {
            return [.. Directory.EnumerateFiles(path, "*", depth).Order(StringComparer.Ordinal)];
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }

    public long GetFileLength(string path)
    {
        try
        {
            var file = new FileInfo(path);

            return file.Exists ? file.Length : 0;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return 0;
        }
    }

    public void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(path, recursive: true);
    }

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public Stream OpenRead(string path) => File.OpenRead(path);

    public Stream Create(string path) => File.Create(path);
}
