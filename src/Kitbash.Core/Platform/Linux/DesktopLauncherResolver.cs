namespace Kitbash.Core.Platform.Linux;

/// <summary>
/// Picks the first launcher present on PATH. Distributions and desktops ship
/// different ones, and minimal installs may ship none, so nothing is assumed.
/// </summary>
internal sealed class DesktopLauncherResolver : IDesktopLauncherResolver
{
    /// <summary>
    /// Ordered by preference. xdg-open is the freedesktop standard and routes through
    /// portals under Flatpak and Snap, so it wins when present. The rest cover
    /// desktops that ship their own, and installs without xdg-utils.
    /// </summary>
    private static readonly DesktopLauncher[] Candidates =
    [
        new("xdg-open", []),
        new("gio", ["open"]),
        new("kde-open", []),
        new("kde-open5", []),
        new("exo-open", []),
        new("mate-open", []),
        new("gnome-open", []),
        new("wslview", []),
    ];

    private readonly IExecutableFinder _executables;
    private readonly Lock _gate = new();

    private DesktopLauncher? _resolved;

    public DesktopLauncherResolver(IExecutableFinder executables)
    {
        ArgumentNullException.ThrowIfNull(executables);
        _executables = executables;
    }

    public DesktopLauncher Resolve()
    {
        lock (_gate)
        {
            // PATH does not change while the app runs, so look once.
            if (_resolved is not null)
            {
                return _resolved;
            }

            foreach (var candidate in Candidates)
            {
                if (_executables.Find(candidate.FileName) is not null)
                {
                    _resolved = candidate;
                    return candidate;
                }
            }

            var names = string.Join(", ", Candidates.Select(candidate => candidate.FileName));

            throw new PlatformNotSupportedException(
                $"No desktop launcher was found on PATH. Install one of: {names}.");
        }
    }
}
