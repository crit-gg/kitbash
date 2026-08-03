using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The right click menu every text field carries. Fluent's TextBox theme holds one and a
/// control theme replaces rather than extends, so this puts it back.
/// </summary>
public class TextMenu : AvaloniaObject
{
    /// <summary>
    /// Whether the field carries the menu. Set for every field by a style in
    /// Themes/Controls/TextBox.axaml, so a field that wants none says so.
    /// </summary>
    public static readonly AttachedProperty<bool> ShowsProperty =
        AvaloniaProperty.RegisterAttached<TextMenu, TextBox, bool>("Shows");

    static TextMenu()
    {
        ShowsProperty.Changed.AddClassHandler<TextBox, bool>(OnShowsChanged);
    }

    public static void SetShows(TextBox field, bool value) => field.SetValue(ShowsProperty, value);

    public static bool GetShows(TextBox field) => field.GetValue(ShowsProperty);

    private static void OnShowsChanged(TextBox field, AvaloniaPropertyChangedEventArgs<bool> change)
    {
        field.ContextMenu = change.GetNewValue<bool>() ? Build(field) : null;
    }

    /// <summary>
    /// A ContextMenu rather than a MenuFlyout, because it opens at the pointer and takes
    /// its own width. A MenuFlyout draws through MenuFlyoutPresenter, which is the dropdown
    /// menu and is pinned under its target and held to its width.
    /// </summary>
    private static ContextMenu Build(TextBox field)
    {
        // Undoing is refused on a read only field the same way typing is, which is what
        // TextBox.OnKeyDown does with the same two keys.
        var undoable = () => field.IsUndoEnabled && !field.IsReadOnly;

        var items = new List<Entry>
        {
            new("Undo", field.Undo, () => undoable() && field.CanUndo, keys => keys?.Undo),
            new("Redo", field.Redo, () => undoable() && field.CanRedo, keys => keys?.Redo),
            new("Cut", field.Cut, () => field.CanCut, keys => keys?.Cut),
            new("Copy", field.Copy, () => field.CanCopy, keys => keys?.Copy),
            new("Paste", () => field.Paste(), () => field.CanPaste, keys => keys?.Paste),

            // Emptying the selection is the delete. It snapshots for undo on the way, and
            // CanCut already means there is a selection and the field is writable.
            new("Delete", () => field.SelectedText = string.Empty, () => field.CanCut, _ => Delete),

            new("Select all", field.SelectAll, () => field.Text?.Length > 0, keys => keys?.SelectAll),
        };

        var menu = new ContextMenu();

        foreach (var entry in items)
        {
            // Two rules and three groups, the order every desktop prints them in.
            if (entry.Header is "Cut" or "Select all")
            {
                menu.Items.Add(new Separator());
            }

            menu.Items.Add(entry.Item);
        }

        menu.Opening += (_, _) => Ready(items, field);

        return menu;
    }

    /// <summary>
    /// What is on and what each shortcut reads, worked out when the menu opens. The state
    /// is Avalonia's own and it recomputes it on every selection change.
    /// </summary>
    private static void Ready(List<Entry> items, TextBox field)
    {
        // The same object TextBox matches a key against, so a hint and the key that works
        // cannot disagree on either OS. TopLevel.PlatformSettings is private in 12.1.1 and
        // Application.Current is the only way to it, which is what TextBox.CutGesture does.
        var keys = Application.Current?.PlatformSettings?.HotkeyConfiguration;

        foreach (var entry in items)
        {
            entry.Item.IsEnabled = entry.Enabled();
            entry.Item.InputGesture = entry.Gesture(keys)?.FirstOrDefault();
        }
    }

    private static readonly List<KeyGesture> Delete = [new(Key.Delete)];

    /// <summary>One row, and the three things that decide what it says and whether it is on.</summary>
    private sealed class Entry
    {
        internal Entry(
            string header,
            Action run,
            Func<bool> enabled,
            Func<PlatformHotkeyConfiguration?, List<KeyGesture>?> gesture)
        {
            Header = header;
            Enabled = enabled;
            Gesture = gesture;
            Item = new MenuItem { Header = header };

            Item.Click += (_, e) =>
            {
                run();
                e.Handled = true;
            };
        }

        internal string Header { get; }

        internal MenuItem Item { get; }

        internal Func<bool> Enabled { get; }

        internal Func<PlatformHotkeyConfiguration?, List<KeyGesture>?> Gesture { get; }
    }
}
