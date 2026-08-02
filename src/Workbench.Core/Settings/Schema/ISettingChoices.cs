namespace Workbench.Core.Settings.Schema;

/// <summary>
/// Where a setting's options come from when they are discovered rather than declared,
/// such as the Godot engines installed on this machine.
/// </summary>
/// <remarks>
/// A source offers options and never invalidates. A pinned value that is not on the list
/// is still the right value, so it is kept and a probe is what says the world is missing
/// something. Use <see cref="ChoiceRule{T}"/> when the set really is closed.
/// </remarks>
public interface ISettingChoices<T>
    where T : notnull
{
    /// <summary>Runs off the UI thread, since discovering options can touch a disk.</summary>
    Task<IReadOnlyList<SettingChoice<T>>> Offer(CancellationToken token);
}
