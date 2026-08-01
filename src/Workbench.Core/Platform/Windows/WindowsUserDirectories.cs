using Workbench.Core.IO;

namespace Workbench.Core.Platform.Windows;

/// <summary>
/// The Windows application data folders. Configuration goes under the roaming folder
/// so a person's choices follow them between machines. State and cache stay local,
/// and since both live under one application folder they take a subfolder each.
/// </summary>
internal sealed class WindowsUserDirectories : IUserDirectories
{
    private readonly IEnvironment _environment;

    public WindowsUserDirectories(IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    public string ConfigurationFor(string application) =>
        Path.Combine(Roaming, Named(application));

    public string StateFor(string application) =>
        Path.Combine(Local, Named(application), "State");

    public string CacheFor(string application) =>
        Path.Combine(Local, Named(application), "Cache");

    // The variables are set on every supported version. The fallback is the layout
    // they would name, so a stripped environment still lands in the right place.
    private string Roaming => Folder("APPDATA", "Roaming");

    private string Local => Folder("LOCALAPPDATA", "Local");

    private string Folder(string variable, string fallback)
    {
        var configured = _environment.GetVariable(variable);

        return !string.IsNullOrWhiteSpace(configured)
            ? configured
            : Path.Combine(_environment.GetHomeDirectory(), "AppData", fallback);
    }

    // Windows names an application folder the way the application is written, so the
    // case is kept. Unix folds it to lower case instead.
    private static string Named(string application)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(application);
        return application;
    }
}
