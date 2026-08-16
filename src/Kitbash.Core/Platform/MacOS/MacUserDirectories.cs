using Kitbash.Core.IO;

namespace Kitbash.Core.Platform.MacOS;

/// <summary>
/// The Library folders Apple names for an application. Configuration and state are two
/// directories, because both files are called kitbash.toml and one would overwrite the other.
/// </summary>
internal sealed class MacUserDirectories : IUserDirectories
{
    private readonly IEnvironment _environment;

    public MacUserDirectories(IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    // Not ~/Library/Preferences. That belongs to NSUserDefaults, which caches and rewrites
    // what is in it, and hand edited TOML has no business there.
    public string ConfigurationFor(string application) =>
        Path.Combine(Support(application), "Config");

    public string StateFor(string application) =>
        Path.Combine(Support(application), "State");

    // The OS may purge this under disk pressure and Time Machine skips it, which is what a
    // cache is for. Engines and tools are state and never come here.
    public string CacheFor(string application) =>
        Path.Combine(Root("Caches"), Named(application));

    // macOS has no runtime directory. TMPDIR is swept of anything untouched for three days,
    // and a lock file deleted while it is held lets a second launcher take a new one.
    public string RuntimeFor(string application) => CacheFor(application);

    private string Support(string application) =>
        Path.Combine(Root("Application Support"), Named(application));

    private string Root(string folder) => Path.Combine(Home(), "Library", folder);

    /// <summary>
    /// Somewhere real when there is no home directory, so a missing HOME writes under the
    /// temporary directory rather than making a Library folder wherever the app was started.
    /// </summary>
    private string Home()
    {
        var home = _environment.GetHomeDirectory();

        if (!string.IsNullOrWhiteSpace(home) && Path.IsPathRooted(home))
        {
            return home;
        }

        var temporary = _environment.GetVariable("TMPDIR");

        return !string.IsNullOrWhiteSpace(temporary) && Path.IsPathRooted(temporary)
            ? temporary
            : "/tmp";
    }

    // Every folder under Application Support carries its application's own spelling, so the
    // name is not folded the way Linux folds it.
    private static string Named(string application)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(application);
        return application;
    }
}
