using Kitbash.Core.IO;

namespace Kitbash.Core.Platform.Linux;

/// <summary>
/// Writes a desktop entry and an icon for the running AppImage, so it appears in the
/// menu instead of being a file somebody has to find again, and names Kitbash as the
/// program that opens a <c>kitbash</c> link.
/// </summary>
internal sealed class LinuxDesktopIntegration : IDesktopIntegration
{
    /// <summary>The AppImage runtime sets these. Both unset means there is nothing to point at.</summary>
    private const string ImagePath = "APPIMAGE";
    private const string MountPoint = "APPDIR";

    /// <summary>The AppImage specification's icon, whatever the file inside was called.</summary>
    private const string BundledIcon = ".DirIcon";

    /// <summary>What says an entry is ours. An entry without it belongs to somebody else.</summary>
    private const string Mark = "X-Kitbash-Entry=true";

    private const string EntryName = "kitbash";
    private const string IconSize = "256x256";

    /// <summary>The mime type a url scheme is named by, which is the shared mime info rule.</summary>
    private const string SchemeType = "x-scheme-handler/" + DeepLink.Scheme;

    /// <summary>Where a person's own default program per mime type is kept.</summary>
    private const string AssociationFile = "mimeapps.list";
    private const string DefaultsSection = "[Default Applications]";

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
            if (!Ours())
            {
                return;
            }

            WriteIcon();
            WriteEntry(image);
            WriteAssociation();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The app works without a menu entry, so a data directory that cannot be
            // written is survived rather than reported.
        }
    }

    /// <summary>
    /// Nothing to take back. An AppImage has no uninstaller, so the entry outlives the
    /// file and is rewritten or replaced at the next launch instead.
    /// </summary>
    public void Remove()
    {
    }

    /// <summary>
    /// Whether the entry that is there is one we wrote. An AppImage manager names its
    /// entry after the app too, and theirs holds keys ours does not, so an unmarked one
    /// is left alone and the icon beside it is not written either.
    /// </summary>
    private bool Ours()
    {
        var file = EntryFile;

        return !_fileSystem.FileExists(file)
            || _fileSystem.ReadAllText(file).Contains(Mark, StringComparison.Ordinal);
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
        var file = EntryFile;
        var wanted = Entry(image);

        if (_fileSystem.FileExists(file) && _fileSystem.ReadAllText(file) == wanted)
        {
            return;
        }

        _fileSystem.CreateDirectory(Path.Combine(DataRoot, "applications"));
        _fileSystem.WriteAllText(file, wanted);
    }

    /// <summary>
    /// TryExec is how an AppImage manager decides the file is still there, and it is a
    /// path rather than a command line, so it carries no quoting. %u is the field code
    /// for the one url the desktop passes, and without it the link is dropped.
    /// </summary>
    private static string Entry(string image) =>
        $"""
        [Desktop Entry]
        Type=Application
        Name=Kitbash
        Comment=Godot, without the version wrangling.
        Icon={EntryName}
        Exec={Quote(image)} %u
        TryExec={image}
        Categories=Development;
        MimeType={SchemeType};
        Terminal=false
        {Mark}

        """;

    /// <summary>
    /// Names this entry as what opens a kitbash link. The MimeType line above only says
    /// the entry can, and a desktop reads the default from here.
    /// </summary>
    private void WriteAssociation()
    {
        var file = Path.Combine(ConfigRoot, AssociationFile);

        var lines = _fileSystem.FileExists(file)
            ? _fileSystem.ReadAllText(file).Split('\n').ToList()
            : [];

        if (!Associate(lines))
        {
            return;
        }

        _fileSystem.CreateDirectory(ConfigRoot);
        _fileSystem.WriteAllText(file, string.Join('\n', lines));
    }

    /// <summary>
    /// Puts the association into the lines of a mimeapps.list, leaving every other
    /// section and every other key exactly as it was. False means it was already there.
    /// </summary>
    private static bool Associate(List<string> lines)
    {
        var wanted = $"{SchemeType}={EntryName}.desktop";
        var section = lines.FindIndex(line => line.Trim() == DefaultsSection);

        if (section < 0)
        {
            if (lines.Count > 0 && lines[^1].Trim().Length > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add(DefaultsSection);
            lines.Add(wanted);
            lines.Add(string.Empty);

            return true;
        }

        // Everything up to the next section heading belongs to this section.
        var end = section + 1;

        while (end < lines.Count && !lines[end].TrimStart().StartsWith('['))
        {
            end++;
        }

        var existing = lines.FindIndex(
            section + 1,
            end - section - 1,
            line => line.TrimStart().StartsWith(SchemeType + "=", StringComparison.Ordinal));

        if (existing < 0)
        {
            // Back over the blank lines that separate this section from the next, so the
            // key lands under the ones already here rather than against the next heading.
            var last = end;

            while (last > section + 1 && lines[last - 1].Trim().Length == 0)
            {
                last--;
            }

            lines.Insert(last, wanted);

            return true;
        }

        if (lines[existing] == wanted)
        {
            return false;
        }

        lines[existing] = wanted;

        return true;
    }

    private string EntryFile => Path.Combine(DataRoot, "applications", EntryName + ".desktop");

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
    /// The data root itself rather than the folder Kitbash keeps its own files in, so
    /// this reads the variable rather than going through IUserDirectories.
    /// </summary>
    private string DataRoot => Root("XDG_DATA_HOME", ".local", "share");

    /// <summary>The config root, for the same reason, and where mimeapps.list lives.</summary>
    private string ConfigRoot => Root("XDG_CONFIG_HOME", ".config");

    private string Root(string variable, params string[] fallback)
    {
        var configured = _environment.GetVariable(variable);

        return !string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured)
            ? configured
            : Path.Combine([_environment.GetHomeDirectory(), .. fallback]);
    }
}
