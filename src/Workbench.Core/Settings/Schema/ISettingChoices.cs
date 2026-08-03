namespace Workbench.Core.Settings.Schema;

/// <summary>
/// Where a setting's options come from when they are discovered rather than declared,
/// such as the Godot engines installed on this machine.
/// </summary>
public interface ISettingChoices<T>
    where T : notnull
{
    /// <summary>Runs off the UI thread, since discovering options can touch a disk.</summary>
    Task<IReadOnlyList<SettingChoice<T>>> Offer(CancellationToken token);
}
