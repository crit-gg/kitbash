namespace Workbench.Ui.Controls;

/// <summary>How bad a <see cref="PathField"/> problem is.</summary>
public enum PathProblem
{
    None = 0,

    /// <summary>The value is allowed and the world is wrong, such as a path that has gone.</summary>
    Warn = 1,

    /// <summary>The value itself will not do.</summary>
    Error = 2,
}
