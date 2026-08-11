using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// Filtering a tree. The question a flat filter never has to answer is what happens to a
/// branch that does not match but holds something that does, and the rule here is that it
/// survives, so nothing is on screen that is not a match or the way to one.
/// </summary>
public sealed class TreeFilterTests
{
    /// <summary>Nothing filtered is the tree it always was, closed at the roots.</summary>
    [Fact]
    public void NoFilterIsTheWholeTreeClosed()
    {
        var rows = Rows();

        Assert.Equal(["assets", "scenes"], Names(rows));
    }

    /// <summary>A branch survives for something under it, and opens so the match is seen.</summary>
    [Fact]
    public void ABranchSurvivesForWhatIsUnderIt()
    {
        var rows = Rows();

        rows.Filter(item => Name(item) == "goblin");

        Assert.Equal(["assets", "actors", "goblin"], Names(rows));
        Assert.True(rows[0].IsExpanded, "the branch that was kept for it is open");
        Assert.True(rows[1].IsExpanded);
        Assert.False(rows[2].HasChildren);
    }

    /// <summary>The siblings that lead nowhere are gone, path or no path.</summary>
    [Fact]
    public void WhatLeadsNowhereIsGone()
    {
        var rows = Rows();

        rows.Filter(item => Name(item) == "goblin");

        Assert.DoesNotContain("props", Names(rows));
        Assert.DoesNotContain("scenes", Names(rows));
    }

    /// <summary>
    /// A branch that matches on its own name keeps nothing under it, since a child that
    /// does not match is not a match.
    /// </summary>
    [Fact]
    public void AMatchingBranchDrawsNoCaret()
    {
        var rows = Rows();

        rows.Filter(item => Name(item) == "props");

        var props = Assert.Single(rows, row => Name(row.Item) == "props");

        Assert.False(props.HasChildren);
        Assert.Equal(["assets", "props"], Names(rows));
    }

    /// <summary>Two matches under one branch both come through, and the branch comes once.</summary>
    [Fact]
    public void TwoMatchesUnderOneBranchComeThroughOnce()
    {
        var rows = Rows();

        rows.Filter(item => Name(item).StartsWith('g'));

        Assert.Equal(["assets", "actors", "goblin", "props", "gate"], Names(rows));
    }

    /// <summary>A filter nothing matches empties the tree rather than leaving the roots.</summary>
    [Fact]
    public void NothingMatchingIsAnEmptyTree()
    {
        var rows = Rows();

        rows.Filter(_ => false);

        Assert.Empty(rows);
    }

    /// <summary>And clearing it puts the tree back, closed the way it started.</summary>
    [Fact]
    public void ClearingItPutsTheTreeBack()
    {
        var rows = Rows();

        rows.Filter(item => Name(item) == "goblin");
        rows.Filter(null);

        Assert.Equal(["assets", "scenes"], Names(rows));
        Assert.False(rows[0].IsExpanded);
    }

    /// <summary>A branch closed while filtered opens again on what the filter kept.</summary>
    [Fact]
    public void ClosingAFilteredBranchAndOpeningItAgainKeepsTheFilter()
    {
        var rows = Rows();

        rows.Filter(item => Name(item) == "goblin");
        rows.Collapse(rows[0]);

        Assert.Equal(["assets"], Names(rows));

        rows.Expand(rows[0]);

        Assert.Equal(["assets", "actors", "goblin"], Names(rows));
    }

    /// <summary>The order a sort put siblings in still holds under a filter.</summary>
    [Fact]
    public void ASortStillHoldsUnderAFilter()
    {
        var rows = Rows();

        rows.Sort(Comparer<object>.Create((left, right) =>
            string.CompareOrdinal(Name(right), Name(left))));

        rows.Filter(item => Name(item).StartsWith('g'));

        Assert.Equal(["assets", "props", "gate", "actors", "goblin"], Names(rows));
    }

    private static string Name(object item) => ((Node)item).Name;

    private static List<string> Names(TreeRows rows) => [.. rows.Select(row => Name(row.Item))];

    private static TreeRows Rows() => new(
        new[]
        {
            new Node("assets",
                new Node("actors", new Node("goblin"), new Node("wolf")),
                new Node("props", new Node("crate"), new Node("gate"))),
            new Node("scenes", new Node("town")),
        },
        item => ((Node)item).Children);

    private sealed class Node(string name, params Node[] children)
    {
        public string Name => name;

        public Node[] Children => children;
    }
}
