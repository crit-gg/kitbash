using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The parts of a picker's popover that the controls do not provide. Attach it to the
/// picker: <c>ui:Picker.Wires="True"</c>, which both picker themes do.
/// </summary>
public class Picker
{
    private const string TodayPart = "PART_Today";
    private const string NowPart = "PART_Now";
    private const string ClearPart = "PART_Clear";
    private const string CalendarPart = "PART_Calendar";
    private const string FieldPart = "PART_TextBox";

    /// <summary>
    /// What an empty date field hints at, which is the shape the culture writes a date in.
    /// </summary>
    public static string DateHint =>
        System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;

    public static readonly AttachedProperty<bool> WiresProperty =
        AvaloniaProperty.RegisterAttached<Picker, TemplatedControl, bool>("Wires");

    /// <summary>The calendar inside the popover, caught when the template is applied.</summary>
    private static readonly AttachedProperty<Calendar?> MonthProperty =
        AvaloniaProperty.RegisterAttached<Picker, TemplatedControl, Calendar?>("Month");

    static Picker()
    {
        WiresProperty.Changed.AddClassHandler<TemplatedControl, bool>(OnWiresChanged);
    }

    public static bool GetWires(TemplatedControl control) => control.GetValue(WiresProperty);

    public static void SetWires(TemplatedControl control, bool value) =>
        control.SetValue(WiresProperty, value);

    private static void OnWiresChanged(TemplatedControl control, AvaloniaPropertyChangedEventArgs<bool> change)
    {
        control.RemoveHandler(Button.ClickEvent, OnClick);
        control.TemplateApplied -= OnTemplateApplied;
        control.PropertyChanged -= OnPickerChanged;

        if (!change.GetNewValue<bool>())
        {
            return;
        }

        control.AddHandler(Button.ClickEvent, OnClick);
        control.TemplateApplied += OnTemplateApplied;
        control.PropertyChanged += OnPickerChanged;
    }

    private static void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (sender is not TemplatedControl control)
        {
            return;
        }

        control.SetValue(MonthProperty, e.NameScope.Find(CalendarPart) as Calendar);

        // The control writes its own hint onto the field, so the template cannot hold one.
        // It is put back here, and again whenever the control writes over it.
        if (e.NameScope.Find(FieldPart) is TextBox field)
        {
            field.PropertyChanged -= OnFieldChanged;
            field.PropertyChanged += OnFieldChanged;
            field.SetCurrentValue(TextBox.PlaceholderTextProperty, DateHint);
        }
    }

    private static void OnFieldChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is TextBox field
            && e.Property == TextBox.PlaceholderTextProperty
            && e.GetNewValue<string?>() != DateHint)
        {
            field.SetCurrentValue(TextBox.PlaceholderTextProperty, DateHint);
        }
    }

    // Opening is the moment the popover has to agree with the field.
    private static void OnPickerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not CalendarDatePicker picker
            || e.Property != CalendarDatePicker.IsDropDownOpenProperty
            || !e.GetNewValue<bool>()
            || picker.GetValue(MonthProperty) is not { } month)
        {
            return;
        }

        month.SelectedDate = picker.SelectedDate;
        month.DisplayDate = picker.SelectedDate ?? DateTime.Today;
    }

    private static void OnClick(object? sender, RoutedEventArgs e)
    {
        switch (sender, e.Source)
        {
            case (CalendarDatePicker date, Button { Name: TodayPart }):
                date.SelectedDate = DateTime.Today;
                date.IsDropDownOpen = false;
                e.Handled = true;
                break;

            case (CalendarDatePicker date, Button { Name: ClearPart }):
                date.SelectedDate = null;
                date.IsDropDownOpen = false;
                e.Handled = true;
                break;

            case (TimePicker time, Button { Name: NowPart }):
                // To the minute, since the field shows hours and minutes.
                var now = DateTime.Now;
                time.SelectedTime = new TimeSpan(now.Hour, now.Minute, 0);
                e.Handled = true;
                break;
        }
    }
}
