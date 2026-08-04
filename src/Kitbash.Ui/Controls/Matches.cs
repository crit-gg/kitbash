using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Kitbash.Ui.Controls;

/// <summary>
/// True while a value equals the parameter, which is what puts a radio over one member of an
/// enum. Unchecking writes nothing back, since the radio that was checked instead is what
/// carries the new answer.
/// </summary>
public class Matches : IValueConverter
{
    public static readonly Matches Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Equals(value, parameter);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? parameter : BindingOperations.DoNothing;
}
