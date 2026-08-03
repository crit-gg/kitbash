using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Kitbash.Core.Platform;

namespace Kitbash.Ui.Settings;

internal sealed class ApplicationRestart : IApplicationRestart
{
    private const string DotnetHost = "dotnet";

    private readonly IPlatformServices _platform;

    public ApplicationRestart(IPlatformServices platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        _platform = platform;
    }

    public bool Restart()
    {
        if (Again() is not { } request)
        {
            return false;
        }

        // Detached, so the new copy does not share a process group with the one about to
        // die and is not taken down with it.
        try
        {
            _platform.StartDetached(request);
        }
        catch (ProcessStartException)
        {
            return false;
        }

        Close();
        return true;
    }

    /// <summary>
    /// This program again, with the arguments it was given. An app launched as
    /// <c>dotnet app.dll</c> reports dotnet as its path, so the assembly goes back on the
    /// front of the arguments rather than being dropped with the program name.
    /// </summary>
    private static ProcessRequest? Again()
    {
        if (Environment.ProcessPath is not { Length: > 0 } path)
        {
            return null;
        }

        var given = Environment.GetCommandLineArgs();
        var host = Path.GetFileNameWithoutExtension(path);

        var arguments = string.Equals(host, DotnetHost, StringComparison.OrdinalIgnoreCase)
            ? given
            : given[1..];

        return ProcessRequest.Command(path, arguments);
    }

    /// <summary>
    /// Avalonia's own root, which has no injected form and is the only way a library can
    /// end the app it is running in.
    /// </summary>
    private static void Close()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
