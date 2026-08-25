namespace Kitbash.Ui.Controls;

/// <summary>What changed about an item, which is what says how much has to be done again.</summary>
public enum GraphChange
{
    /// <summary>Only what it looks like. Repaint and nothing else.</summary>
    Look,

    /// <summary>Its box. The index, every wire touching it and the layout all follow.</summary>
    Box,

    /// <summary>The set of items itself. Everything is worked out again.</summary>
    Set,
}
