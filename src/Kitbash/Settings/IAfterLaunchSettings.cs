using Kitbash.Tools;

namespace Kitbash.Settings;

/// <summary>
/// What the launcher does with itself after it has started something. Read again at every
/// launch, so a change applies at once.
/// </summary>
public interface IAfterLaunchSettings
{
    /// <summary>Once a project manager is running.</summary>
    AfterLaunchAction AfterProjectManager { get; }

    /// <summary>Once a project is open in the editor.</summary>
    AfterLaunchAction AfterEditor { get; }

    /// <summary>Once a project is running.</summary>
    AfterLaunchAction AfterPlay { get; }

    /// <summary>Once a tool from the Open in menu is running.</summary>
    AfterLaunchAction AfterExternalTool { get; }

    /// <summary>Once a Kitbash tool is running, for every tool that says nothing itself.</summary>
    AfterLaunchAction AfterTool { get; }

    /// <summary>This tool's own answer, and <see cref="AfterTool"/> when it has none.</summary>
    AfterLaunchAction ForTool(ToolId id);
}
