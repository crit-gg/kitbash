using Workbench.Core.IO;

namespace Workbench.Core.Platform.Linux;

/// <summary>
/// Writes a desktop entry and an icon for the running AppImage, so it appears in the
/// menu instead of being a file somebody has to find again.
/// </summary>
internal sealed class LinuxDesktopIntegration : IDesktopIntegration
{
    /// <summary>The AppImage runtime sets these. Both unset means there is nothing to point at.</summary>
    private const string ImagePath = "APPIMAGE";
    private const string MountPoint = "APPDIR";

    /// <summary>The AppImage specification's icon, whatever the file inside was called.</summary>
    private const string BundledIcon = ".DirIcon";

    private const string EntryName = "workbench";
    private const string IconSize = "256x256";

    private readonly IFileSystem _fileSystem;
    private readonly IEnvironment _environment;

    public LinuxDesktopIntegration(IFileSystem fileSystem, IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(environment);

        _fileSystem = fileSystem;
        _environment = environment;
    }

    public void Install()
    {
        var image = _environment.GetVariable(ImagePath);

        if (string.IsNullOrWhiteSpace(image))
        {
            return;
        }

        try
        {
            WriteIcon();
            WriteEntry(image);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The app works without a menu entry, so a data directory that cannot be
            // written is survived rather than reported.
        }
    }

    /// <summary>
    /// The hicolor theme under the data root. The icon is whatever was passed to the
    /// packaging step, which is a PNG, so it goes in a sized directory rather than
    /// scalable.
    /// </summary>
    private void WriteIcon()
    {
        var mount = _environment.GetVariable(MountPoint);

        if (string.IsNullOrWhiteSpace(mount))
        {
            return;
        }

        var source = Path.Combine(mount, BundledIcon);

        if (!_fileSystem.FileExists(source))
        {
            return;
        }

        var directory = Path.Combine(DataRoot, "icons", "hicolor", IconSize, "apps");
        _fileSystem.CreateDirectory(directory);

        using var reading = _fileSystem.OpenRead(source);
        using var writing = _fileSystem.Create(Path.Combine(directory, EntryName + ".png"));

        reading.CopyTo(writing);
    }

    /// <summary>
    /// Rewritten whenever it does not match, so moving the AppImage fixes the entry at
    /// the next launch rather than leaving one that points nowhere.
    /// </summary>
    private void WriteEntry(string image)
    {
        var directory = Path.Combine(DataRoot, "applications");
        var file = Path.Combine(directory, EntryName + ".desktop");
        var wanted = Entry(image);

        if (_fileSystem.FileExists(file) && _fileSystem.ReadAllText(file) == wanted)
        {
            return;
        }

        _fileSystem.CreateDirectory(directory);
        _fileSystem.WriteAllText(file, wanted);
    }

    private static string Entry(string image) =>
        $"""
        [Desktop Entry]
        Type=Application
        Name=Workbench
        Comment=Designer tools for a Godot project
        Icon={EntryName}
        Exec={Quote(image)}
        Categories=Development;
        Terminal=false

        """;

    /// <summary>
    /// The Exec value, quoted the way the desktop entry specification asks, so a path
    /// holding a space still names one program.
    /// </summary>
    private static string Quote(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal);

        return $"\"{escaped}\"";
    }

    /// <summary>
    /// The data root itself rather than the folder Workbench keeps its own files in, so
    /// this reads the variable rather than going through IUserDirectories.
    /// </summary>
    private string DataRoot
    {
        get
        {
            var configured = _environment.GetVariable("XDG_DATA_HOME");

            return !string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(_environment.GetHomeDirectory(), ".local", "share");
        }
    }
}
