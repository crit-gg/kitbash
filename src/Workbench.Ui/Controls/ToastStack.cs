using System.ComponentModel;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Workbench.Ui.Toasts;

namespace Workbench.Ui.Controls;

/// <summary>
/// One region's cards, and the count of anything held back behind them.
/// </summary>
public class ToastStack : TemplatedControl
{
    /// <summary>Which service to draw. A host hands its own down to all eight of these.</summary>
    public static readonly StyledProperty<IToastService?> ServiceProperty =
        AvaloniaProperty.Register<ToastStack, IToastService?>(nameof(Service));

    public static readonly StyledProperty<ToastAnchor> AnchorProperty =
        AvaloniaProperty.Register<ToastStack, ToastAnchor>(nameof(Anchor));

    /// <summary>The region this stack draws, resolved from the service and the anchor.</summary>
    public static readonly DirectProperty<ToastStack, IToastRegion?> RegionProperty =
        AvaloniaProperty.RegisterDirect<ToastStack, IToastRegion?>(nameof(Region), o => o.Region);

    /// <summary>Something is held back, so the count is worth drawing.</summary>
    public static readonly DirectProperty<ToastStack, bool> HasWaitingProperty =
        AvaloniaProperty.RegisterDirect<ToastStack, bool>(nameof(HasWaiting), o => o.HasWaiting);

    private IToastRegion? _region;
    private bool _hasWaiting;

    public ToastStack()
    {
        // The pointer lands on a card and the event bubbles, so this hears a hover over
        // any of them.
        PointerEntered += (_, _) => Pause(true);
        PointerExited += (_, _) => Pause(false);

        // Bottom right is the default anchor, so a stack given that one is never told a
        // new value and would otherwise wear no classes and stretch to fill its cell.
        ApplyAnchor(Anchor);
    }

    /// <inheritdoc cref="ServiceProperty"/>
    public IToastService? Service
    {
        get => GetValue(ServiceProperty);
        set => SetValue(ServiceProperty, value);
    }

    /// <inheritdoc cref="AnchorProperty"/>
    public ToastAnchor Anchor
    {
        get => GetValue(AnchorProperty);
        set => SetValue(AnchorProperty, value);
    }

    /// <inheritdoc cref="RegionProperty"/>
    public IToastRegion? Region
    {
        get => _region;
        private set => SetAndRaise(RegionProperty, ref _region, value);
    }

    /// <inheritdoc cref="HasWaitingProperty"/>
    public bool HasWaiting
    {
        get => _hasWaiting;
        private set => SetAndRaise(HasWaitingProperty, ref _hasWaiting, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ServiceProperty || change.Property == AnchorProperty)
        {
            Resolve();
        }

        if (change.Property == AnchorProperty)
        {
            ApplyAnchor(change.GetNewValue<ToastAnchor>());
        }
    }

    /// <summary>
    /// Unpauses on the way out. A stack removed while a card was under the pointer would
    /// otherwise leave its region paused with nothing left to unpause it.
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        Pause(false);
    }

    private void Resolve()
    {
        if (Region is not null)
        {
            Region.PropertyChanged -= OnRegionChanged;
            Region.IsPaused = false;
        }

        Region = Service?.Region(Anchor);

        if (Region is not null)
        {
            Region.PropertyChanged += OnRegionChanged;
        }

        HasWaiting = Region?.Waiting > 0;
    }

    private void OnRegionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IToastRegion.Waiting))
        {
            HasWaiting = Region?.Waiting > 0;
        }
    }

    private void Pause(bool paused)
    {
        if (Region is not null)
        {
            Region.IsPaused = paused;
        }
    }

    /// <summary>
    /// The same column and row classes <see cref="ToastCard"/> reads, so the alignment
    /// of the stack and the direction its cards arrive from are one decision.
    /// </summary>
    private void ApplyAnchor(ToastAnchor anchor)
    {
        Classes.Set("atLeft", anchor
            is ToastAnchor.TopLeft or ToastAnchor.LeftCenter or ToastAnchor.BottomLeft);
        Classes.Set("atRight", anchor
            is ToastAnchor.TopRight or ToastAnchor.RightCenter or ToastAnchor.BottomRight);
        Classes.Set("atCenter", anchor
            is ToastAnchor.TopCenter or ToastAnchor.BottomCenter);

        Classes.Set("atTop", anchor
            is ToastAnchor.TopLeft or ToastAnchor.TopCenter or ToastAnchor.TopRight);
        Classes.Set("atMiddle", anchor
            is ToastAnchor.LeftCenter or ToastAnchor.RightCenter);
        Classes.Set("atBottom", anchor
            is ToastAnchor.BottomLeft or ToastAnchor.BottomCenter or ToastAnchor.BottomRight);
    }
}
