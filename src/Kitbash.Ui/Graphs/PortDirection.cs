namespace Kitbash.Ui.Controls;

/// <summary>Which side of a node a port sits on.</summary>
public enum PortDirection
{
    /// <summary>Takes a value. The left edge, and it holds one wire at most.</summary>
    Input,

    /// <summary>Gives a value. The right edge, and it holds any number of wires.</summary>
    Output,
}
