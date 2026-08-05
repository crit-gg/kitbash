using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The buttons over a picked run of lines. Placed in the same panel as the diff it points at,
/// rather than inside it, so the editor's own template is left alone.
/// </summary>
[TemplatePart(StagePart, typeof(Button))]
[TemplatePart(UnstagePart, typeof(Button))]
[TemplatePart(DiscardPart, typeof(Button))]
public class TextDiffBar : TemplatedControl
{
    private const string StagePart = "PART_Stage";
    private const string UnstagePart = "PART_Unstage";
    private const string DiscardPart = "PART_Discard";

    /// <summary>The diff whose picked run this acts on.</summary>
    public static readonly StyledProperty<TextDiff?> DiffProperty =
        AvaloniaProperty.Register<TextDiffBar, TextDiff?>(nameof(Diff));

    /// <summary>How far down from the top of the run the buttons sit.</summary>
    public static readonly StyledProperty<double> DropProperty =
        AvaloniaProperty.Register<TextDiffBar, double>(nameof(Drop), 2);

    private TextDiff? _watching;
    private Button? _stage;
    private Button? _unstage;
    private Button? _discard;

    /// <inheritdoc cref="DiffProperty"/>
    public TextDiff? Diff
    {
        get => GetValue(DiffProperty);
        set => SetValue(DiffProperty, value);
    }

    /// <inheritdoc cref="DropProperty"/>
    public double Drop
    {
        get => GetValue(DropProperty);
        set => SetValue(DropProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextDiffBar);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _stage = Wire(e.NameScope.Find<Button>(StagePart), TextDiffActions.Stage);
        _unstage = Wire(e.NameScope.Find<Button>(UnstagePart), TextDiffActions.Unstage);
        _discard = Wire(e.NameScope.Find<Button>(DiscardPart), TextDiffActions.Discard);

        Show();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DiffProperty)
        {
            Follow(change.GetNewValue<TextDiff?>());
        }
    }

    private Button? Wire(Button? button, TextDiffActions action)
    {
        if (button is not null)
        {
            button.Click += (_, _) => Diff?.Ask(action);
        }

        return button;
    }

    private void Follow(TextDiff? diff)
    {
        if (_watching is not null)
        {
            _watching.PropertyChanged -= OnDiffChanged;
        }

        _watching = diff;

        if (_watching is not null)
        {
            _watching.PropertyChanged += OnDiffChanged;
        }

        Show();
    }

    private void OnDiffChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextDiff.ChunkProperty || e.Property == TextDiff.ActionsProperty)
        {
            Show();
        }
    }

    /// <summary>
    /// Puts the buttons against the top of the run, or takes them away when nothing is picked
    /// or nothing can be done to it.
    /// </summary>
    private void Show()
    {
        var actions = Diff?.Actions ?? TextDiffActions.None;
        var chunk = Diff?.Chunk;
        var showing = chunk is not null && actions != TextDiffActions.None;

        Wear(_stage, showing && actions.HasFlag(TextDiffActions.Stage), Key.S);
        Wear(_unstage, showing && actions.HasFlag(TextDiffActions.Unstage), Key.U);
        Wear(_discard, showing && actions.HasFlag(TextDiffActions.Discard), Key.D);

        if (chunk is not null)
        {
            Margin = new Thickness(0, chunk.Top + Drop, 0, 0);
        }

        IsVisible = showing;
    }

    /// <summary>
    /// Shows or hides one button and takes its shortcut with it. A key binding outlives the
    /// button being hidden, so a shortcut left behind would still run a gesture off screen.
    /// </summary>
    private static void Wear(Button? button, bool on, Key key)
    {
        if (button is null)
        {
            return;
        }

        var gesture = on ? new KeyGesture(key, Command()) : null;

        button.IsVisible = on;
        button.HotKey = gesture;

        ToolTip.SetTip(button, gesture?.ToString());
    }

    /// <summary>Meta on macOS and Control elsewhere, asked of the platform rather than the OS.</summary>
    private static KeyModifiers Command() =>
        Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers
            ?? KeyModifiers.Control;
}
