namespace Kitbash.Tools;

/// <summary>What starting a tool means.</summary>
public enum ToolKind
{
    /// <summary>A program that opens a window and outlives the launcher.</summary>
    App,

    /// <summary>A job that runs to the end under a modal, reporting as it goes.</summary>
    Script,
}
