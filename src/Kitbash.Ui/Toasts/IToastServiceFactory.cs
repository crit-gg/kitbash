namespace Kitbash.Ui.Toasts;

/// <summary>
/// Makes a toast service of its own, for anything that owns its toasts rather than
/// sharing the application's.
/// </summary>
public interface IToastServiceFactory
{
    IToastService Create();
}
