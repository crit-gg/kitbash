using Kitbash.Tools;

namespace Kitbash.Settings;

/// <summary>
/// One tool answering for itself rather than following the default. A tool with no
/// override of its own is not on the list at all.
/// </summary>
public sealed record AfterLaunchOverride(ToolId Id, AfterLaunchAction Action);
