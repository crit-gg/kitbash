using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Projects;

/// <summary>
/// A page an app adds to the rail, under the list. The window draws the rail item and
/// gives the pane over to <see cref="Content"/>, and knows nothing else about it.
/// </summary>
/// <param name="Glyph">The rail item's mark.</param>
/// <param name="Label">Its tooltip and the name a screen reader says.</param>
/// <param name="Content">
/// A control, or a view model an app has a template for. It is built once and kept, so a
/// page comes back to where it was left rather than starting over.
/// </param>
public sealed record ProjectPage(IconGlyph Glyph, string Label, object Content);
