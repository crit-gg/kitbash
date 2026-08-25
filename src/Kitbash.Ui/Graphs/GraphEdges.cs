namespace Kitbash.Ui.Controls;

/// <summary>Which sides of a box a drag is holding. A corner is two of them.</summary>
[Flags]
public enum GraphEdges
{
    None = 0,
    Left = 1,
    Right = 2,
    Top = 4,
    Bottom = 8,
}
