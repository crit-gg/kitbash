using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Kitbash.Core.IO;

namespace Kitbash.Core.Platform.Windows;

/// <summary>
/// Names the running copy as the program that opens a <c>kitbash</c> link, and offers it
/// for a <c>project.godot</c>. Setup.exe already writes the shortcut and the uninstall entry.
/// </summary>
// Per user, under HKEY_CURRENT_USER, so nothing here needs elevation and a second person
// signed in keeps their own answer.
[SupportedOSPlatform("windows")]
internal sealed partial class WindowsDesktopIntegration : IDesktopIntegration
{
    private const string Classes = @"Software\Classes\";

    private const string SchemeKey = Classes + DeepLink.Scheme;

    /// <summary>The value that makes the key a url scheme rather than a file type.</summary>
    private const string ProtocolValue = "URL Protocol";

    /// <summary>The form Windows expects, which shows in the dialog naming the program.</summary>
    private const string Description = "URL:Kitbash Protocol";

    // Windows associates by extension, and in Godot 4 only project.godot carries this one.
    private const string ExtensionKey = Classes + ".godot";

    private const string ProgId = "Kitbash.GodotProject";

    private const string ProgIdKey = Classes + ProgId;

    private const string ProjectDescription = "Godot project";

    /// <summary>Lists a program in Open with without making it the default.</summary>
    private const string OpenWithKey = ExtensionKey + @"\OpenWithProgids";

    private const int AssociationsChanged = 0x08000000;

    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;

    public WindowsDesktopIntegration(IEnvironment environment, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _environment = environment;
        _fileSystem = fileSystem;
    }

    // A choice made in the Open with dialog is kept under UserChoice, which outranks the
    // default written here and cannot be written by a program.
    public void Install(bool projectsByDefault)
    {
        if (Program() is not { } program)
        {
            return;
        }

        // The whole command line, quoted, so a path holding a space still names one
        // program and the argument stays one argument.
        var command = $"\"{program}\" \"%1\"";
        var icon = $"{program},0";

        try
        {
            var changed = false;

            using (var scheme = Registry.CurrentUser.CreateSubKey(SchemeKey))
            {
                changed |= Ensure(scheme, null, Description);
                changed |= Ensure(scheme, ProtocolValue, string.Empty);
                changed |= EnsureSub(scheme, "DefaultIcon", icon);
                changed |= EnsureSub(scheme, @"shell\open\command", command);
            }

            using (var type = Registry.CurrentUser.CreateSubKey(ProgIdKey))
            {
                changed |= Ensure(type, null, ProjectDescription);
                changed |= EnsureSub(type, "DefaultIcon", icon);
                changed |= EnsureSub(type, @"shell\open\command", command);
            }

            using (var openWith = Registry.CurrentUser.CreateSubKey(OpenWithKey))
            {
                changed |= Ensure(openWith, ProgId, string.Empty);
            }

            using (var extension = Registry.CurrentUser.CreateSubKey(ExtensionKey))
            {
                changed |= projectsByDefault
                    ? Ensure(extension, null, ProgId)
                    : GiveBack(extension);
            }

            if (changed)
            {
                Announce();
            }
        }
        catch (Exception exception) when (Survivable(exception))
        {
            // The app works without deep links or file types, so a registry that will not
            // take this is survived rather than reported.
        }
    }

    public void Remove()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(SchemeKey, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(ProgIdKey, throwOnMissingSubKey: false);

            using (var openWith = Registry.CurrentUser.OpenSubKey(OpenWithKey, writable: true))
            {
                openWith?.DeleteValue(ProgId, throwOnMissingValue: false);
            }

            using (var extension = Registry.CurrentUser.OpenSubKey(ExtensionKey, writable: true))
            {
                if (extension is not null)
                {
                    GiveBack(extension);
                }
            }

            Announce();
        }
        catch (Exception exception) when (Survivable(exception))
        {
            // An uninstall carries on. A key left behind points at a program that has
            // gone, which Windows reports for itself the next time somebody uses it.
        }
    }

    /// <summary>
    /// The program to register, or null when there is none worth registering. A copy
    /// started as <c>dotnet app.dll</c> is somebody working on Kitbash and takes over
    /// nothing, which is the same answer Linux gives outside an AppImage.
    /// </summary>
    private string? Program() =>
        _environment.GetProcessCommand() is [{ Length: > 0 } path] && _fileSystem.FileExists(path)
            ? path
            : null;

    /// <summary>Writes a string value only when it differs, so an unchanged launch writes nothing.</summary>
    private static bool Ensure(RegistryKey key, string? name, string value)
    {
        if (key.GetValue(name) as string == value)
        {
            return false;
        }

        key.SetValue(name, value);

        return true;
    }

    private static bool EnsureSub(RegistryKey key, string path, string value)
    {
        using var sub = key.CreateSubKey(path);

        return Ensure(sub, null, value);
    }

    /// <summary>Clears the extension's default only when it names this program.</summary>
    private static bool GiveBack(RegistryKey extension)
    {
        if (extension.GetValue(null) as string != ProgId)
        {
            return false;
        }

        extension.DeleteValue(string.Empty, throwOnMissingValue: false);

        return true;
    }

    /// <summary>Explorer caches associations until it is told they changed.</summary>
    private static void Announce() => SHChangeNotify(AssociationsChanged, 0, IntPtr.Zero, IntPtr.Zero);

    [LibraryImport("shell32.dll")]
    private static partial void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    private static bool Survivable(Exception exception) =>
        exception is UnauthorizedAccessException or IOException
            or System.Security.SecurityException or ArgumentException;
}
