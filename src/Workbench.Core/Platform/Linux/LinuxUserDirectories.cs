using Workbench.Core.IO;

namespace Workbench.Core.Platform.Linux;

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

    private string Resolve(string variable, string fallback, string application)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(application);

        var configured = _environment.GetVariable(variable);

        var root = !string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_environment.GetHomeDirectory(), fallback);

        return Path.Combine(root, application);
    }
}
