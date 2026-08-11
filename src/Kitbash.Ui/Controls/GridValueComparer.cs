using System.Globalization;

namespace Kitbash.Ui.Controls;

/// <summary>
/// How two cell values order when a column names no comparer of its own. Numbers compare
/// as numbers whatever type they arrived as, and text compares the way a person reads it,
/// so item2 comes before item10.
/// </summary>
public sealed class GridValueComparer : IComparer<object?>
{
    /// <summary>Nulls sort before values, whichever way the column is pointing.</summary>
    public int Compare(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        // Before the same type check, since an int against a long is two numbers and
        // comparing them as text puts 10 before 9.
        if (Number(left) is { } first && Number(right) is { } second)
        {
            return first.CompareTo(second);
        }

        // Before the IComparable check, because a string is one and its own ordering is
        // character by character, which is what puts item10 in front of item2.
        if (left is string leftText && right is string rightText)
        {
            return Natural(leftText, rightText);
        }

        if (left.GetType() == right.GetType() && left is IComparable comparable)
        {
            return comparable.CompareTo(right);
        }

        return Natural(left.ToString(), right.ToString());
    }

    /// <summary>
    /// The value as a number, or null when it is not one. Every built in numeric type
    /// fits a decimal except the far ends of double and float, which fall back to it.
    /// </summary>
    private static decimal? Number(object value) => value switch
    {
        byte or sbyte or short or ushort or int or uint or long or ulong or decimal =>
            Convert.ToDecimal(value, CultureInfo.InvariantCulture),
        float or double => Fit(Convert.ToDouble(value, CultureInfo.InvariantCulture)),
        _ => null,
    };

    /// <summary>A double as a decimal, or null when it will not fit one.</summary>
    private static decimal? Fit(double value)
    {
        if (double.IsNaN(value) || value < (double)decimal.MinValue || value > (double)decimal.MaxValue)
        {
            return null;
        }

        return (decimal)value;
    }

    /// <summary>
    /// Compares run by run, digits as numbers and everything else as text, so a name
    /// carrying a number sorts the way it is read rather than character by character.
    /// </summary>
    private static int Natural(string? left, string? right)
    {
        if (left is null)
        {
            return right is null ? 0 : -1;
        }

        if (right is null)
        {
            return 1;
        }

        var a = 0;
        var b = 0;

        while (a < left.Length && b < right.Length)
        {
            var digits = char.IsDigit(left[a]) && char.IsDigit(right[b]);

            var endA = Run(left, a, digits);
            var endB = Run(right, b, digits);

            var order = digits
                ? Digits(left.AsSpan(a, endA - a), right.AsSpan(b, endB - b))
                : string.Compare(left[a..endA], right[b..endB], StringComparison.CurrentCultureIgnoreCase);

            if (order == 0 && !digits)
            {
                // Same letters in a different case, which orders after the compare above
                // has said they are otherwise the same run.
                order = string.CompareOrdinal(left[a..endA], right[b..endB]);
            }

            if (order != 0)
            {
                return order;
            }

            a = endA;
            b = endB;
        }

        return (left.Length - a).CompareTo(right.Length - b);
    }

    /// <summary>Where the run starting here ends, either all digits or all not.</summary>
    private static int Run(string value, int start, bool digits)
    {
        var end = start;

        while (end < value.Length && char.IsDigit(value[end]) == digits)
        {
            end++;
        }

        return end;
    }

    /// <summary>
    /// Two runs of digits, compared as numbers of any length. Leading zeros are dropped
    /// first, so 007 and 7 are the same number and the shorter one wins the tie.
    /// </summary>
    private static int Digits(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        var a = left.TrimStart('0');
        var b = right.TrimStart('0');

        if (a.Length != b.Length)
        {
            return a.Length.CompareTo(b.Length);
        }

        var order = a.CompareTo(b, StringComparison.Ordinal);

        return order != 0 ? order : right.Length.CompareTo(left.Length);
    }
}
