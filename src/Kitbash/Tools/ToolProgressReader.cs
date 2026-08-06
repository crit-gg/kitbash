using System.Globalization;
using System.Text.Json;

namespace Kitbash.Tools;

/// <summary>
/// Reads what a script writes to its output. Two forms are understood, a JSON object per
/// line and a prefixed plain line, and anything else is a log line.
/// </summary>
public sealed class ToolProgressReader
{
    /// <summary>What a plain line starts with to be read as a message.</summary>
    public const string Prefix = "@kitbash";

    /// <summary>The property a JSON line carries to be read as a message.</summary>
    public const string Marker = "kitbash";

    /// <summary>A bar runs from 0 to 100, so a script reporting a fraction is not one.</summary>
    private const double Most = 100;

    public ToolProgressStep Read(string? line)
    {
        var text = line?.Trim() ?? string.Empty;

        if (text.Length == 0)
        {
            return new ToolProgressStep();
        }

        return FromJson(text) ?? FromPrefix(text) ?? new ToolProgressStep { Log = line!.TrimEnd() };
    }

    /// <summary>One line of standard error, which is a log line and never a message.</summary>
    public ToolProgressStep ReadError(string line) =>
        new() { Log = line.TrimEnd(), IsError = true };

    /// <summary>
    /// A JSON object carrying the marker. An object without it is a script printing JSON
    /// for its own reasons, so it stays a log line.
    /// </summary>
    private static ToolProgressStep? FromJson(string text)
    {
        if (text[0] != '{' || text[^1] != '}')
        {
            return null;
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(Marker, out _))
            {
                return null;
            }

            var progress = Number(root, "progress");

            return new ToolProgressStep
            {
                Stage = Text(root, "stage"),
                Detail = Text(root, "detail"),
                Progress = progress,
                IsIndeterminate = progress is null && root.TryGetProperty("progress", out var written)
                    && written.ValueKind is JsonValueKind.Null,
                Log = Text(root, "log") ?? Text(root, "error"),
                IsError = Text(root, "error") is not null,
            };
        }
    }

    /// <summary>
    /// The prefix, a verb, and the rest of the line. An unknown verb keeps the whole line
    /// as a log line rather than being dropped.
    /// </summary>
    private static ToolProgressStep? FromPrefix(string text)
    {
        if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = text[Prefix.Length..];

        // The prefix has to end the word, so a line about @kitbashing is not a message.
        if (rest.Length > 0 && !char.IsWhiteSpace(rest[0]) && rest[0] != ':')
        {
            return null;
        }

        rest = rest.TrimStart(':').Trim();

        var space = rest.IndexOf(' ', StringComparison.Ordinal);
        var verb = (space < 0 ? rest : rest[..space]).ToLowerInvariant();
        var value = space < 0 ? string.Empty : rest[(space + 1)..].Trim();

        return verb switch
        {
            "stage" => new ToolProgressStep { Stage = value },
            "detail" => new ToolProgressStep { Detail = value },
            "log" => new ToolProgressStep { Log = value },
            "error" => new ToolProgressStep { Log = value, IsError = true },
            "progress" => Bar(value),
            _ => new ToolProgressStep { Log = text },
        };
    }

    /// <summary>A number moves the bar and nothing at all puts the spinner back.</summary>
    private static ToolProgressStep Bar(string value)
    {
        var number = Parse(value.TrimEnd('%'));

        return number is null
            ? new ToolProgressStep { IsIndeterminate = true }
            : new ToolProgressStep { Progress = number };
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.String
            ? found.GetString()!.Trim()
            : null;

    private static double? Number(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var found))
        {
            return null;
        }

        return found.ValueKind switch
        {
            JsonValueKind.Number => Math.Clamp(found.GetDouble(), 0, Most),
            JsonValueKind.String => Parse(found.GetString()!.Trim().TrimEnd('%')),
            _ => null,
        };
    }

    private static double? Parse(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? Math.Clamp(number, 0, Most)
            : null;
}
