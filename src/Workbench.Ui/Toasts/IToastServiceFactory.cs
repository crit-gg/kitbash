namespace Workbench.Ui.Toasts;

/// <summary>
/// Makes a toast service of its own, for anything that owns its toasts rather than
/// sharing the application's.
/// </summary>
/// <remarks>
/// A region belongs to whatever raised it. The window owns the application's, and a tool
/// panel that reports its own progress owns another, whose host clips to that panel. Two
/// services means two sets of eight regions and no way for one to reflow the other.
/// </remarks>
public interface IToastServiceFactory
{
    IToastService Create();
}
