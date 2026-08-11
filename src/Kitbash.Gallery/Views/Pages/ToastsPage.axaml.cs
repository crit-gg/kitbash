using Avalonia.Interactivity;
using Avalonia.Threading;
using Kitbash.Ui.Toasts;

namespace Kitbash.Gallery.Views.Pages;

/// <summary>Toasts in every region, and the three alert forms.</summary>
public partial class ToastsPage : GalleryPage
{
    /// <summary>The window's own toasts, handed in rather than reached for.</summary>
    private readonly IToastService _toasts;

    /// <summary>
    /// A second service, owned by one panel on the page. Its regions are that panel's,
    /// which is what a tool panel reporting its own progress would have.
    /// </summary>
    private readonly IToastService _panelToasts;

    public ToastsPage(IToastService toasts, IToastServiceFactory scopes)
    {
        ArgumentNullException.ThrowIfNull(toasts);
        ArgumentNullException.ThrowIfNull(scopes);

        _toasts = toasts;
        _panelToasts = scopes.Create();

        InitializeComponent();

        PanelToasts.Service = _panelToasts;

        ToastRegions.ItemsSource = Enum.GetValues<ToastAnchor>();
        ToastRegions.SelectedItem = ToastAnchor.BottomRight;
    }

    /// <summary>Where the tier buttons send their toasts.</summary>
    private ToastAnchor Chosen =>
        ToastRegions.SelectedItem is ToastAnchor anchor ? anchor : ToastAnchor.BottomRight;

    private void OnToastInfo(object? sender, RoutedEventArgs e) => _toasts.Show(new ToastRequest
    {
        Anchor = Chosen,
        Title = "Workspace switched to Sandbox",
    });

    private void OnToastOk(object? sender, RoutedEventArgs e) => _toasts.Show(new ToastRequest
    {
        Anchor = Chosen,
        Tier = ToastTier.Ok,
        Title = "Saved 12 recipes",
        Body = "RCP_IronPlate_T2 and 11 others written to data/recipes.",
        Actions =
        [
            new ToastAction("Undo", () => { }),
            new ToastAction("Show in Explorer", () => { }),
        ],
    });

    private void OnToastWarn(object? sender, RoutedEventArgs e) => _toasts.Show(new ToastRequest
    {
        Anchor = Chosen,
        Tier = ToastTier.Warn,
        Title = "3 references could not be resolved",
        Body = "They were left pointing at their last known ids.",
        Actions = [new ToastAction("Show in Problems", () => { })],
    });

    private void OnToastError(object? sender, RoutedEventArgs e) => _toasts.Show(new ToastRequest
    {
        Anchor = Chosen,
        Tier = ToastTier.Error,
        Title = "Export failed",
        Body = "Godot 4.7.1 (.NET) is not installed for this workspace.",
        Actions =
        [
            new ToastAction("Install engine", () => { }) { IsPrimary = true },
            new ToastAction("View log", () => { }),
        ],
    });

    private void OnToastCompact(object? sender, RoutedEventArgs e) => _toasts.Show(new ToastRequest
    {
        Anchor = Chosen,
        Tier = ToastTier.Ok,
        Form = ToastForm.Compact,
        Title = "Copied path",
    });

    private void OnToastUndo(object? sender, RoutedEventArgs e) => _toasts.Show(new ToastRequest
    {
        Anchor = Chosen,
        Form = ToastForm.Undo,
        Title = "Deleted 4 rows",
        Actions = [new ToastAction("Undo", () => { })],
    });

    /// <summary>
    /// Long work on one card. The toast is raised first and then updated, which is the
    /// shape a download or an export takes: the handle is the whole point of Show
    /// handing one back.
    /// </summary>
    private void OnToastProgress(object? sender, RoutedEventArgs e)
    {
        var toast = _toasts.Show(new ToastRequest
        {
            Anchor = Chosen,
            Tier = ToastTier.Busy,
            Title = "Downloading Godot 4.7.1 stable",
            Body = "118 MB",
            Progress = 0,
            Actions = [new ToastAction("Cancel", () => { })],
        });

        var at = 0d;

        DispatcherTimer.Run(
            () =>
            {
                at += 0.04;
                toast.Progress = at;
                toast.Body = $"118 MB, {at:P0} of it";

                if (at < 1)
                {
                    return true;
                }

                toast.Dismiss();

                _toasts.Show(new ToastRequest
                {
                    Anchor = Chosen,
                    Tier = ToastTier.Ok,
                    Title = "Godot 4.7.1 installed",
                });

                return false;
            },
            TimeSpan.FromMilliseconds(120));
    }

    private void OnToastRepeat(object? sender, RoutedEventArgs e) => _toasts.Show(new ToastRequest
    {
        Anchor = Chosen,
        Tier = ToastTier.Warn,
        Title = "Validation warnings",
        Body = "Latest: heat exceeds the declared maximum on Arc Furnace Mk2.",
        Actions = [new ToastAction("Show all", () => { })],
    });

    /// <summary>All eight at once, which is legal and rare, and the case regions exist for.</summary>
    private void OnToastEveryRegion(object? sender, RoutedEventArgs e)
    {
        foreach (var anchor in Enum.GetValues<ToastAnchor>())
        {
            _toasts.Show(new ToastRequest
            {
                Anchor = anchor,
                Tier = ToastTier.Error,
                Form = ToastForm.Compact,
                Title = anchor.ToString(),
            });
        }
    }

    private void OnToastPanel(object? sender, RoutedEventArgs e) => _panelToasts.Show(new ToastRequest
    {
        Tier = ToastTier.Busy,
        Form = ToastForm.Compact,
        Title = "Reading the pack",
    });

    private void OnToastClear(object? sender, RoutedEventArgs e)
    {
        _toasts.DismissAll();
        _panelToasts.DismissAll();
    }
}
