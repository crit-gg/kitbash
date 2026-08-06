namespace Kitbash.Settings;

/// <summary>
/// What the launcher does with itself once it has started something else. Stored by name,
/// so a hand edited file spells the value rather than a number.
/// </summary>
public enum AfterLaunchAction
{
    /// <summary>Leave the launcher where it is.</summary>
    DoNothing,

    /// <summary>Minimize the launcher window and keep running.</summary>
    Minimize,

    /// <summary>End the launcher.</summary>
    Close,
}
