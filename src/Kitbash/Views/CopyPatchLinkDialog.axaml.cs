using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Kitbash.Ui.Controls;
using Kitbash.ViewModels;

namespace Kitbash.Views;

/// <summary>Makes a patch link for the open workspace and puts it on the clipboard.</summary>
public partial class CopyPatchLinkDialog : DialogWindow
{
    public CopyPatchLinkDialog()
    {
        InitializeComponent();
    }

    private CopyPatchLinkViewModel? Model => DataContext as CopyPatchLinkViewModel;

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (Model is not { CanCopy: true } model || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        await clipboard.SetTextAsync(model.Link);

        Close(true);
    }
}
