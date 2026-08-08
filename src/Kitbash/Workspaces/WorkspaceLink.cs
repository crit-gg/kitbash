using Kitbash.Core.Platform;
using Kitbash.Ui.Controls;

namespace Kitbash.Workspaces;

/// <summary>
/// One address a workspace points at. The label is what the team calls the thing, so it
/// is drawn on its own and the address is never shown.
/// </summary>
/// <param name="Label">The workspace's own words for it.</param>
/// <param name="Address">Where it goes, http or https only.</param>
/// <param name="Icon">The mark at the head of the row.</param>
public sealed record WorkspaceLink(string Label, WebAddress Address, IconGlyph Icon);
