namespace Workbench.Ui.Controls;

/// <summary>One entry in a pager's row of page numbers, or the gap between two runs.</summary>
public sealed class GridPagerPage
{
    internal GridPagerPage(int number, bool current)
    {
        Number = number;
        IsCurrent = current;
    }

    private GridPagerPage()
    {
        IsEllipsis = true;
    }

    /// <summary>The page this entry goes to. Zero on the gap.</summary>
    public int Number { get; }

    /// <summary>Whether this is the page being shown.</summary>
    public bool IsCurrent { get; }

    /// <summary>Whether this stands for the pages left out rather than for one page.</summary>
    public bool IsEllipsis { get; }

    /// <summary>Whether this entry can be pressed.</summary>
    public bool IsPage => !IsEllipsis;

    internal static GridPagerPage Gap() => new();
}
