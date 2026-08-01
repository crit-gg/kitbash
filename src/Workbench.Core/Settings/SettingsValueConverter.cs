using System.Globalization;

namespace Workbench.Core.Settings;

internal sealed class SettingsValueConverter : ISettingsValueConverter
{
    public bool TryConvert<T>(object? raw, out T value)
    {
        if (TryConvert(raw, typeof(T), out var converted) && converted is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    private bool TryConvert(object? raw, Type target, out object? value)
    {
        value = null;

        if (raw is null)
        {
            return false;
        }

        var underlying = Nullable.GetUnderlyingType(target) ?? target;

        if (underlying.IsInstanceOfType(raw))
        {
            value = raw;
            return true;
        }

        // Enums are stored as strings.
        if (underlying.IsEnum)
        {
            if (raw is string text && Enum.TryParse(underlying, text, ignoreCase: true, out var parsed))
            {
                value = parsed;
                return true;
            }

            return false;
        }

        if (underlying.IsArray && raw is object?[] items)
        {
            var elementType = underlying.GetElementType()!;
            var result = Array.CreateInstance(elementType, items.Length);

            for (var index = 0; index < items.Length; index++)
            {
                if (!TryConvert(items[index], elementType, out var element))
                {
                    return false;
                }

                result.SetValue(element, index);
            }

            value = result;
            return true;
        }

        // Handles narrowing such as long to int.
        if (raw is IConvertible && typeof(IConvertible).IsAssignableFrom(underlying))
        {
            try
            {
                value = Convert.ChangeType(raw, underlying, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception exception)
                when (exception is InvalidCastException or FormatException or OverflowException)
            {
                return false;
            }
        }

        return false;
    }
}
