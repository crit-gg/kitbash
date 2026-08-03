using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Workbench.Core.Godot;
using Workbench.ViewModels;

namespace Workbench.Views;

public partial class EnginesPage : UserControl
{
    public EnginesPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The first read happens when the page is first shown rather than when the window is
    /// built. It fetches over a network, so doing it at launch would make every start wait
    /// on one whether or not anyone opened this page.
    /// </summary>
    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is not EnginesViewModel engines)
        {
            return;
        }

        engines.Copier ??= CopyAsync;
        engines.Picker ??= PickAsync;
        engines.Confirm ??= ConfirmAsync;
        engines.Reveal ??= Reveal;

        await engines.LoadAsync();
    }

    /// <summary>Scrolls a card into view. Does not touch the filter.</summary>
    private void Reveal(ReleaseCardViewModel card) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () => CardList.ScrollIntoView(card),
            Avalonia.Threading.DispatcherPriority.Background);

    private async Task CopyAsync(string text)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private async Task<string?> PickAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
        {
            return null;
        }

        var picked = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Pick the folder the Godot editor is in",
            AllowMultiple = false,
        });

        return picked.Count == 0 ? null : picked[0].Path.LocalPath;
    }

    private async Task<bool> ConfirmAsync(InstalledEngine engine)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return false;
        }

        return await UninstallDialog.For(engine).ShowDialog<bool>(owner);
    }
}
