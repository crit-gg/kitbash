using Kitbash.Core.IO;

namespace Kitbash.Core.Platform.Linux;

/// <summary>
/// Takes the AppImage's own directories back out of the variables a child would inherit,
/// so a program on this machine is found and run instead of one of ours.
/// </summary>
internal sealed class AppImageEnvironment : IBundleEnvironment
{
    /// <summary>The AppImage runtime sets this to the mount point. Unset means no bundle.</summary>
    private const string MountPoint = "APPDIR";

    /// <summary>
    /// PATH is the one Velopack's AppRun sets, putting the whole published output on the
    /// front of it. The rest are what other AppRun scripts set, since a hand built AppDir
    /// is passed through untouched, and each costs nothing when it is unset.
    /// </summary>
    private static readonly string[] Inherited =
    [
        "PATH",
        "LD_LIBRARY_PATH",
        "LD_PRELOAD",
        "PYTHONHOME",
        "PYTHONPATH",
        "PERLLIB",
        "GSETTINGS_SCHEMA_DIR",
        "QT_PLUGIN_PATH",
        "GTK_PATH",
        "GDK_PIXBUF_MODULE_FILE",
        "XDG_DATA_DIRS",
    ];

    public AppImageEnvironment(IEnvironment environment, IPathRules paths)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(paths);

        Outside = Build(environment, paths);
    }

    public IReadOnlyDictionary<string, string> Outside { get; }

    /// <summary>
    /// Read once. Nothing in this process changes its own environment, and the mount
    /// point is fixed for as long as the AppImage is running.
    /// </summary>
    private static IReadOnlyDictionary<string, string> Build(IEnvironment environment, IPathRules paths)
    {
        var overlay = new Dictionary<string, string>(StringComparer.Ordinal);
        var mount = environment.GetVariable(MountPoint);

        if (string.IsNullOrWhiteSpace(mount))
        {
            return overlay;
        }

        foreach (var name in Inherited)
        {
            if (Strip(environment.GetVariable(name), mount, paths) is { } corrected)
            {
                overlay[name] = corrected;
            }
        }

        return overlay;
    }

    /// <summary>
    /// The value without its entries under the mount point, or null when there was
    /// nothing to take out, so a variable nobody touched stays off the overlay.
    /// </summary>
    private static string? Strip(string? value, string mount, IPathRules paths)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var entries = value.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var kept = entries.Where(entry => !IsInside(entry, mount, paths)).ToArray();

        if (kept.Length == entries.Length)
        {
            return null;
        }

        // Empty is how ProcessRequest says unset, which is what a variable that held
        // nothing but our own directories should become.
        return string.Join(Path.PathSeparator, kept);
    }

    private static bool IsInside(string entry, string mount, IPathRules paths)
    {
        try
        {
            return paths.AreSame(entry, mount) || paths.Contains(mount, entry);
        }
        catch (ArgumentException)
        {
            // An entry that will not parse as a path is left alone. Keeping something
            // that was already there cannot break a program that was working.
            return false;
        }
    }
}
