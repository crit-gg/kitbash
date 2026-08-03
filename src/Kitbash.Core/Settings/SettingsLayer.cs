namespace Kitbash.Core.Settings;

/// <summary>
/// Which file a setting lives in. Declared lowest precedence first, so
/// <see cref="User"/> wins over <see cref="TeamShared"/>.
/// </summary>
public enum SettingsLayer
{
    /// <summary>Shared by the workspace and committed to git.</summary>
    TeamShared = 0,

    /// <summary>One person's settings for this workspace. Not committed.</summary>
    User = 1,
}
