namespace Kitbash;

/// <summary>
/// Ends the launcher. Behind an interface so a view model can be given one, rather than
/// reaching for the running application.
/// </summary>
public interface IApplicationShutdown
{
    /// <summary>
    /// Closes every window and ends the app. Safe to call from any thread, and it returns
    /// before the app has gone.
    /// </summary>
    void Shutdown();
}
