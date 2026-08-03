using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Workbench.Ui.Toasts;

namespace Workbench.Ui.Controls;

/// <summary>
/// One toast, drawn. A card is told which <see cref="Toasts.Toast"/> it is showing and
/// nothing else, and everything about how it looks comes from that.
/// </summary>
public class ToastCard : TemplatedControl
{
    /// <summary>What this card is showing. A stack binds it and nothing else does.</summary>
    public static readonly StyledProperty<Toast?> ToastProperty =
        AvaloniaProperty.Register<ToastCard, Toast?>(nameof(Toast));

    /// <summary>
    /// Which region the card is in. It decides the edge the card enters from and the
    /// corner it scales toward as it falls back in the stack.
    /// </summary>
    public static readonly StyledProperty<ToastAnchor> AnchorProperty =
        AvaloniaProperty.Register<ToastCard, ToastAnchor>(nameof(Anchor));

    /// <summary>
    /// The mark's glyph turns while work is running. A direct property rather than a
    /// class, because the template hands it to the shared <c>spin</c> style rather than
    /// this theme animating anything of its own.
    /// </summary>
    public static readonly DirectProperty<ToastCard, bool> IsBusyProperty =
        AvaloniaProperty.RegisterDirect<ToastCard, bool>(nameof(IsBusy), o => o.IsBusy);

    /// <summary>How far the bar along the foot has run, from 0 to 1.</summary>
    public static readonly DirectProperty<ToastCard, double> MeterProperty =
        AvaloniaProperty.RegisterDirect<ToastCard, double>(nameof(Meter), o => o.Meter);

    /// <summary>The deepest card that still gets a look of its own. Anything past it draws like this one.</summary>
    private const int DeepestDepth = 2;

    private bool _isBusy;
    private double _meter;
    private bool _attached;

    public ToastCard()
    {
        Run = new TemplateCommand(parameter =>
        {
            if (parameter is ToastAction action)
            {
                Toast?.Run(action);
            }
        });

        Close = new TemplateCommand(_ => Toast?.Dismiss());

        // Bottom right is the default anchor, so a card there is never told a new one and
        // would otherwise carry no anchor class.
        ApplyAnchor(Anchor);
    }

    /// <inheritdoc cref="ToastProperty"/>
    public Toast? Toast
    {
        get => GetValue(ToastProperty);
        set => SetValue(ToastProperty, value);
    }

    /// <inheritdoc cref="AnchorProperty"/>
    public ToastAnchor Anchor
    {
        get => GetValue(AnchorProperty);
        set => SetValue(AnchorProperty, value);
    }

    /// <inheritdoc cref="IsBusyProperty"/>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetAndRaise(IsBusyProperty, ref _isBusy, value);
    }

    /// <inheritdoc cref="MeterProperty"/>
    public double Meter
    {
        get => _meter;
        private set => SetAndRaise(MeterProperty, ref _meter, value);
    }

    /// <summary>Runs one of the toast's actions. The parameter is the <see cref="ToastAction"/>.</summary>
    public ICommand Run { get; }

    /// <summary>Takes the toast away.</summary>
    public ICommand Close { get; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ToastProperty)
        {
            Follow(change.GetOldValue<Toast?>(), change.GetNewValue<Toast?>());
        }
        else if (change.Property == AnchorProperty)
        {
            ApplyAnchor(change.GetNewValue<ToastAnchor>());
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        Listen(true);
        ApplyAll(Toast);
    }

    /// <summary>
    /// Detaches the handler. A toast can outlive the card that drew it, so a card left
    /// subscribed is held alive by the toast for as long as the work runs.
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Listen(false);
        _attached = false;

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// The one place a card is told everything and untold it again.
    /// </summary>
    private void Follow(Toast? was, Toast? now)
    {
        if (was is not null)
        {
            was.PropertyChanged -= OnToastChanged;
        }

        Listen(true);
        ApplyAll(now);
    }

    private void Listen(bool on)
    {
        if (Toast is null)
        {
            return;
        }

        Toast.PropertyChanged -= OnToastChanged;

        if (on && _attached)
        {
            Toast.PropertyChanged += OnToastChanged;
        }
    }

    private void OnToastChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Toast toast)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(Toasts.Toast.Meter):
                Meter = toast.Meter;
                break;

            case nameof(Toasts.Toast.HasBody):
            case nameof(Toasts.Toast.IsGrouped):
            case nameof(Toasts.Toast.HasMeter):
            case nameof(Toasts.Toast.IsDismissing):
            case nameof(Toasts.Toast.Depth):
                ApplyState(toast);
                break;
        }
    }

    private void ApplyAll(Toast? toast)
    {
        foreach (var tier in Enum.GetValues<ToastTier>())
        {
            Classes.Set(ClassFor(tier), toast is not null && toast.Tier == tier);
        }

        foreach (var form in Enum.GetValues<ToastForm>())
        {
            Classes.Set(ClassFor(form), toast is not null && toast.Form == form);
        }

        IsBusy = toast?.Tier == ToastTier.Busy;
        Meter = toast?.Meter ?? 0;

        ApplyState(toast);
    }

    private void ApplyState(Toast? toast)
    {
        Classes.Set("hasBody", toast?.HasBody == true);
        Classes.Set("hasActions", toast?.Actions.Count > 0);
        Classes.Set("grouped", toast?.IsGrouped == true);
        Classes.Set("meter", toast?.HasMeter == true);
        Classes.Set("leaving", toast?.IsDismissing == true);

        var depth = Math.Clamp(toast?.Depth ?? 0, 0, DeepestDepth);

        for (var at = 1; at <= DeepestDepth; at++)
        {
            Classes.Set("depth" + at, depth == at);
        }

        // The cards overlap, so which is in front is the whole reading order. The newest
        // is 0 and everything behind it is negative, which holds whichever end of the deck
        // the newest sits at.
        ZIndex = -depth;
    }

    /// <summary>
    /// One anchor becomes two classes, a column and a row. A corner is the two of them
    /// together, so no selector has to name all eight regions and the six scale origins
    /// fall out of the pair. <see cref="ToastStack"/> reads the same anchor the same way.
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

    private static string ClassFor(ToastTier tier) => tier switch
    {
        ToastTier.Ok => "ok",
        ToastTier.Warn => "warn",
        ToastTier.Error => "error",
        ToastTier.Busy => "busy",
        _ => "info",
    };

    private static string ClassFor(ToastForm form) => form switch
    {
        ToastForm.Compact => "compact",
        ToastForm.Undo => "undo",
        _ => "card",
    };
}
