using Kitbash.Core.Platform;
using Kitbash.Core.Platform.Openers;
using Kitbash.Settings;
using Kitbash.Ui.Toasts;

namespace Kitbash.ViewModels;

/// <summary>
/// Makes an Open in menu. There is one per workspace on the workspaces page, so the
/// services they share arrive here once rather than at every place that builds one.
/// </summary>
public sealed class OpenInFactory
{
    private readonly IWorkspaceOpeners _openers;
    private readonly IPlatformServices _platform;
    private readonly IToastService _toasts;
    private readonly IAfterLaunchSettings _afterLaunch;
    private readonly IAfterLaunchActions _actions;

    public OpenInFactory(
        IWorkspaceOpeners openers,
        IPlatformServices platform,
        IToastService toasts,
        IAfterLaunchSettings afterLaunch,
        IAfterLaunchActions actions)
    {
        ArgumentNullException.ThrowIfNull(openers);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(toasts);
        ArgumentNullException.ThrowIfNull(afterLaunch);
        ArgumentNullException.ThrowIfNull(actions);

        _openers = openers;
        _platform = platform;
        _toasts = toasts;
        _afterLaunch = afterLaunch;
        _actions = actions;
    }

    /// <summary>An empty menu. It gathers when it is handed a workspace.</summary>
    public OpenInViewModel Create() =>
        new(_openers, _platform, _toasts, _afterLaunch, _actions);
}
