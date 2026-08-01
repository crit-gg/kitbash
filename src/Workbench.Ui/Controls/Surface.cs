using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Reactive;

namespace Workbench.Ui.Controls;

/// <summary>
/// The depth ramp. <see cref="LevelProperty"/> says which tone a place in the tree sits
/// on, and <see cref="NestsProperty"/> marks an element that starts a new one.
/// </summary>
/// <remarks>
/// The root tone is unique and never appears again inside a window. Everything below it
/// cycles three tones and starts over, so a grandchild never matches its grandparent and
/// nothing lightens as it descends. A fill change therefore means one thing, that a
/// different panel has been entered.
/// <para>
/// Each tone carries its own seam, so a fill and a border arrive together. Level 1 takes
/// <c>LineSeam</c>, level 2 <c>LineControl</c> and level 3 <c>LineControlDeep</c>.
/// <c>Themes/Surfaces.axaml</c> is where the pairs are written and the only place that
/// names them.
/// </para>
/// <para>
/// Controls are off the ramp entirely. A button stays on <c>SurfaceNest2</c> and an input
/// well on <c>SurfaceWell</c> at every level, both with a <c>LineSeam</c> border, so a
/// field reads as a field wherever it lands and only the container moves. Nothing a
/// control theme writes reads this.
/// </para>
/// <para>
/// Nothing counts. An element that nests takes one step down from whatever is above it,
/// which is read from the parent rather than searched for, so a panel moved to another
/// depth is right without being told. That matters because docking reparents panels.
/// </para>
/// </remarks>
public class Surface
{
    /// <summary>
    /// The tone at this point in the tree. 0 is the root, then 1, 2 and 3 cycle. It is
    /// inherited, so a control reads the surface it is standing on without being given it.
    /// </summary>
    /// <remarks>
    /// Set this by hand only on a container that is not itself a surface, such as a page
    /// that has to declare the ground it was dropped onto. An element carrying
    /// <see cref="NestsProperty"/> writes its own and would overwrite the value.
    /// </remarks>
    public static readonly AttachedProperty<int> LevelProperty =
        AvaloniaProperty.RegisterAttached<Surface, Control, int>("Level", inherits: true);

    /// <summary>
    /// This element is a surface of its own, so it takes one step down from its parent
    /// and gives that step to everything inside it.
    /// </summary>
    /// <remarks>
    /// A control theme sets this on itself, which is how a panel and an expander share
    /// one behaviour without sharing a base type. A view can set it on a plain
    /// <see cref="Border"/> to make a region without writing a tone anywhere.
    /// </remarks>
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
    /// <remarks>
    /// The parent is watched rather than read once, because the parent's own level moves
    /// when something above it moves. Reading it here rather than reading this element's
    /// inherited value is the whole trick: writing a level to give to the children would
    /// otherwise hide the level that arrived from above, and the step could never be
    /// worked out again.
    /// </remarks>
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
