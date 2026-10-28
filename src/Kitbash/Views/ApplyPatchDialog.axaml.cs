using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Kitbash.Ui.Controls;
using Kitbash.ViewModels;

namespace Kitbash.Views;

/// <summary>
/// Picks a patch, shows what it changes and applies it. It answers yes only once the patch
/// has landed, and it cannot be closed while git runs.
/// </summary>
public partial class ApplyPatchDialog : DialogWindow
{
    public ApplyPatchDialog()
    {
        InitializeComponent();
    }

    private ApplyPatchViewModel? Model => DataContext as ApplyPatchViewModel;

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (Model is { } model)
        {
            await model.CheckSetAsideAsync();
        }
    }

    // The frame's close glyph and Escape both arrive here, and neither may stop git part way.
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (Model is { IsWorking: true })
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    private async void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (Model is { } model && await model.ApplyAsync())
        {
            Close(true);
        }
    }

    private async void OnRestoreClick(object? sender, RoutedEventArgs e)
    {
        if (Model is { } model)
        {
            await model.RestoreAsync();
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (Model is { } model && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(model.Details);
        }
    }
}
