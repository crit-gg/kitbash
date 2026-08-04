namespace Kitbash.ViewModels;

/// <summary>How a launch ended, once the dialog over it has gone.</summary>
public enum GodotLaunchOutcome
{
    /// <summary>
    /// Every step ran. Something is now open, except after a rebuild, which starts
    /// nothing.
    /// </summary>
    Finished,

    /// <summary>Stopped part way, so nothing was started.</summary>
    Cancelled,

    /// <summary>A rebuild that finished, with Open in Editor pressed.</summary>
    OpenEditor,
}
