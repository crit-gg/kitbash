using Avalonia;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The box between two points, whichever way round they are.
///
/// Avalonia's <c>Rect(Point, Point)</c> takes a top left and a bottom right and does not
/// normalise, so a drag that runs right to left or bottom to top makes a rectangle with a
/// negative width. Such a rectangle intersects nothing and draws nothing, which is how a box
/// select that only worked one way and a wire running backwards both got through.
/// </summary>
internal static class GraphBox
{
    public static Rect Between(Point a, Point b) =>
        new(
            Math.Min(a.X, b.X),
            Math.Min(a.Y, b.Y),
            Math.Abs(a.X - b.X),
            Math.Abs(a.Y - b.Y));

    /// <summary>
    /// The box holding a run of points. Not a fold of <c>Union</c> over zero sized rectangles:
    /// <c>Rect.Union</c> treats one of those as empty and answers with the other rectangle, so
    /// every point after the first is silently dropped.
    /// </summary>
    public static Rect Around(IEnumerable<Point> run)
    {
        var known = false;
        double left = 0, top = 0, right = 0, bottom = 0;

        foreach (var at in run)
        {
            if (!known)
            {
                left = right = at.X;
                top = bottom = at.Y;
                known = true;
                continue;
            }

            left = Math.Min(left, at.X);
            top = Math.Min(top, at.Y);
            right = Math.Max(right, at.X);
            bottom = Math.Max(bottom, at.Y);
        }

        return known ? new Rect(left, top, right - left, bottom - top) : default;
    }
}
