namespace Kitbash.Core.Settings.Schema;

/// <summary>
/// Asks the environment about a value that is already good. Whether an engine is
/// installed or a folder exists cannot run inside a rule, because it touches the world
/// and because the answer changes after the value is written.
/// </summary>
public interface ISettingProbe
{
    /// <summary>Runs off the UI thread. Null when there is nothing to say.</summary>
    Task<string?> Inspect(object? value, CancellationToken token);
}
