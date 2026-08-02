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
    /// <remarks>
    /// The three things a page can do and a view model cannot are handed over here: a
    /// folder picker, a clipboard and a dialog all belong to a window, and the view model
    /// has none. It asks through a function rather than reaching for a top level.
    /// </remarks>
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

        await engines.LoadAsync();
    }

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
        if (TopLevel.GetTopLevel(this) is not Window owner
            || DataContext is not EnginesViewModel engines)
        {
            return false;
        }

        var path = engines.Shorten(engine.Directory, EnginesViewModel.DialogPathLength);

        return await UninstallDialog.For(engine, path).ShowDialog<bool>(owner);
    }
}
