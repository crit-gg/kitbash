namespace Workbench.Ui.Controls;

/// <summary>
/// Where an alert sits, which is what decides how much of it there is.
/// </summary>
/// <remarks>
/// The design names a fourth, in place, which is an alert that replaces the content
/// entirely. That is the empty state and it is already built: it is the
/// <c>StackPanel.emptyState</c> class in <c>Themes/Controls/Panel.axaml</c>, on the
/// surface it stands on rather than on a semantic tint. Nothing is added here for it.
/// </remarks>
public enum AlertForm
{
    /// <summary>
    /// The default. Sits between the things it concerns, wraps freely, and carries a
    /// title, a detail and actions.
    /// </summary>
    Block,

    /// <summary>
    /// Full bleed under the header of a panel, a document or a dialog. One line, cut
    /// off rather than wrapped, with the seam of its own tier under it.
    /// </summary>
    Strip,

    /// <summary>
    /// Attached to one control, under the field. No surface and no border, an icon and
    /// one line in the tier's own text colour.
    /// </summary>
    Inline,
}
