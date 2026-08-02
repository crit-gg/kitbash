namespace Workbench.Core.Settings.Schema;

/// <summary>
/// Asks the environment about a value that is already good. Whether an engine is
/// installed or a folder exists cannot run inside a rule, because it touches the world
/// and because the answer changes after the value is written.
/// </summary>
/// <remarks>
/// A probe message means the value survived and the world is wrong, so a reader keeps the
/// origin it already worked out. A rule failure means the value did not survive. Both fill
/// the same message slot and the origin is what tells them apart.
/// </remarks>
public interface ISettingProbe
{
    /// <summary>Runs off the UI thread. Null when there is nothing to say.</summary>
    Task<string?> Inspect(object? value, CancellationToken token);
}
