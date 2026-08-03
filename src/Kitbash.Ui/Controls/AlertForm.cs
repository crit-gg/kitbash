namespace Kitbash.Ui.Controls;

/// <summary>
/// Where an alert sits, which is what decides how much of it there is.
/// </summary>
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
