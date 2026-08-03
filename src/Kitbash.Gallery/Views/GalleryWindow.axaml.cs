using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Toasts;

namespace Kitbash.Gallery.Views;

public partial class GalleryWindow : ChromelessWindow
{
    private readonly List<Control> _chips = [];
    private readonly List<Node> _big = [];

    /// <summary>The window's own toasts, handed in rather than reached for.</summary>
    private readonly IToastService _toasts;

    /// <summary>
    /// A second service, owned by one panel on the page. Its regions are that panel's,
    /// which is what a tool panel reporting its own progress would have.
    /// </summary>
    private readonly IToastService _panelToasts;

    private bool _disabled;

    public GalleryWindow(IToastService toasts, IToastServiceFactory scopes)
    {
        ArgumentNullException.ThrowIfNull(toasts);
        ArgumentNullException.ThrowIfNull(scopes);

        _toasts = toasts;
        _panelToasts = scopes.Create();

        InitializeComponent();

        WindowToasts.Service = _toasts;
        PanelToasts.Service = _panelToasts;

        ToastRegions.ItemsSource = Enum.GetValues<ToastAnchor>();
        ToastRegions.SelectedItem = ToastAnchor.BottomRight;

        BuildTrees();
        BuildGrids();

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

            // A press is always also a hover, so both are set and the theme must order
            // pressed after hover.
            pseudo.Set(":pointerover", hover || pressed);
            pseudo.Set(":pressed", pressed);
            pseudo.Set(":focus-visible", focus);

            control.IsHitTestVisible = false;
        }
    }

    /// <summary>
    /// The two trees. Both are flattened by <see cref="TreeRows"/>, which is what a tree
    /// is given here, and the big one is the case a nested tree of controls cannot do.
    /// </summary>
    private void BuildTrees()
    {
        var sample = new List<Node>
        {
            new("First group", "3",
            [
                new("A row"),
                new("A row that is picked"),
                new("A branch", "2",
                [
                    new("Deeper"),
                    new("Deeper again"),
                ]),
            ]),
            new("Second group", "2",
            [
                new("A leaf keeps the caret's room, so a branch and a leaf line up"),
                new("Another"),
            ]),
            new("A group with nothing in it"),
        };

        var opened = new TreeRows(sample, Under);

        // Opened, so the indent, the guides and a branch inside a branch are all on the
        // page rather than a click away.
        opened.Expand(opened[0]);
        opened.Expand(opened[3]);

        SampleTree.ItemsSource = opened;

        for (var group = 1; group <= 200; group++)
        {
            var rows = new List<Node>();

            for (var row = 1; row <= 50; row++)
            {
                rows.Add(new Node($"Row {group}.{row}"));
            }

            _big.Add(new Node($"Group {group}", rows.Count.ToString(), rows));
        }

        BigTree.ItemsSource = new TreeRows(_big, Under);
    }

    private static IEnumerable<Node> Under(object item) => ((Node)item).Children;

    private void OnExpandAll(object? sender, RoutedEventArgs e)
    {
        if (BigTree.ItemsSource is not TreeRows rows)
        {
            return;
        }

        // Backwards, so expanding one does not move the rows still to be opened.
        for (var index = rows.Count - 1; index >= 0; index--)
        {
            rows.Expand(rows[index]);
        }

        ShowRowCount();
    }

    private void OnCollapseAll(object? sender, RoutedEventArgs e)
    {
        if (BigTree.ItemsSource is TreeRows rows)
        {
            rows.Reset(_big);
            ShowRowCount();
        }
    }

    private void OnCountRows(object? sender, RoutedEventArgs e) => ShowRowCount();

    private void ShowRowCount()
    {
        var rows = BigTree.ItemsSource is TreeRows source ? source.Count : 0;
        var real = BigTree.GetVisualDescendants().OfType<TreeItem>().Count();

        RowCount.Text = $"{rows} rows, {real} of them are controls";
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

    /// <summary>Where the tier buttons send their toasts.</summary>
    private ToastAnchor Chosen =>
        ToastRegions.SelectedItem is ToastAnchor anchor ? anchor : ToastAnchor.BottomRight;

    private void OnToastInfo(object? sender, RoutedEventArgs e) => _toasts.Show(new ToastRequest
    {
        Anchor = Chosen,
        Title = "Workspace switched to Slopworks",
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


    /// <summary>
    /// Ten thousand rows for the flat grid and a small hierarchy for the tree grid. The
    /// sort keys are set here because a column takes a function and XAML cannot write one.
    /// </summary>
    private void BuildGrids()
    {
        string[] kinds = ["Table", "Graph", "Sheet", "List"];
        (PillStatus Status, string Text)[] states =
        [
            (PillStatus.Ok, "synced"),
            (PillStatus.Modified, "modified"),
            (PillStatus.Accent, "checked out"),
            (PillStatus.Error, "conflict"),
            (PillStatus.Neutral, "archived"),
        ];

        var entries = new List<Entry>();

        for (var index = 1; index <= 10_000; index++)
        {
            var state = states[index % states.Length];

            entries.Add(new Entry(
                $"ROW_{index:0000}_{kinds[index % kinds.Length]}",
                $"Row number {index}",
                kinds[index % kinds.Length],
                Math.Round(1 + (index % 97) * 0.5, 1),
                index % 5,
                state.Status,
                state.Text));
        }

        Key(SampleGrid.Columns[1], entry => entry.Id);
        Key(SampleGrid.Columns[2], entry => entry.Name);
        Key(SampleGrid.Columns[3], entry => entry.Kind);
        Key(SampleGrid.Columns[4], entry => entry.Value);
        Key(SampleGrid.Columns[5], entry => entry.Tier);
        Key(SampleGrid.Columns[6], entry => entry.State);

        SampleGrid.ItemsSource = new GridRows(entries);
        SampleGrid.SelectionChanged += (_, _) => ShowPickAll();
        SamplePager.Rows = SampleGrid.Rows;

        // The name column, since a picker stands in front of it and only a caller knows that.
        SampleTreeGrid.LeadColumn = SampleTreeGrid.Columns[1];

        List<Aggregate> roots =
        [
            new("First branch", "4 kinds", "avg 9.4", "1.8x", "3 modified",
            [
                new("A leaf", "Table", "6.0", "1.0x", "synced"),
                new("Another leaf", "Table", "8.0", "2.4x", "modified"),
                new("A branch inside it", "Graph", "14.0", "3.1x", "checked out",
                [
                    new("Deeper", "Graph", "16.5", "2.8x", "synced"),
                    new("Deeper again", "Graph", "11.0", "2.2x", "synced"),
                ]),
            ]),
            new("Second branch", "9 kinds", "avg 4.2", "1.2x", "all synced",
            [
                new("A leaf keeps the caret's room", "Sheet", "4.0", "1.1x", "synced"),
                new("Another", "Sheet", "4.4", "1.3x", "synced"),
            ]),
            new("A branch with nothing in it", "0 kinds", "-", "-", "empty"),
        ];

        var opened = new TreeRows(roots, item => ((Aggregate)item).Children);

        // Opened, so the indent and a branch inside a branch are on the page rather than a
        // click away.
        opened.Expand(opened[0]);
        opened.Expand(opened[3]);

        SampleTreeGrid.ItemsSource = opened;
    }

    private static void Key(GridColumn column, Func<Entry, object?> key) =>
        column.SortKey = item => key((Entry)item);

    /// <summary>Picks every row on the page, or none of them.</summary>
    private void OnPickAll(object? sender, RoutedEventArgs e)
    {
        if (PickAll.IsChecked == true)
        {
            SampleGrid.SelectAll();
        }
        else
        {
            SampleGrid.UnselectAll();
        }
    }

    private void ShowPickAll()
    {
        var picked = SampleGrid.SelectedItems?.Count ?? 0;

        PickAll.IsChecked = picked == 0 ? false : picked >= SampleGrid.ItemCount ? true : null;
    }

    private void OnGroupGrid(object? sender, RoutedEventArgs e)
    {
        if (SampleGrid.Rows is not { } rows || sender is not Button button)
        {
            return;
        }

        var grouped = rows.GroupKey is not null;

        rows.Group(grouped ? null : item => ((Entry)item).Kind);
        button.Content = grouped ? "Group by kind" : "Drop the grouping";
    }

    private void OnPageGrid(object? sender, RoutedEventArgs e)
    {
        if (SampleGrid.Rows is not { } rows || sender is not Button button)
        {
            return;
        }

        var paging = rows.PageSize > 0;

        rows.PageSize = paging ? 0 : SamplePager.PageSize;
        SamplePager.IsVisible = !paging;
        button.Content = paging ? "Turn paging on" : "Turn paging off";
    }

    /// <summary>Hides a column, which is the whole of what a column chooser would do.</summary>
    private void OnHideColumn(object? sender, RoutedEventArgs e)
    {
        var column = SampleGrid.Columns[3];

        column.IsVisible = !column.IsVisible;
    }

    private void OnCountCells(object? sender, RoutedEventArgs e)
    {
        var rows = SampleGrid.Rows?.Count ?? 0;
        var real = SampleGrid.GetVisualDescendants().OfType<DataGridRow>().Count();
        var cells = SampleGrid.GetVisualDescendants().OfType<DataGridCell>().Count();

        GridCount.Text = $"{rows} rows, {real} of them are controls, holding {cells} cells";
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
