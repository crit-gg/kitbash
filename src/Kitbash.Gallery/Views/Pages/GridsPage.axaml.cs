using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Gallery.Views.Pages;

/// <summary>Both grids, over one column model.</summary>
public partial class GridsPage : GalleryPage
{
    /// <summary>The rows the row model grid holds, which it hears about for itself.</summary>
    private readonly ObservableCollection<Part> lines = [];

    /// <summary>What the validation grid holds, so a button can break a row nobody can see.</summary>
    private readonly List<Sample> checks = [];

    /// <summary>The rows the empty states grid shows while it is showing any.</summary>
    private readonly List<Part> stateRows = [];

    private readonly List<Aggregate> roots = [];

    /// <summary>The column layout that was last remembered, which stands in for the store.</summary>
    private IReadOnlyList<GridColumnState> layout = [];

    /// <summary>Which ids have been handed out, so an added row is never a second asset_1.</summary>
    private int added = 20;

    public GridsPage()
    {
        InitializeComponent();

        BuildGrids();
        BuildForms();
        BuildChecks();
        BuildStates();
        BuildModel();
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

        Key<Entry>(SampleGrid.Columns[1], entry => entry.Id);
        Key<Entry>(SampleGrid.Columns[2], entry => entry.Name);
        Key<Entry>(SampleGrid.Columns[3], entry => entry.Kind);
        Key<Entry>(SampleGrid.Columns[4], entry => entry.Value);
        Key<Entry>(SampleGrid.Columns[5], entry => entry.Tier);
        Key<Entry>(SampleGrid.Columns[6], entry => entry.State);

        // Where the reference cell's mark goes. A column that says nothing draws no mark,
        // so this is what puts one on the page.
        SampleGrid.Columns[7].Jump = item => GridJump.Text = $"opened {((Entry)item).Machine}";

        // The three columns that carry no sort key, so copy has nothing to fall back on and
        // would leave the field out of the text.
        Says<Entry>(SampleGrid.Columns[7], entry => entry.Machine);
        Says<Entry>(SampleGrid.Columns[8], entry => entry.YieldText);
        Says<Entry>(SampleGrid.Columns[9], entry => string.Join(" ", entry.Tags));

        // The three columns a person can type into. A column with no Write refuses the
        // whole paste rather than taking part of it, which is the rule being shown.
        Takes(SampleGrid.Columns[2], "NAME");
        Takes(SampleGrid.Columns[3], "KIND");
        Takes(SampleGrid.Columns[4], "VALUE");

        SampleChooser.Columns = SampleGrid.Columns;

        // The picker column has nothing to say and the identifier is what a row is, so
        // neither is something a person can turn off.
        SampleGrid.Columns[0].CanHide = false;
        SampleGrid.Columns[1].CanHide = false;

        // The keys are what a kept layout is written against, and the chooser falls back to
        // one for a column whose title is a control rather than words.
        string[] keys = ["pick", "id", "name", "kind", "value", "tier", "machine", "yield", "tags", "state"];

        for (var at = 0; at < SampleGrid.Columns.Count; at++)
        {
            SampleGrid.Columns[at].Key = keys[at];
        }

        SampleGrid.ItemsSource = new GridRows(entries);
        SampleGrid.RowModified = item => ((Entry)item).IsModified;
        SampleGrid.SelectionChanged += (_, _) => ShowPickAll();
        SamplePager.Rows = SampleGrid.Rows;

        BuildTree();
    }

    /// <summary>
    /// The hierarchy, its sort keys and its modified mark. Sorting a tree orders the
    /// children under every parent, so a key here is worth as much as one on the flat grid.
    /// </summary>
    private void BuildTree()
    {
        // The name column, since a picker stands in front of it and only a caller knows that.
        SampleTreeGrid.LeadColumn = SampleTreeGrid.Columns[1];

        Key<Aggregate>(SampleTreeGrid.Columns[1], node => node.Name);
        Key<Aggregate>(SampleTreeGrid.Columns[2], node => node.Kind);
        Key<Aggregate>(SampleTreeGrid.Columns[3], node => node.Value);
        Key<Aggregate>(SampleTreeGrid.Columns[4], node => node.Tier);
        Key<Aggregate>(SampleTreeGrid.Columns[5], node => node.State);

        roots.AddRange(
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
        ]);

        var opened = new TreeRows(roots, item => ((Aggregate)item).Children);

        // Opened, so the indent and a branch inside a branch are on the page rather than a
        // click away.
        opened.Expand(opened[0]);
        opened.Expand(opened[3]);

