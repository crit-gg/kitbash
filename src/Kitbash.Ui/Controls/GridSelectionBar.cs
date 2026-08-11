using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The bar a grid floats over its last rows once rows are picked. It carries the count, the
/// actions for the whole set, and the way to drop the selection.
/// </summary>
[TemplatePart(DropPart, typeof(Button))]
public class GridSelectionBar : ContentControl
{
    private const string DropPart = "PART_Drop";

    /// <summary>How many rows are held, as words.</summary>
    public static readonly StyledProperty<string> CountProperty =
        AvaloniaProperty.Register<GridSelectionBar, string>(nameof(Count), string.Empty);

    private Button? drop;

    /// <summary>Raised when a person asks for the selection to be dropped.</summary>
    public event EventHandler? Dropped;

    /// <inheritdoc cref="CountProperty"/>
    public string Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (drop is not null)
        {
            drop.Click -= OnDrop;
        }

        drop = e.NameScope.Find<Button>(DropPart);

        if (drop is not null)
        {
            drop.Click += OnDrop;
        }
    }

    private void OnDrop(object? sender, RoutedEventArgs e) => Dropped?.Invoke(this, EventArgs.Empty);
}
