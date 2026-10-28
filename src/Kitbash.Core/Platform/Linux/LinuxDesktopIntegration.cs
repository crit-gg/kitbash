using Kitbash.Core.IO;

namespace Kitbash.Core.Platform.Linux;

/// <summary>
/// Puts the running AppImage in the menu and names it as the program that opens a
/// <c>kitbash</c> link. An entry an AppImage manager wrote for it is kept up to date
/// instead of being joined by a second one.
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
    private const string EntryExtension = ".desktop";
    private const string IconSize = "256x256";

    private const string EntryGroup = "[Desktop Entry]";

    /// <summary>Copied from the entry inside the AppImage into a manager's, so neither goes stale.</summary>
    private static readonly string[] BundledKeys = ["Comment", "X-AppImage-Version"];

    /// <summary>The field codes that pass a file or a url, per the desktop entry specification.</summary>
    private static readonly string[] FieldCodes = ["%u", "%U", "%f", "%F"];

    /// <summary>The mime type a url scheme is named by, which is the shared mime info rule.</summary>
    private const string SchemeType = "x-scheme-handler/" + DeepLink.Scheme;

    /// <summary>Where a person's own default program per mime type is kept.</summary>
    private const string AssociationFile = "mimeapps.list";
    private const string DefaultsSection = "[Default Applications]";

    // GLib finds no handler from a MimeType line without a cache Kitbash does not build, and
    // the portal asks which app to use unless exactly one is associated.
    private const string AddedSection = "[Added Associations]";

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
            var entry = ManagerEntry(image);
            string? removed = null;

            if (entry is null)
            {
                if (!Ours())
                {
                    return;
                }

                WriteIcon();
                WriteEntry(image);
                entry = EntryFile;
            }
            else
            {
                removed = RemoveOwnEntry(entry);
                Refresh(entry);
            }

            WriteAssociation(Path.GetFileName(entry), removed);
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
    /// An entry somebody else wrote for this very file. Gear Lever and AppImageLauncher both
    /// set TryExec to the AppImage, and neither names its entry the same way.
    /// </summary>
    private string? ManagerEntry(string image)
    {
        var folder = ApplicationsFolder;

        if (!_fileSystem.DirectoryExists(folder))
        {
            return null;
        }

        foreach (var file in _fileSystem.EnumerateFiles(folder, recursive: false))
        {
            if (!file.EndsWith(EntryExtension, StringComparison.Ordinal))
            {
                continue;
            }

            string text;

            try
            {
                text = _fileSystem.ReadAllText(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (!text.Contains(Mark, StringComparison.Ordinal) && Value(Lines(text), "TryExec") == image)
            {
                return file;
            }
        }

        return null;
    }

    /// <summary>
    /// One entry claims the scheme, so ours goes once a manager has written its own.
    /// Answers the id of the entry removed, or null when there was none of ours.
    /// </summary>
    private string? RemoveOwnEntry(string managed)
    {
        var file = EntryFile;

        if (file == managed || !_fileSystem.FileExists(file)
            || !_fileSystem.ReadAllText(file).Contains(Mark, StringComparison.Ordinal))
        {
            return null;
        }

        _fileSystem.DeleteFile(file);

        return Path.GetFileName(file);
    }

    /// <summary>
    /// Brings a manager's entry up to the copy that is running, which a Velopack update
    /// replaced without the manager knowing. Gear Lever recognises its app by the file name,
    /// Name, TryExec and the rest of Exec, so none of those change.
    /// </summary>
    private void Refresh(string file)
    {
        var lines = Lines(_fileSystem.ReadAllText(file));
        var changed = false;

        if (Value(lines, "Exec") is { } exec && !TakesUrl(exec))
        {
            changed |= Set(lines, "Exec", exec + " %u");
        }

        var types = Value(lines, "MimeType") ?? string.Empty;

        if (!types.Split(';').Contains(SchemeType, StringComparer.Ordinal))
        {
            var separator = types.Length == 0 || types.EndsWith(';') ? string.Empty : ";";

            changed |= Set(lines, "MimeType", $"{types}{separator}{SchemeType};");
        }

        if (BundledEntry() is { } bundled)
        {
            foreach (var key in BundledKeys)
            {
                if (Value(bundled, key) is { } value)
                {
                    changed |= Set(lines, key, value);
                }
            }
        }

        if (changed)
        {
            Replace(file, string.Join('\n', lines));
        }
    }

    /// <summary>The entry the AppImage carries at its root, or null outside one.</summary>
    private List<string>? BundledEntry()
    {
        var mount = _environment.GetVariable(MountPoint);

        if (string.IsNullOrWhiteSpace(mount) || !_fileSystem.DirectoryExists(mount))
        {
            return null;
        }

        var file = _fileSystem
            .EnumerateFiles(mount, recursive: false)
            .FirstOrDefault(path => path.EndsWith(EntryExtension, StringComparison.Ordinal));

        return file is null ? null : Lines(_fileSystem.ReadAllText(file));
    }

    private static bool TakesUrl(string exec) =>
        exec
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(word => FieldCodes.Contains(word, StringComparer.Ordinal));

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

        _fileSystem.CreateDirectory(ApplicationsFolder);
        Replace(file, wanted);
    }

    /// <summary>
    /// Written beside the file and renamed over it. KDE notices a changed entry by the
    /// folder's modified time, which an edit in place leaves alone.
    /// </summary>
    private void Replace(string file, string contents)
    {
        var staged = Path.Combine(
            Path.GetDirectoryName(file)!,
            $".{Path.GetFileName(file)}.{Guid.NewGuid():n}.tmp");

        try
        {
            _fileSystem.WriteAllText(staged, contents);
            _fileSystem.MoveFile(staged, file, overwrite: true);
        }
        finally
        {
            _fileSystem.DeleteFile(staged);
        }
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
        Comment=Game data tools
        Icon={EntryName}
        Exec={Quote(image)} %u
        TryExec={image}
        Categories=Development;
        MimeType={SchemeType};
        Terminal=false
        {Mark}

        """;

    private static List<string> Lines(string text) => text.Split('\n').ToList();

    /// <summary>The value of a key in the main group, ignoring every localised form of it.</summary>
    private static string? Value(List<string> lines, string key)
    {
        if (Group(lines) is not (var start, var end))
        {
            return null;
        }

        for (var index = start; index < end; index++)
        {
            if (KeyOf(lines[index]) == key)
            {
                return lines[index][(lines[index].IndexOf('=') + 1)..].Trim();
            }
        }

        return null;
    }

    /// <summary>Sets a key in the main group, adding it at the end when absent. False means it already held that.</summary>
    private static bool Set(List<string> lines, string key, string value)
    {
        if (Group(lines) is not (var start, var end))
        {
            return false;
        }

        var wanted = $"{key}={value}";

        for (var index = start; index < end; index++)
        {
            if (KeyOf(lines[index]) != key)
            {
                continue;
            }

            if (lines[index] == wanted)
            {
                return false;
            }

            lines[index] = wanted;

            return true;
        }

        lines.Insert(BeforeTrailingBlanks(lines, start, end), wanted);

        return true;
    }

    /// <summary>The lines after the main group heading, up to the next heading. Null when there is none.</summary>
    private static (int Start, int End)? Group(List<string> lines)
    {
        var heading = lines.FindIndex(line => line.Trim() == EntryGroup);

        return heading < 0 ? null : (heading + 1, SectionEnd(lines, heading));
    }

    /// <summary>The key of a key value line, which the specification lets carry spaces around the equals sign.</summary>
    private static string? KeyOf(string line)
    {
        var equals = line.IndexOf('=');

        return equals < 0 || line.TrimStart().StartsWith('#') ? null : line[..equals].Trim();
    }

    /// <summary>
    /// Names this entry as what opens a kitbash link. Edited in place, since home-manager
    /// keeps the file as a read only link and replacing it would take that link away.
    /// </summary>
    private void WriteAssociation(string id, string? removed)
    {
        var file = Path.Combine(ConfigRoot, AssociationFile);

        var lines = _fileSystem.FileExists(file)
            ? Lines(_fileSystem.ReadAllText(file))
            : [];

        var added = Associate(lines, AddedSection, existing => First(existing, id, removed));
        var defaulted = Associate(lines, DefaultsSection, _ => id);

        if (!added && !defaulted)
        {
            return;
        }

        _fileSystem.CreateDirectory(ConfigRoot);
        _fileSystem.WriteAllText(file, string.Join('\n', lines));
    }

    /// <summary>
    /// An association list with this entry first and every other one kept after it,
    /// except an entry of ours that was just removed.
    /// </summary>
    private static string First(string? existing, string id, string? removed)
    {
        var others = (existing ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(other => other != id && other != removed);

        return string.Join(';', [id, .. others]) + ";";
    }

    /// <summary>
    /// Puts the scheme's key into one section of a mimeapps.list, leaving every other
    /// section and every other key exactly as it was. False means it already held that.
    /// </summary>
    private static bool Associate(List<string> lines, string heading, Func<string?, string> value)
    {
        var section = lines.FindIndex(line => line.Trim() == heading);

        if (section < 0)
        {
            if (lines.Count > 0 && lines[^1].Trim().Length > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add(heading);
            lines.Add($"{SchemeType}={value(null)}");
            lines.Add(string.Empty);

            return true;
        }

        var end = SectionEnd(lines, section);

        var existing = lines.FindIndex(
            section + 1,
            end - section - 1,
            line => line.TrimStart().StartsWith(SchemeType + "=", StringComparison.Ordinal));

        if (existing < 0)
        {
            lines.Insert(BeforeTrailingBlanks(lines, section + 1, end), $"{SchemeType}={value(null)}");

            return true;
        }

        var current = lines[existing][(lines[existing].IndexOf('=') + 1)..].Trim();
        var wanted = $"{SchemeType}={value(current)}";

        if (lines[existing] == wanted)
        {
            return false;
        }

        lines[existing] = wanted;

        return true;
    }

    /// <summary>Everything up to the next section heading belongs to this section.</summary>
    private static int SectionEnd(List<string> lines, int heading)
    {
        var end = heading + 1;

        while (end < lines.Count && !lines[end].TrimStart().StartsWith('['))
        {
            end++;
        }

        return end;
    }

    /// <summary>
    /// Back over the blank lines that separate a section from the next, so a key lands
    /// under the ones already there rather than against the next heading.
    /// </summary>
    private static int BeforeTrailingBlanks(List<string> lines, int start, int end)
    {
        var last = end;

        while (last > start && lines[last - 1].Trim().Length == 0)
        {
            last--;
        }

        return last;
    }

    private string ApplicationsFolder => Path.Combine(DataRoot, "applications");

    private string EntryFile => Path.Combine(ApplicationsFolder, EntryName + EntryExtension);

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
