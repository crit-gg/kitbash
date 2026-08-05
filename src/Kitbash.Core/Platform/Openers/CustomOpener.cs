using Kitbash.Core.Settings;

namespace Kitbash.Core.Platform.Openers;

/// <summary>
/// A tool a person added because Kitbash did not find it.
/// </summary>
/// <param name="Arguments">
/// A template holding {workspace}, or blank for the workspace folder alone.
/// </param>
public sealed record CustomOpener(string Name, string Path, string Arguments);

/// <summary>The tools a person added, held in the global config.</summary>
public interface ICustomOpeners
{
    IReadOnlyList<CustomOpener> Read();

    /// <exception cref="SettingsFileUnreadableException">
    /// The file is there and could not be read, so nothing was written.
    /// </exception>
    void Write(IReadOnlyList<CustomOpener> openers);
}
