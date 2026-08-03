using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Kitbash.Core.Settings.Schema;
using Kitbash.Ui.Settings;

namespace Kitbash.Ui.Controls;

/// <summary>
/// One app's settings, drawn from its schema. Every app owns its own window, so nothing
/// here knows anything about the app whose settings it is showing.
/// </summary>
public partial class SettingsWindow : ChromelessWindow
{
    private SettingsTreeNode? _open;

    /// <summary>The tree is answering a selection it made itself.</summary>
    private bool _picking;

    public SettingsWindow()
    {
        InitializeComponent();

        Opened += OnOpened;
        Activated += OnActivated;
    }

    private SettingsWindowViewModel? Model => DataContext as SettingsWindowViewModel;

    /// <summary>
    /// Copies one line of a readout. The menu sits inside the line's template, so its
    /// data context is the line, and what goes on the clipboard is the whole value rather
    /// than the shortened form the row draws.
    /// </summary>
    private async void OnCopyLine(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: SettingsListEntry line })
        {
            return;
        }

        // Avalonia 12 replaced SetTextAsync with a format and a value.
        if (Clipboard is { } clipboard)
        {
            await clipboard.SetValueAsync(DataFormat.Text, line.Copied);
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (Model?.First() is { } first)
        {
            await Show(first);
        }
    }

    /// <summary>
    /// Nothing watches the filesystem and nothing watches the workspace list, so both are
    /// invisible until this reads them again. Coming to the front is when it does.
    /// </summary>
    private async void OnActivated(object? sender, EventArgs e)
    {
        if (Model is not { } model)
        {
            return;
        }

        // The tree is rebuilt whole, which empties the selection on the way through. The
        // mark goes back on before anything reacts to that.
        _picking = true;

        try
        {
            model.Refresh();
            Restore(model);
        }
        finally
        {
            _picking = false;
        }

        await model.ReloadAsync();
    }

    private async void OnPageSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_picking || Model is not { } model)
        {
            return;
        }

        var node = (Pages.SelectedItem as TreeRow)?.Item as SettingsTreeNode;

        // A store opens nothing, so picking one puts the mark back on the page that is
        // open rather than leaving a heading looking like the current page.
        if (node is null || node.IsGroup)
        {
            Restore(model);
            return;
        }

        _open = node;
        await model.OpenAsync(node);
    }

    private async Task Show(SettingsTreeNode node)
    {
        if (Model is not { } model)
        {
            return;
        }

        _open = node;
        Restore(model);
        await model.OpenAsync(node);
    }

    private void Restore(SettingsWindowViewModel model)
    {
        _picking = true;

        try
        {
            Pages.SelectedIndex = _open is null ? -1 : model.IndexOf(_open);
        }
        finally
        {
            _picking = false;
        }
    }
}
