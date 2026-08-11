using System.Globalization;
using System.Text;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Turns picked cells into the text a spreadsheet reads. Tab between columns, carriage
/// return and newline between rows, and a field carrying either of those, or a quote, is
/// quoted with its own quotes doubled. That is what Excel writes and what it expects back.
/// </summary>
internal sealed class GridClipboard
{
    public string Write(IEnumerable<object> items, IReadOnlyList<GridColumn> columns)
    {
        var text = new StringBuilder();
        var first = true;

        foreach (var item in items)
        {
            if (!first)
            {
                text.Append("\r\n");
            }

            first = false;

            for (var index = 0; index < columns.Count; index++)
            {
                if (index > 0)
                {
                    text.Append('\t');
                }

                text.Append(Field(columns[index].ValueOf(item)));
            }
        }

        return text.ToString();
    }

    private static string Field(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            string words => words,
            IFormattable number => number.ToString(null, CultureInfo.CurrentCulture),
            _ => value.ToString() ?? string.Empty,
        };

        if (!text.Contains('\t') && !text.Contains('\n') && !text.Contains('\r') && !text.Contains('"'))
        {
            return text;
        }

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}
