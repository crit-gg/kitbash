using Kitbash.Settings;

namespace Kitbash;

/// <summary>
/// Carries out what a person chose to happen once the launcher has started something.
/// Behind an interface so a view model can be given one, rather than reaching for the
/// running application.
/// </summary>
public interface IAfterLaunchActions
{
    /// <summary>
    /// Safe to call from any thread, and it returns before the window has moved or the app
    /// has gone.
    /// </summary>
    void Apply(AfterLaunchAction action);
}