        SampleTreeGrid.RowModified = item => ((Aggregate)item).IsModified;
        SampleTreeGrid.ItemsSource = opened;
    }

    /// <summary>The nine kinds of cell, one column each, over eight rows.</summary>
    private void BuildForms()
    {
        string[][] tags =
        [
            ["stone", "rough"],
            ["metal", "shiny", "review"],
            ["cloth"],
            ["glass", "clear", "review", "bulk"],
        ];

        var parts = new List<Part>();

        for (var index = 0; index < 8; index++)
        {
            parts.Add(new Part(
                $"PRT_{index + 1:000}",
                index % 3 != 0,
                Part.Kinds[index % Part.Kinds.Length],
                (index + 1) * 3,
                $"The {Part.Kinds[index % Part.Kinds.Length]} in slot {index + 1}",
                $"SRC_{index + 1:000}",
                tags[index % tags.Length],
                (index + 1) / 8.0,
                new ColorValue(0.2 + (index * 0.09), 0.55, 0.9 - (index * 0.07), 1)));
        }

        Key<Part>(FormsGrid.Columns[0], part => part.Id);
        Key<Part>(FormsGrid.Columns[1], part => part.IsOn);
        Key<Part>(FormsGrid.Columns[2], part => part.Label);
        Key<Part>(FormsGrid.Columns[3], part => part.Kind);
        Key<Part>(FormsGrid.Columns[4], part => part.Count);
        Key<Part>(FormsGrid.Columns[5], part => part.Source);
        Says<Part>(FormsGrid.Columns[6], part => string.Join(" ", part.Tags));
        Key<Part>(FormsGrid.Columns[7], part => part.Done);
        Says<Part>(FormsGrid.Columns[8], part => part.TintText);

        FormsGrid.ItemsSource = new GridRows(parts);

        ShowGestures();
    }

    /// <summary>The validation grid. Two rows start wrong, so the marks are on the page.</summary>
    private void BuildChecks()
    {
        checks.AddRange(
        [
            new Sample("MTL_STONE", "Rough stone", 0, 40),
            new Sample("STONE_WET", "Wet stone", 5, 45),
            new Sample("MTL_METAL", string.Empty, 10, 60),
            new Sample("MTL_CLOTH", "Plain cloth", 20, 12),
            new Sample("MTL_GLASS", "Clear glass", 0, 100),
            new Sample("MTL_WOOD", "Sawn wood", 4, 30),
            new Sample("MTL_BONE", "Dry bone", 1, 22),
            new Sample("MTL_RESIN", "Cast resin", 3, 33),
            new Sample("MTL_WAX", "Soft wax", 2, 18),
            new Sample("MTL_IRON", "Cast iron", 8, 64),
        ]);

        Key<Sample>(ChecksGrid.Columns[0], sample => sample.Code);
        Key<Sample>(ChecksGrid.Columns[1], sample => sample.Name);
        Key<Sample>(ChecksGrid.Columns[2], sample => sample.Low);
        Key<Sample>(ChecksGrid.Columns[3], sample => sample.High);

        ChecksGrid.ItemsSource = new GridRows(checks);
    }

    /// <summary>The rows the three empty states are shown against.</summary>
    private void BuildStates()
    {
        for (var index = 0; index < 6; index++)
        {
            stateRows.Add(new Part(
                $"PRT_{index + 1:000}",
                true,
                Part.Kinds[index % Part.Kinds.Length],
                index + 1,
                $"Part number {index + 1}",
                $"SRC_{index + 1:000}",
                [],
                0,
                new ColorValue(0.5, 0.5, 0.5, 1)));
        }

        StatesGrid.ItemsSource = new GridRows(stateRows);
    }

    /// <summary>
    /// The row model grid. Its source notifies, so adding a row is heard rather than being
    /// followed by a call to rebuild, and the ids are numbered so natural ordering shows.
    /// </summary>
    private void BuildModel()
    {
        (string Id, string? Stage, int Count)[] rows =
        [
            ("asset_1", "final", 12),
            ("asset_2", "draft", 3),
            ("asset_10", "review", 44),
            ("asset_11", null, 7),
            ("asset_3", "final", 21),
            ("asset_20", "draft", 9),
        ];

        foreach (var row in rows)
        {
            lines.Add(new Part(
                row.Id,
                true,
                "mesh",
                row.Count,
                $"The mesh called {row.Id}",
                $"SRC_{row.Id}",
                [],
                0,
                new ColorValue(0.5, 0.5, 0.5, 1),
                row.Stage));
        }

        Key<Part>(ModelGrid.Columns[0], part => part.Id);
        Key<Part>(ModelGrid.Columns[1], part => part.Label);
        Key<Part>(ModelGrid.Columns[2], part => part.Stage);
        Key<Part>(ModelGrid.Columns[3], part => part.Count);

        // The one column that cannot be ordered by reading it, since draft, review and
        // final are a sequence and not three words.
        ModelGrid.Columns[2].Comparer = new StageOrder();

        ModelGrid.ItemsSource = new GridRows(lines);
        ModelGrid.SelectionChanged += (_, _) => ShowPicked();

        ShowPicked();
    }

    private static void Key<T>(GridColumn column, Func<T, object?> key) =>
        column.SortKey = item => key((T)item);

    /// <summary>What a column holds, for a column that copy has no sort key to fall back on.</summary>
    private static void Says<T>(GridColumn column, Func<T, object?> value) =>
        column.Value = item => value((T)item);

    private static void Takes(GridColumn column, string name) =>
        column.Write = (item, text) => ((Entry)item).Put(name, text);

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

    private void OnPlainGrid(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: var on })
        {
            SampleGrid.Classes.Set("plain", on is true);
        }
    }

    /// <summary>
    /// The other selection unit. Row is the default and the one the pick column belongs to,
    /// so both are on the same grid rather than the page choosing for a reader.
    /// </summary>
    private void OnCellRanges(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: var on })
        {
            SampleGrid.SelectionUnit = on is true ? GridSelectionUnit.Cell : GridSelectionUnit.Row;
        }
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

    /// <summary>
    /// The filter sits on the rows rather than on the source, so the footer still says how
    /// many rows there are as well as how many are drawn.
    /// </summary>
    private void OnFilterGrid(object? sender, TextChangedEventArgs e)
    {
        if (SampleGrid.Rows is not { } rows)
        {
            return;
        }

        var text = GridFilter.Text;

        rows.Filter = string.IsNullOrWhiteSpace(text)
            ? null
            : item => ((Entry)item).Id.Contains(text, StringComparison.OrdinalIgnoreCase)
                || ((Entry)item).Name.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A tree filter keeps the way to a match as well as the match, so a branch that does
    /// not match itself still stands when something under it does.
    /// </summary>
    private void OnFilterTree(object? sender, TextChangedEventArgs e)
    {
        if (SampleTreeGrid.ItemsSource is not TreeRows rows)
        {
            return;
        }

        var text = TreeFilter.Text;

        rows.Filter(string.IsNullOrWhiteSpace(text)
            ? null
            : item => ((Aggregate)item).Name.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The way out of the no matches state, which is the whole point of that state.</summary>
    private void OnClearTreeFilter(object? sender, RoutedEventArgs e) => TreeFilter.Text = null;

    /// <summary>Takes every edit as saved, which is what clears the amber bars.</summary>
    private void OnSaveEdits(object? sender, RoutedEventArgs e)
    {
        foreach (var row in SampleGrid.Rows ?? Enumerable.Empty<GridRow>())
        {
            (row.Item as Entry)?.Save();
        }
    }

    /// <summary>The same for the tree, where a leaf is what a person types into.</summary>
    private void OnSaveTree(object? sender, RoutedEventArgs e)
    {
        foreach (var node in roots)
        {
            SaveTree(node);
        }
    }

    private static void SaveTree(Aggregate node)
    {
        node.Save();

        foreach (var child in node.Children)
        {
            SaveTree(child);
        }
    }

    private void OnCountCells(object? sender, RoutedEventArgs e)
    {
        var rows = SampleGrid.Rows?.Count ?? 0;
        var real = SampleGrid.GetVisualDescendants().OfType<DataGridRow>().Count();
        var cells = SampleGrid.GetVisualDescendants().OfType<DataGridCell>().Count();

        GridCount.Text = $"{rows} rows, {real} of them are controls, holding {cells} cells";
    }

    /// <summary>
    /// Keeps what a person changed about the columns and nothing else. An app hands this to
    /// IGridColumnStore, and the page holds it in a field instead.
    /// </summary>
    private void OnKeepColumns(object? sender, RoutedEventArgs e)
    {
        layout = SampleGrid.Columns.Capture();

        ColumnState.Text = layout.Count switch
        {
            0 => "nothing was changed, so nothing is kept",
            1 => "1 column kept",
            _ => $"{layout.Count} columns kept",
        };
    }

    private void OnApplyColumns(object? sender, RoutedEventArgs e)
    {
        SampleGrid.Columns.Apply(layout);
        ColumnState.Text = $"{layout.Count} put back";
    }

    private void OnResetColumns(object? sender, RoutedEventArgs e)
    {
        SampleGrid.Columns.Reset();
        ColumnState.Text = "back to what the markup declared";
    }

    /// <summary>
    /// A printable key opens the editor and becomes its first character. Off by default,
    /// since it is also a stray keystroke starting an edit.
    /// </summary>
    private void OnTypeToEdit(object? sender, RoutedEventArgs e) => ShowGestures();

    /// <summary>No gesture at all, which is the read only grid.</summary>
    private void OnReadOnlyForms(object? sender, RoutedEventArgs e) => ShowGestures();

    private void ShowGestures()
    {
        if (ReadOnlyForms.IsChecked is true)
        {
            FormsGrid.BeginEditGestures = BeginEditGestures.None;
            FormsGestures.Text = "nothing opens an editor";

            return;
        }

        var gestures = BeginEditGestures.DoubleTap | BeginEditGestures.F2 | BeginEditGestures.Enter;

        if (TypeToEdit.IsChecked is true)
        {
            gestures |= BeginEditGestures.TextInput;
        }

        FormsGrid.BeginEditGestures = gestures;
        FormsGestures.Text = TypeToEdit.IsChecked is true
            ? "a double click, F2, Enter or any printable key"
            : "a double click, F2 or Enter";
    }

    /// <summary>
    /// Breaks a row nobody has scrolled to, which the footer does not hear, since a row
    /// with no container has nothing listening to it.
    /// </summary>
    private void OnBreakRow(object? sender, RoutedEventArgs e)
    {
        var last = checks[^1];

        last.Code = "IRON";
        last.Low = 90;
    }

    /// <summary>Counts what is wrong over the whole source rather than over what is drawn.</summary>
    private void OnRecheck(object? sender, RoutedEventArgs e) => ChecksGrid.Revalidate();

    private void OnFixChecks(object? sender, RoutedEventArgs e)
    {
        foreach (var sample in checks)
        {
            if (!sample.Code.StartsWith("MTL_", StringComparison.Ordinal))
            {
                sample.Code = $"MTL_{sample.Code}";
            }

            if (string.IsNullOrWhiteSpace(sample.Name))
            {
                sample.Name = "A name";
            }

            if (sample.High <= sample.Low)
            {
                sample.High = sample.Low + 10;
            }
        }

        ChecksGrid.Revalidate();
    }

    /// <summary>Whichever of the four the segmented row is on.</summary>
    private void OnGridState(object? sender, RoutedEventArgs e) => ShowState();

    private void ShowState()
    {
        StatesGrid.IsLoading = StateLoading.IsChecked is true;

        if (StateNoMatches.IsChecked is true)
        {
            StatesGrid.ItemsSource = new GridRows(stateRows);
            StatesFilter.Text = "nothing by this name";

            return;
        }

        StatesFilter.Text = null;
        StatesGrid.ItemsSource = new GridRows(StateNothing.IsChecked is true ? [] : stateRows);
    }

    /// <summary>
    /// The no matches state repeats what emptied the grid, which only the caller knows,
    /// so the cause and the way out are in the same place.
    /// </summary>
    private void OnFilterStates(object? sender, TextChangedEventArgs e)
    {
        if (StatesGrid.Rows is not { } rows)
        {
            return;
        }

        var text = StatesFilter.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            rows.Filter = null;

            return;
        }

        StatesNoMatches.Text = $"No part matches {text}.";
        rows.Filter = item => ((Part)item).Label.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private void OnClearStatesFilter(object? sender, RoutedEventArgs e)
    {
        StateRows.IsChecked = true;
        ShowState();
    }

    /// <summary>The one action the nothing yet state offers, which is what it teaches.</summary>
    private void OnFirstPart(object? sender, RoutedEventArgs e)
    {
        StateRows.IsChecked = true;
        ShowState();
    }

    /// <summary>
    /// The source is observed, so this is the whole of adding a row. The rows already on
    /// screen keep their containers and the new one is spliced in where the sort puts it.
    /// </summary>
    private void OnAddRow(object? sender, RoutedEventArgs e)
    {
        added++;

        lines.Add(new Part(
            $"asset_{added}",
            true,
            "mesh",
            added,
            $"The mesh called asset_{added}",
            $"SRC_asset_{added}",
            [],
            0,
            new ColorValue(0.5, 0.5, 0.5, 1),
            Part.Stages[added % Part.Stages.Length]));
    }

    private void OnRemoveRows(object? sender, RoutedEventArgs e)
    {
        foreach (var part in ModelGrid.PickedItems.OfType<Part>().ToList())
        {
            lines.Remove(part);
        }
    }

    /// <summary>
    /// What is picked is the item and not the wrapper the grid drew it in, which is what
    /// SelectedValue is for.
    /// </summary>
    private void ShowPicked() =>
        ModelPicked.Text = ModelGrid.SelectedValue is Part part
            ? $"the item picked is {part.Id}"
            : "nothing is picked";

    /// <summary>
    /// Orders the stages the way they happen rather than the way they are spelled. Nulls
    /// come first whichever way the column points, which is what the default does too.
    /// </summary>
    private sealed class StageOrder : IComparer<object?>
    {
        public int Compare(object? left, object? right) => Rank(left).CompareTo(Rank(right));

        private static int Rank(object? value) =>
            value is string stage ? Array.IndexOf(Part.Stages, stage) : -1;
    }
}
