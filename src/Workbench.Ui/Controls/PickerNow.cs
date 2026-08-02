using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Workbench.Ui.Controls;

/// <summary>
/// Wires the Today and Now buttons a picker's popup carries. Attach it to the picker:
/// <c>ui:PickerNow.Wires="True"</c>, which both picker themes do.
/// </summary>
/// <remarks>
/// Neither <see cref="CalendarDatePicker"/> nor <see cref="TimePicker"/> exposes a command
/// for this, and a theme cannot carry behaviour, so the button is heard rather than looked
/// up. It sits in the picker's own template, and a click bubbles, which is the same shape
/// <see cref="SearchBox"/> uses for its clear button.
/// </remarks>
public class PickerNow
{
    private const string TodayPart = "PART_Today";
    private const string NowPart = "PART_Now";

    public static readonly AttachedProperty<bool> WiresProperty =
        AvaloniaProperty.RegisterAttached<PickerNow, Control, bool>("Wires");

    static PickerNow()
    {
        WiresProperty.Changed.AddClassHandler<Control, bool>(OnWiresChanged);
    }

    public static bool GetWires(Control control) => control.GetValue(WiresProperty);

    public static void SetWires(Control control, bool value) => control.SetValue(WiresProperty, value);

    private static void OnWiresChanged(Control control, AvaloniaPropertyChangedEventArgs<bool> change)
    {
        control.RemoveHandler(Button.ClickEvent, OnClick);

        if (change.GetNewValue<bool>())
        {
            control.AddHandler(Button.ClickEvent, OnClick);
        }
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

            case (TimePicker time, Button { Name: NowPart }):
                // To the minute, since the field shows hours and minutes.
                var now = DateTime.Now;
                time.SelectedTime = new TimeSpan(now.Hour, now.Minute, 0);
                e.Handled = true;
                break;
        }
    }
}
