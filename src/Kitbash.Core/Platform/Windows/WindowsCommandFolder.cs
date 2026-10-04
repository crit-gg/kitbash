using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Microsoft.Win32;

namespace Kitbash.Core.Platform.Windows;

/// <summary>
/// A bin folder beside Kitbash's own data, on the user PATH. A command is the vendored
/// shim copied in under the command's name, with a .shim file naming the program, since a
/// link would make Godot look for GodotSharp beside the link. Untested on this machine.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsCommandFolder : ICommandFolder
{
    // Where Kitbash.csproj puts the vendored shim, beside the running program.
    private const string ShimLocation = "shims/shim.exe";

    private const string EnvironmentKey = "Environment";
    private const string MachineEnvironmentKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
    private const string PathValue = "Path";

    // The shim reads one name = value per line and takes path as the program.
    private const string PathPrefix = "path = ";

    // What Windows falls back to when PATHEXT is unset.
    private const string FallbackExtensions = ".COM;.EXE;.BAT;.CMD";

    private const int Broadcast = 0xFFFF;
    private const uint SettingChange = 0x001A;
    private const uint AbortIfHung = 0x0002;
    private const uint BroadcastWait = 5000;

    private readonly IFileSystem _files;
    private readonly IEnvironment _environment;
    private readonly IUserDirectories _directories;
    private readonly IPathRules _paths;

    public WindowsCommandFolder(
        IFileSystem files, IEnvironment environment, IUserDirectories directories, IPathRules paths)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(directories);
        ArgumentNullException.ThrowIfNull(paths);

        _files = files;
        _environment = environment;
        _directories = directories;
        _paths = paths;
    }

    // %LOCALAPPDATA%\KitbashData\bin, outside the folder Velopack's uninstaller deletes.
    public string? Directory =>
        Path.GetDirectoryName(_directories.StateFor(ApplicationPaths.ApplicationName)) is { } data
            ? Path.Combine(data, "bin")
            : null;

    public bool CanWrite => Directory is not null && ShimSource() is not null;

    public string? PathOf(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Directory is { } directory ? Path.Combine(directory, name + ".exe") : null;
    }

    public CommandEntry Read(string name)
    {
        if (PathOf(name) is not { } program)
        {
            return CommandEntry.Absent;
        }

        var shim = ShimFileOf(program);

        if (_files.FileExists(shim) && ReadTarget(shim) is { } target)
        {
            return new CommandEntry(true, target);
        }

        return _files.FileExists(program) || _files.FileExists(shim)
            ? new CommandEntry(true, null)
            : CommandEntry.Absent;
    }

    public void Write(string name, string program)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(program);

        var path = PathOf(name) ?? throw new InvalidOperationException("There is no folder to write a command into.");
        var source = ShimSource() ?? throw new FileNotFoundException("This copy of Kitbash carries no shim.", ShimLocation);

        _files.CreateDirectory(Path.GetDirectoryName(path)!);

        // A running godot.exe cannot be replaced, so the copy is only made when it differs.
        if (!SameBytes(source, path))
        {
            var copy = path + ".kitbash";
            Copy(source, copy);
            _files.MoveFile(copy, path, overwrite: true);
        }

        var shim = ShimFileOf(path);
        var staged = shim + ".kitbash";
        _files.WriteAllText(staged, PathPrefix + program + "\r\n");
        _files.MoveFile(staged, shim, overwrite: true);
    }

    public void Remove(string name)
    {
        if (PathOf(name) is not { } path)
        {
            return;
        }

        _files.DeleteFile(path);
        _files.DeleteFile(ShimFileOf(path));
    }


    public Task<CommandReach> ReachAsync(string name, bool mayAsk, CancellationToken cancellation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (Directory is not { } directory)
        {
            return Task.FromResult(CommandReach.Unknown);
        }

        try
        {
            // The user PATH needs no administrator, so this is done without asking.
            var reach = PathReach.OnPath;
            var user = ReadUserPath(out var kind);

            if (!user.Contains(entry => Names(entry, directory)))
            {
                WriteUserPath(user.With(directory, entry => Names(entry, directory)), kind);
                reach = PathReach.Added;
            }

            return Task.FromResult(new CommandReach(reach, Shadow(directory, name)));
        }
        catch (Exception exception) when (Survivable(exception))
        {
            return Task.FromResult(CommandReach.Unknown);
        }
    }

    public void Unreach()
    {
        if (Directory is not { } directory)
        {
            return;
        }

        try
        {
            var user = ReadUserPath(out var kind);

            if (user.Contains(entry => Names(entry, directory)))
            {
                WriteUserPath(user.Without(entry => Names(entry, directory)), kind);
            }
        }
        catch (Exception exception) when (Survivable(exception))
        {
            // Left on PATH, an empty folder costs a terminal one lookup and nothing else.
        }
    }

    public void Forget(string name)
    {
        try
        {
            Remove(name);
        }
        catch (Exception exception) when (Survivable(exception))
        {
            // A shim left behind names a program that is still there, since engines are kept.
        }

        Unreach();
    }

    /// <summary>
    /// The first program of this name a new terminal finds, if it is not ours. Read fresh
    /// from the registry, since this process's PATH predates any edit made here.
    /// </summary>
    private string? Shadow(string directory, string name)
    {
        var machine = ReadPath(Registry.LocalMachine, MachineEnvironmentKey, out _);
        var user = ReadUserPath(out _);
        var extensions = _environment.GetVariable("PATHEXT") is { Length: > 0 } set ? set : FallbackExtensions;
        var names = extensions.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(extension => name + extension.Trim()).ToList();

        foreach (var entry in machine.Entries.Concat(user.Entries).Select(Expand).Where(Path.IsPathRooted))
        {
            if (_paths.AreSame(entry, directory))
            {
                return null;
            }

            foreach (var candidate in names.Select(file => Path.Combine(entry, file)))
            {
                if (_files.FileExists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    // DoNotExpandEnvironmentNames keeps %USERPROFILE% and the like as written, and the
    // kind is kept, so writing back never turns an expandable value into a plain one.
    private static PathVariable ReadPath(RegistryKey hive, string key, out RegistryValueKind kind)
    {
        using var environment = hive.OpenSubKey(key);
        kind = RegistryValueKind.ExpandString;

        if (environment?.GetValue(PathValue, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not string value)
        {
            return PathVariable.Parse(null, ';');
        }

        kind = environment.GetValueKind(PathValue);

        return PathVariable.Parse(value, ';');
    }

    private static PathVariable ReadUserPath(out RegistryValueKind kind) =>
        ReadPath(Registry.CurrentUser, EnvironmentKey, out kind);

    // Environment.SetEnvironmentVariable is not used, since it writes REG_SZ and so breaks
    // every %VARIABLE% entry already in the value.
    private static void WriteUserPath(PathVariable path, RegistryValueKind kind)
    {
        using var environment = Registry.CurrentUser.CreateSubKey(EnvironmentKey);
        environment.SetValue(PathValue, path.ToString(), kind == RegistryValueKind.String ? kind : RegistryValueKind.ExpandString);

        // Explorer reads the environment again on this, so a terminal it starts gets the
        // new PATH. One already open keeps the old one.
        SendMessageTimeout(Broadcast, SettingChange, 0, EnvironmentKey, AbortIfHung, BroadcastWait, out _);
    }

    private bool Names(string entry, string directory)
    {
        var expanded = Expand(entry);

        return Path.IsPathRooted(expanded) && _paths.AreSame(expanded, directory);
    }

    /// <summary>Fills in %NAME% from the environment, leaving a name it does not know as written.</summary>
    private string Expand(string entry)
    {
        var parts = entry.Trim().Split('%');

        if (parts.Length < 3)
        {
            return entry.Trim();
        }

        var result = new System.Text.StringBuilder(parts[0]);

        for (var index = 1; index < parts.Length; index++)
        {
            var closed = index + 1 < parts.Length;

            if (closed && parts[index].Length > 0 && _environment.GetVariable(parts[index]) is { } value)
            {
                result.Append(value).Append(parts[++index]);
            }
            else
            {
                result.Append('%').Append(parts[index]);
            }
        }

        return result.ToString();
    }

    private string? ShimSource()
    {
        var command = _environment.GetProcessCommand();

        // An apphost names itself, and a run under dotnet names the assembly last.
        if (command.Count == 0 || Path.GetDirectoryName(command[^1]) is not { Length: > 0 } folder)
        {
            return null;
        }

        var shim = Path.Combine(folder, ShimLocation);

        return _files.FileExists(shim) ? shim : null;
    }

    private string? ReadTarget(string shim)
    {
        try
        {
            return _files.ReadAllText(shim)
                .Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .FirstOrDefault(line => line.StartsWith(PathPrefix, StringComparison.Ordinal))?[PathPrefix.Length..]
                .Trim('"');
        }
        catch (Exception exception) when (Survivable(exception))
        {
            return null;
        }
    }

    private bool SameBytes(string left, string right)
    {
        if (!_files.FileExists(right) || _files.GetFileLength(left) != _files.GetFileLength(right))
        {
            return false;
        }

        using var one = _files.OpenRead(left);
        using var two = _files.OpenRead(right);

        int a;

        while ((a = one.ReadByte()) != -1)
        {
            if (a != two.ReadByte())
            {
                return false;
            }
        }

        return true;
    }

    private void Copy(string source, string destination)
    {
        using var input = _files.OpenRead(source);
        using var output = _files.Create(destination);
        input.CopyTo(output);
    }

    private static string ShimFileOf(string program) => Path.ChangeExtension(program, ".shim");

    private static bool Survivable(Exception exception) =>
        exception is UnauthorizedAccessException or IOException or SecurityException or ArgumentException;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SendMessageTimeout(
        nint window, uint message, nint wParam, string lParam, uint flags, uint timeout, out nint result);
}
