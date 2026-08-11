using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// What a person changed about the columns, kept between runs. Only what differs from the
/// declaration is kept, so a grid whose tool moves on is not held to an old layout.
/// </summary>
public sealed class GridColumnStateTests
{
    /// <summary>Columns nobody touched say nothing at all.</summary>
    [AvaloniaFact]
    public void ADeclarationUntouchedKeepsNothing()
    {
        var columns = Columns();

        Assert.Empty(columns.Capture());
    }

    /// <summary>A column with no name of its own is never kept, which is the opt in.</summary>
    [AvaloniaFact]
    public void AColumnWithNoKeyIsNeverKept()
    {
        var columns = Columns();

        columns[0].Key = null;
        columns[0].IsVisible = false;
        columns[1].IsVisible = false;

        var kept = columns.Capture();

        Assert.Single(kept);
        Assert.Equal("name", kept[0].Key);
    }

    /// <summary>Each change is kept on its own, and nothing else is.</summary>
    [AvaloniaFact]
    public void OnlyWhatChangedIsKept()
    {
        var columns = Columns();

        columns.SetWidth(columns[0], 120);
        columns[1].IsPinned = true;
        columns[2].IsVisible = false;

        var kept = columns.Capture().ToDictionary(state => state.Key);

        Assert.Equal(120, kept["id"].Width);
        Assert.Null(kept["id"].IsPinned);
        Assert.Null(kept["id"].IsVisible);

        Assert.True(kept["name"].IsPinned);
        Assert.Null(kept["name"].Width);

        Assert.False(kept["kind"].IsVisible);
    }

    /// <summary>A move is kept as where the column ended up.</summary>
    [AvaloniaFact]
    public void AMoveIsKeptAsAPlace()
    {
        var columns = Columns();

        columns.Move(0, 2);

        var kept = columns.Capture().ToDictionary(state => state.Key);

        Assert.Equal(2, kept["id"].Order);
        Assert.Equal(0, kept["name"].Order);
        Assert.Equal(1, kept["kind"].Order);
    }

    /// <summary>What was kept goes back on, and a name the grid lost is passed over.</summary>
    [AvaloniaFact]
    public void AKeptLayoutGoesBackOn()
    {
        var columns = Columns();

        columns.Apply(
        [
            new GridColumnState("id", Width: 90, IsPinned: true),
            new GridColumnState("kind", IsVisible: false),
            new GridColumnState("gone", Width: 40),
        ]);

        Assert.Equal(90, columns[0].Width.Value);
        Assert.True(columns[0].IsPinned);
        Assert.False(columns[2].IsVisible);

        // Nothing was thrown for the name that is no longer there.
        Assert.Equal(3, columns.Count);
    }

    /// <summary>A kept order puts the columns back in it.</summary>
    [AvaloniaFact]
    public void AKeptOrderGoesBackOn()
    {
        var columns = Columns();

        columns.Apply(
        [
            new GridColumnState("id", Order: 2),
            new GridColumnState("name", Order: 0),
            new GridColumnState("kind", Order: 1),
        ]);

        Assert.Equal(["name", "kind", "id"], columns.Select(column => column.Key));
    }

    /// <summary>Reset puts every column back the way the tool declared it.</summary>
    [AvaloniaFact]
    public void ResetPutsTheDeclarationBack()
    {
        var columns = Columns();

        columns.SetWidth(columns[0], 120);
        columns[1].IsPinned = true;
        columns[2].IsVisible = false;
        columns.Move(0, 2);

        Assert.NotEmpty(columns.Capture());

        columns.Reset();

        Assert.Empty(columns.Capture());
        Assert.Equal(["id", "name", "kind"], columns.Select(column => column.Key));
        Assert.True(columns[2].IsVisible);
        Assert.False(columns[1].IsPinned);
    }

    private static GridColumns Columns()
    {
        var columns = new GridColumns();

        foreach (var key in (string[])["id", "name", "kind"])
        {
            columns.Add(new GridColumn
            {
                Key = key,
                Header = key.ToUpperInvariant(),
                Width = new GridLength(1, GridUnitType.Star),
            });
        }

        return columns;
    }
}
