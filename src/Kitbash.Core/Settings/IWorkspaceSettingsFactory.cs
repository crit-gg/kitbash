namespace Kitbash.Core.Settings;

/// <summary>
/// Settings for a workspace named at the call, for an app that holds a list of them and
/// reads whichever one it needs. An app that opens one workspace and keeps it takes
/// <see cref="ISettingsService"/> from the container instead.
/// </summary>
public interface IWorkspaceSettingsFactory
{
    /// <summary>
    /// Reads on demand and caches until reloaded, so the result is worth holding for as
    /// long as the workspace is open and cheap to make again.
    /// </summary>
    ISettingsService For(WorkspacePaths paths);
}
