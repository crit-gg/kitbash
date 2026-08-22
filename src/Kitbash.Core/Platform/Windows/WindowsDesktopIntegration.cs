using System.Runtime.Versioning;
using Microsoft.Win32;
using Kitbash.Core.IO;

namespace Kitbash.Core.Platform.Windows;

/// <summary>
/// Names the running copy as the program that opens a <c>kitbash</c> link. Setup.exe
/// already writes the shortcut and the uninstall entry, so this is the whole of what
/// Windows needs here.
/// </summary>
// Per user, under HKEY_CURRENT_USER, so nothing here needs elevation and a second person
// signed in keeps their own answer.
[SupportedOSPlatform("windows")]
internal sealed class WindowsDesktopIntegration : IDesktopIntegration
{
    private const string ClassesKey = @"Software\Classes\" + DeepLink.Scheme;

    /// <summary>The value that makes the key a url scheme rather than a file type.</summary>
    private const string ProtocolValue = "URL Protocol";

    /// <summary>The form Windows expects, which shows in the dialog naming the program.</summary>
    private const string Description = "URL:Kitbash Protocol";

    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;

    public WindowsDesktopIntegration(IEnvironment environment, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _environment = environment;
        _fileSystem = fileSystem;
    }

    public void Install()
    {
        if (Program() is not { } program)
        {
            return;
        }

        // The whole command line, quoted, so a path holding a space still names one
        // program and the link stays one argument.
        var command = $"\"{program}\" \"%1\"";

        try
        {
            using var scheme = Registry.CurrentUser.CreateSubKey(ClassesKey);

            if (Current(scheme) == command)
            {
                return;
            }

            scheme.SetValue(null, Description);
            scheme.SetValue(ProtocolValue, string.Empty);

            using (var icon = scheme.CreateSubKey("DefaultIcon"))
            {
                icon.SetValue(null, $"{program},0");
            }

            using var open = scheme.CreateSubKey(@"shell\open\command");

            open.SetValue(null, command);
        }
        catch (Exception exception) when (Survivable(exception))
        {
            // The app works without deep links, so a registry that will not take this is
            // survived rather than reported.
        }
    }

    public void Remove()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(ClassesKey, throwOnMissingSubKey: false);
        }
        catch (Exception exception) when (Survivable(exception))
        {
            // An uninstall carries on. A key left behind points at a program that has
            // gone, which Windows reports for itself the next time somebody follows a link.
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

    /// <summary>What the key already names, so an unchanged launch writes nothing.</summary>
    private static string? Current(RegistryKey scheme)
    {
        using var open = scheme.OpenSubKey(@"shell\open\command");

        return open?.GetValue(null) as string;
    }

    private static bool Survivable(Exception exception) =>
        exception is UnauthorizedAccessException or IOException
            or System.Security.SecurityException or ArgumentException;
}
