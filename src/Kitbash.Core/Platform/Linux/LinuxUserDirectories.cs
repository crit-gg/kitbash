using Kitbash.Core.IO;

namespace Kitbash.Core.Platform.Linux;

/// <summary>
/// The XDG base directories. Each has a variable that overrides it. The spec says a
/// value that is not an absolute path must be ignored, so the default is used instead.
/// </summary>
internal sealed class LinuxUserDirectories : IUserDirectories
{
    private readonly IEnvironment _environment;

    public LinuxUserDirectories(IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    public string ConfigurationFor(string application) =>
        Resolve("XDG_CONFIG_HOME", ".config", application);

    // The spec would put this under XDG_STATE_HOME. It goes in the data directory by
    // choice, so anything added later that is genuinely data shares the folder.
    public string StateFor(string application) =>
        Resolve("XDG_DATA_HOME", Path.Combine(".local", "share"), application);

    public string CacheFor(string application) =>
        Resolve("XDG_CACHE_HOME", ".cache", application);

    // XDG_RUNTIME_DIR has no defined fallback, and the spec says so: a login that does not
    // go through a session manager, such as ssh, leaves it unset. The cache directory is
    // the next best per user place, and a lock there works the same way.
    public string RuntimeFor(string application)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(application);

        var configured = _environment.GetVariable("XDG_RUNTIME_DIR");

        return !string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured)
            ? Path.Combine(configured, application.ToLowerInvariant())
            : CacheFor(application);
    }

    private string Resolve(string variable, string fallback, string application)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(application);

        var configured = _environment.GetVariable(variable);

        var root = !string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_environment.GetHomeDirectory(), fallback);

        // Unix names these directories in lower case, and the filesystem is case
        // sensitive, so the name is folded once here rather than at every caller.
        // Invariant because a Turkish locale would otherwise fold I to a dotless i.
        return Path.Combine(root, application.ToLowerInvariant());
    }
}
