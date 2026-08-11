using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Gallery.Views.Pages;

/// <summary>Rows, trees, the diff reader and the list.</summary>
public partial class ListsPage : GalleryPage
{
    private readonly List<Node> _big = [];

    public ListsPage()
    {
        InitializeComponent();

        BuildTrees();
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

        ShowDiff(marked: true);

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

    /// <summary>One diff with every kind in it, so the whole vocabulary is on one screen.</summary>
    private static IReadOnlyList<TextDiffLine> DiffSample() =>
    [
        new TextDiffLine(TextDiffLineKind.Heading, "@@ -14,9 +14,12 @@ func _ready()"),
        new TextDiffLine(TextDiffLineKind.Context, "extends CharacterBody2D", 14, 14),
        new TextDiffLine(TextDiffLineKind.Context, "", 15, 15),
        new TextDiffLine(TextDiffLineKind.Removed, "@onready var state := $StateMachine", 16, null),
        new TextDiffLine(TextDiffLineKind.Added, "@onready var state := $Logic/StateMachine", null, 16),
        new TextDiffLine(TextDiffLineKind.Added, "@onready var hurt_box: Area2D = $HurtBox", null, 17),
        new TextDiffLine(TextDiffLineKind.Context, "", 17, 18),
        new TextDiffLine(TextDiffLineKind.Context, "func _ready() -> void:", 18, 19),
        new TextDiffLine(TextDiffLineKind.Removed, "\tstate.start(\"Idle\")", 19, null),
        new TextDiffLine(TextDiffLineKind.Added, "\tstate.start(\"Walk\")", null, 20),
        new TextDiffLine(TextDiffLineKind.Heading, "@@ merged for you"),
        new TextDiffLine(TextDiffLineKind.Settled, "\thurt_box.body_entered.connect(_on_hurt)", null, 21),
        new TextDiffLine(TextDiffLineKind.Ours, "\tvelocity.y = jump_force", null, 22),
        new TextDiffLine(TextDiffLineKind.Theirs, "\tvelocity.y = JUMP", null, 23),
        new TextDiffLine(TextDiffLineKind.Chosen, "\tvelocity.y = jump_force", null, 24),
    ];

    private void OnMarkWords(object? sender, RoutedEventArgs e) => ShowDiff(marked: true);

    private void OnLeaveWords(object? sender, RoutedEventArgs e) => ShowDiff(marked: false);

    private void ShowDiff(bool marked)
    {
        var lines = DiffSample();

        SampleDiff.Lines = marked ? new TextDiffWords().Mark(lines) : lines;

        DiffCount.Text = marked
            ? "words marked"
            : "line kinds only";
    }
}
