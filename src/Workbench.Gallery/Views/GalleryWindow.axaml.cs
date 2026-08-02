using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Workbench.Ui.Controls;

namespace Workbench.Gallery.Views;

public partial class GalleryWindow : ChromelessWindow
{
    private readonly List<Control> _chips = [];
    private bool _disabled;

    public GalleryWindow()
    {
        InitializeComponent();

        _chips.AddRange(Chips.Children);

        foreach (var chip in _chips.OfType<Chip>())
        {
            chip.RemoveCommand = new Run(() => Chips.Children.Remove(chip));
        }

        ShowValue();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        HoldStates();
        ShowNomadLevel();
    }

    /// <summary>
    /// Pins the sample controls to one state so the five can be read side by side.
    /// </summary>
    /// <remarks>
    /// A pseudo class is normally the control's own answer to the pointer, so a pinned
    /// one would be cleared the moment a real pointer arrived. The samples opt out of
    /// hit testing for that reason, which is also honest: they are for looking at.
    /// </remarks>
    private void HoldStates()
    {
        foreach (var control in this.GetVisualDescendants().OfType<Control>())
        {
            var hover = control.Classes.Contains("forceHover");
            var pressed = control.Classes.Contains("forcePressed");
            var focus = control.Classes.Contains("forceFocus");

            if (!hover && !pressed && !focus)
            {
                continue;
            }

            var pseudo = (IPseudoClasses)control.Classes;

            // A press is always also a hover, which is why the two are set together and
            // why the theme has to order pressed after hover.
            pseudo.Set(":pointerover", hover || pressed);
            pseudo.Set(":pressed", pressed);
            pseudo.Set(":focus-visible", focus);

            control.IsHitTestVisible = false;
        }
    }

    private void OnToggleEnabled(object? sender, RoutedEventArgs e)
    {
        _disabled = !_disabled;
        Body.IsEnabled = !_disabled;
        ToggleEnabledButton.Content = _disabled ? "Enable everything" : "Disable everything";
    }

    private void OnRestoreChips(object? sender, RoutedEventArgs e)
    {
        Chips.Children.Clear();

        foreach (var chip in _chips)
        {
            Chips.Children.Add(chip);
        }
    }

    private void OnLess(object? sender, RoutedEventArgs e) => Move(-10);

    private void OnMore(object? sender, RoutedEventArgs e) => Move(10);

    private void Move(double by)
    {
        Determinate.Value = Math.Clamp(Determinate.Value + by, Determinate.Minimum, Determinate.Maximum);
        ShowValue();
    }

    private void ShowValue() => DeterminateValue.Text = $"{Determinate.Value:0}%";

    private void OnMaximize(object? sender, RoutedEventArgs e) => ToggleMaximized();

    private void OnTogglePane(object? sender, RoutedEventArgs e) =>
        Sidebar.IsPaneOpen = !Sidebar.IsPaneOpen;

    /// <summary>
    /// Moves a panel between two grounds of different depth, which is what docking will
    /// do to it. Nothing tells it its new tone and nothing recounts anything.
    /// </summary>
    private void OnMovePanel(object? sender, RoutedEventArgs e)
    {
        var home = ReferenceEquals(Nomad.Parent, ShallowGround);

        ShallowGround.Content = home ? null : Nomad;
        DeeperGround.Content = home ? Nomad : null;

        ShowNomadLevel();
    }

    private void ShowNomadLevel() =>
        NomadLevel.Text = $"it is on level {Surface.GetLevel(Nomad)}";

    // Built here rather than in a view, so the shape a real dialog takes is visible:
    // a title bar, content, and a footer holding the actions.
    private async void OnOpenDialog(object? sender, RoutedEventArgs e)
    {
        var body = new TextBlock
        {
            Text = "A dialog is a real window with the same frame and the same title bar as any other. It cannot be resized or minimised, so its title bar keeps the close button alone. There is no scrim behind it.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(16),
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };

        var dialog = new DialogWindow
        {
            Title = "Remove workspace",
            Width = 420,
            Height = 220,
        };

        var cancel = new Button { Content = "Cancel", Classes = { "ghost" } };
        var remove = new Button { Content = "Remove", Classes = { "danger" } };

        // A role rather than a handler. The dialog closes itself and answers for the
        // button that was pressed, so nothing here is wired to either one.
        Dialog.SetRole(cancel, DialogRole.Cancel);
        Dialog.SetRole(remove, DialogRole.Accept);

        // Accepting is the destructive answer here, so cancelling is the one that is
        // ready and Enter no longer reaches Remove.
        Dialog.SetTakesFocus(cancel, true);

        actions.Children.Add(cancel);
        actions.Children.Add(remove);

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
        };

        var bar = new WindowTitleBar { Title = "Remove workspace" };
        var footer = new DialogFooter { Content = actions };

        layout.Children.Add(bar);
        layout.Children.Add(body);
        layout.Children.Add(footer);
        Grid.SetRow(bar, 0);
        Grid.SetRow(body, 1);
        Grid.SetRow(footer, 2);

        dialog.Content = layout;

        var removed = await dialog.ShowDialog<bool>(this);
        DialogAnswer.Text = removed ? "The dialog said remove." : "The dialog said no.";
    }

    /// <summary>A command that runs one action. The gallery has no view models.</summary>
    private sealed class Run(Action action) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => action();
    }
}
