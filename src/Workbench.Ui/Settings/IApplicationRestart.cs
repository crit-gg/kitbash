namespace Workbench.Ui.Settings;

/// <summary>
/// Starts a fresh copy of this application and closes the one running. What a setting
/// nothing rereads needs before it means anything.
/// </summary>
public interface IApplicationRestart
{
    /// <summary>
    /// True when a new copy is running and this one is closing. False when it could not
    /// be started, in which case nothing has changed and this one is still up.
    /// </summary>
    bool Restart();
}
