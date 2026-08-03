namespace Kitbash.Core.IO;

/// <summary>
/// Where an application keeps its own files for this user on this machine. Three
/// places rather than one, because they are backed up, roamed and cleared differently.
/// </summary>
public interface IUserDirectories
{
    /// <summary>Choices a person may edit by hand.</summary>
    string ConfigurationFor(string application);

    /// <summary>
    /// What the application writes for itself and wants back next time, such as a
    /// window size or a list of recent things. Losing it resets the app, nothing more.
    /// </summary>
    string StateFor(string application);

    /// <summary>Anything that can be built again. Safe to delete at any time.</summary>
    string CacheFor(string application);
}
