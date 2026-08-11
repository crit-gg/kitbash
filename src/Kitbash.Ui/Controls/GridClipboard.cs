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

    /// <summary>
    /// Reads text back into rows of fields. A quoted field keeps its tabs and its newlines
    /// and its doubled quotes become one, which is the same shape <see cref="Write"/> puts
    /// out and what a spreadsheet puts on the clipboard.
    /// </summary>
    public List<List<string>> Read(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var at = 0;

        while (at < text.Length)
        {
            var letter = text[at];

            if (quoted)
            {
                if (letter != '"')
                {
                    field.Append(letter);
                    at++;
                }
                else if (at + 1 < text.Length && text[at + 1] == '"')
                {
                    field.Append('"');
                    at += 2;
                }
                else
                {
                    quoted = false;
                    at++;
                }

                continue;
            }

            switch (letter)
            {
                case '"' when field.Length == 0:
                    quoted = true;
                    break;

                case '\t':
                    row.Add(field.ToString());
                    field.Clear();
                    break;

                case '\r' or '\n':
                    // A carriage return and a newline together end one row, not two.
                    if (letter == '\r' && at + 1 < text.Length && text[at + 1] == '\n')
                    {
                        at++;
                    }

                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = [];
                    break;

                default:
                    field.Append(letter);
                    break;
            }

            at++;
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
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
