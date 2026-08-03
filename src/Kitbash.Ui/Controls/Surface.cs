using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Reactive;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The depth ramp. <see cref="LevelProperty"/> says which tone a place in the tree sits
/// on, and <see cref="NestsProperty"/> marks an element that starts a new one.
/// </summary>
public class Surface
{
    /// <summary>
    /// The tone at this point in the tree. 0 is the root, then 1, 2 and 3 cycle. It is
    /// inherited, so a control reads the surface it is standing on without being given it.
    /// </summary>
    public static readonly AttachedProperty<int> LevelProperty =
        AvaloniaProperty.RegisterAttached<Surface, Control, int>("Level", inherits: true);

    /// <summary>
    /// This element is a surface of its own, so it takes one step down from its parent
    /// and gives that step to everything inside it.
    /// </summary>
    public static readonly AttachedProperty<bool> NestsProperty =
        AvaloniaProperty.RegisterAttached<Surface, Control, bool>("Nests");

    /// <summary>
    /// What the level is following, held on the element itself rather than in a table, so
    /// nothing here keeps a control alive after its window has gone.
    /// </summary>
    private static readonly AttachedProperty<IDisposable?> FollowingProperty =
        AvaloniaProperty.RegisterAttached<Surface, Control, IDisposable?>("Following");

    /// <summary>The last tone before the cycle starts again.</summary>
    private const int Deepest = 3;

    static Surface()
    {
        NestsProperty.Changed.AddClassHandler<Control, bool>(OnNestsChanged);
    }

    public static int GetLevel(Control control) => control.GetValue(LevelProperty);

    public static void SetLevel(Control control, int value) => control.SetValue(LevelProperty, value);

    public static bool GetNests(Control control) => control.GetValue(NestsProperty);

    public static void SetNests(Control control, bool value) => control.SetValue(NestsProperty, value);

    /// <summary>
    /// One step down. The root tone is never returned, since it belongs to the window and
    /// its chrome alone.
    /// </summary>
    public static int Deeper(int level) => level >= Deepest ? 1 : level + 1;

    private static void OnNestsChanged(Control control, AvaloniaPropertyChangedEventArgs<bool> change)
    {
        control.AttachedToLogicalTree -= OnAttached;
        control.DetachedFromLogicalTree -= OnDetached;
        StopFollowing(control);

        if (!change.GetNewValue<bool>())
        {
            control.ClearValue(LevelProperty);
            return;
        }

        control.AttachedToLogicalTree += OnAttached;
        control.DetachedFromLogicalTree += OnDetached;

        // A theme sets this while the control is already in a tree, and a view sets it
        // before there is one. Only the first case has a parent to read.
        if (control.Parent is not null)
        {
            Follow(control);
        }
    }

    private static void OnAttached(object? sender, LogicalTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
        {
            Follow(control);
        }
    }

    private static void OnDetached(object? sender, LogicalTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
        {
            StopFollowing(control);
        }
    }

    /// <summary>
    /// Takes the parent's tone and holds one step below it, for as long as this element
    /// stays where it is.
    /// </summary>
    private static void Follow(Control control)
    {
        StopFollowing(control);

        if (control.Parent is not Control parent)
        {
            // Nothing above, so this is standing on the root.
            control.SetValue(LevelProperty, Deeper(0));
            return;
        }

        control.SetValue(FollowingProperty, parent.GetObservable(LevelProperty).Subscribe(
            new AnonymousObserver<int>(level => control.SetValue(LevelProperty, Deeper(level)))));
    }

    private static void StopFollowing(Control control)
    {
        control.GetValue(FollowingProperty)?.Dispose();
        control.ClearValue(FollowingProperty);
    }
}
