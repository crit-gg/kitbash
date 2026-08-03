using System.Collections;
using System.Globalization;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace Kitbash.Core.Settings;

/// <summary>
/// One TOML file as its own text, edited in place. The same three operations
/// <see cref="SettingsDocument"/> has, over Tomlyn's syntax tree instead of a model, so
/// every comment, blank line and key order survives a write.
/// </summary>
internal sealed class TomlDocument
{
    private readonly DocumentSyntax _document;

    /// <summary>The file's own line ending, so an edit does not mix the two.</summary>
    private readonly string _newLine;

    public TomlDocument(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _document = SyntaxParser.Parse(text);
        _newLine = NewLineOf(text);
    }

    /// <summary>Whether the text this was built from could not be parsed.</summary>
    public bool HasErrors => _document.HasErrors;

    public override string ToString() => _document.ToString();

    /// <summary>Writes a dotted key, and reports whether the file changed.</summary>
    public bool SetValue(string key, object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var path = Split(key);

        if (path.Length == 0)
        {
            throw new ArgumentException("Key must contain at least one segment.", nameof(key));
        }

        var written = ValueFor(value);

        if (Find(path) is { } pair)
        {
            return Replace(pair, written);
        }

        DropScalarPrefix(path);
        Append(path, written);
        return true;
    }

    /// <summary>Drops a dotted key. Reports whether anything was there.</summary>
    public bool RemoveValue(string key)
    {
        var path = Split(key);

        if (path.Length == 0 || Find(path) is not { } pair)
        {
            return false;
        }

        Detach(pair);
        return true;
    }

    // Finding

    private KeyValueSyntax? Find(string[] path)
    {
        if (Find(Pairs(_document.KeyValues), path) is { } atRoot)
        {
            return atRoot;
        }

        foreach (var table in _document.Tables)
        {
            // A table array is never a setting, so it is not searched.
            if (table is not TableSyntax { Name: { } name })
            {
                continue;
            }

            var prefix = PathOf(name);

            if (prefix.Length < path.Length && StartsWith(path, prefix)
                && Find(Pairs(table.Items), path[prefix.Length..]) is { } inTable)
            {
                return inTable;
            }
        }

        return null;
    }

    private static KeyValueSyntax? Find(IEnumerable<KeyValueSyntax> pairs, string[] path)
    {
        if (path.Length == 0)
        {
            return null;
        }

        foreach (var pair in pairs)
        {
            if (pair.Key is not { } key)
            {
                continue;
            }

            var here = PathOf(key);

            if (Same(here, path))
            {
                return pair;
            }

            if (here.Length < path.Length && StartsWith(path, here)
                && pair.Value is InlineTableSyntax inline
                && Find(Pairs(inline), path[here.Length..]) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static IEnumerable<KeyValueSyntax> Pairs(SyntaxList<KeyValueSyntax>? items)
    {
        if (items is null)
        {
            yield break;
        }

        foreach (var item in items)
        {
            if (item is not null)
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<KeyValueSyntax> Pairs(InlineTableSyntax table)
    {
        foreach (var item in table.Items!)
        {
            if (item?.KeyValue is { } pair)
            {
                yield return pair;
            }
        }
    }

    // Writing a value

    /// <summary>
    /// Carries the old value's trivia onto the new one, so an inline comment after the
    /// value stays. Reports false when the value already said this, which leaves the
    /// spelling a person chose alone.
    /// </summary>
    private static bool Replace(KeyValueSyntax pair, ValueSyntax value)
    {
        if (pair.Value is not { } old)
        {
            pair.Value = value;
            return true;
        }

        if (Same(old, value))
        {
            return false;
        }

        CarryLeading(FirstToken(old), FirstToken(value));
        CarryTrailing(LastToken(old), LastToken(value));

        pair.Value = value;
        return true;
    }

    private static bool Same(ValueSyntax old, ValueSyntax value) => (old, value) switch
    {
        (StringValueSyntax a, StringValueSyntax b) => a.Value == b.Value,
        (IntegerValueSyntax a, IntegerValueSyntax b) => a.Value == b.Value,
        (BooleanValueSyntax a, BooleanValueSyntax b) => a.Value == b.Value,
        (FloatValueSyntax a, FloatValueSyntax b) => a.Value.Equals(b.Value),

        // An array or an inline table counts as changed rather than being compared.
        _ => false,
    };

    private static void CarryLeading(SyntaxToken? from, SyntaxToken? to)
    {
        if (from?.LeadingTrivia is not { Count: > 0 } trivia || to is null)
        {
            return;
        }

        foreach (var item in trivia)
        {
            to.AddLeadingTrivia(item);
        }

        trivia.Clear();
    }

    private static void CarryTrailing(SyntaxToken? from, SyntaxToken? to)
    {
        if (from?.TrailingTrivia is not { Count: > 0 } trivia || to is null)
        {
            return;
        }

        foreach (var item in trivia)
        {
            to.AddTrailingTrivia(item);
        }

        trivia.Clear();
    }

    // Appending

    private void Append(string[] path, ValueSyntax value)
    {
        if (LongestTable(path) is { } longest)
        {
            AddTo(longest.Table, path[longest.Depth..], value);
            return;
        }

        if (path.Length == 1)
        {
            var pair = PairFor(path, value);
            EndLast();
            TakeHeader(pair);
            _document.KeyValues.Add(pair);
            return;
        }

        var created = new TableSyntax(KeyFor(path[..^1]))
        {
            EndOfLineToken = NewLineToken(),
        };

        Separate(created);
        created.Items!.Add(PairFor(path[^1..], value));
        _document.Tables.Add(created);
    }

    /// <summary>
    /// The new pair goes after everything already in the table, but before the blank line
    /// and comment that introduce whatever comes next, which trail the last pair's end of
    /// line token rather than belonging to it.
    /// </summary>
    private void AddTo(TableSyntax table, string[] path, ValueSyntax value)
    {
        var pair = PairFor(path, value);
        var tail = Last(table.Items)?.EndOfLineToken;
        var carried = tail?.TrailingTrivia?.ToList();

        tail?.TrailingTrivia?.Clear();
        table.Items!.Add(pair);

        if (carried is { Count: > 0 })
        {
            foreach (var trivia in carried)
            {
                pair.EndOfLineToken!.AddTrailingTrivia(trivia);
            }
        }
    }

    private (TableSyntax Table, int Depth)? LongestTable(string[] path)
    {
        (TableSyntax Table, int Depth)? best = null;

        foreach (var candidate in _document.Tables)
        {
            if (candidate is not TableSyntax { Name: { } name } table)
            {
                continue;
            }

            var prefix = PathOf(name);

            if (prefix.Length < path.Length && StartsWith(path, prefix)
                && (best is null || prefix.Length > best.Value.Depth))
            {
                best = (table, prefix.Length);
            }
        }

        return best;
    }

    /// <summary>
    /// A value where a table belongs is replaced, which is what
    /// <see cref="SettingsDocument.SetValue"/> does. Left in place it would make a table
    /// header that redefines a key, which is invalid TOML.
    /// </summary>
    private void DropScalarPrefix(string[] path)
    {
        for (var length = path.Length - 1; length >= 1; length--)
        {
            if (Find(path[..length]) is { Value: not InlineTableSyntax } pair)
            {
                Detach(pair);
            }
        }
    }

    /// <summary>A blank line above a new table, and a line ending if the file lacks one.</summary>
    private void Separate(TableSyntax table)
    {
        var text = _document.ToString();

        if (text.Length == 0)
        {
            return;
        }

        if (!text.EndsWith('\n'))
        {
            table.AddLeadingTrivia(NewLineTrivia());
        }

        table.AddLeadingTrivia(NewLineTrivia());
    }

    /// <summary>
    /// A comment at the top of a file leads whatever comes first, which is the first table
    /// when the file has no root keys. Every root key is written above every table, so
    /// without this the first one written would slide in above the file's own header.
    /// </summary>
    private void TakeHeader(KeyValueSyntax pair)
    {
        if (Last(_document.KeyValues) is not null
            || First(_document.Tables) is not { LeadingTrivia: { Count: > 0 } trivia } )
        {
            return;
        }

        foreach (var item in trivia)
        {
            pair.AddLeadingTrivia(item);
        }

        trivia.Clear();
        pair.EndOfLineToken!.AddTrailingTrivia(NewLineTrivia());
    }

    /// <summary>Ends the file's last line, so an appended root key starts on its own.</summary>
    private void EndLast()
    {
        var text = _document.ToString();

        if (text.Length > 0 && !text.EndsWith('\n') && Last(_document.KeyValues) is { } last)
        {
            last.EndOfLineToken = NewLineToken();
        }
    }

    private KeyValueSyntax PairFor(string[] path, ValueSyntax value) =>
        new(KeyFor(path), value) { EndOfLineToken = NewLineToken() };

    private static KeySyntax KeyFor(string[] path)
    {
        var key = new KeySyntax(path[0]);

        for (var index = 1; index < path.Length; index++)
        {
            key.DotKeys!.Add(new DottedKeyItemSyntax(path[index]));
        }

        return key;
    }

    // Removing

    /// <summary>Takes a pair out of whichever list holds it.</summary>
    private static void Detach(KeyValueSyntax pair)
    {
        switch (pair.Parent)
        {
            case SyntaxList<KeyValueSyntax> list:
                list.RemoveChild(pair);
                break;

            case InlineTableItemSyntax item
                when item.Parent is SyntaxList<InlineTableItemSyntax> items:
                items.RemoveChild(item);

                // The comma belonged to the item before the end of the table.
                if (Last(items) is { } last)
                {
                    last.Comma = null;
                }

                break;
        }
    }

    // Values

    private ValueSyntax ValueFor(object value) => value switch
    {
        string text => new StringValueSyntax(text),
        bool flag => new BooleanValueSyntax(flag),
        long number => new IntegerValueSyntax(number),
        double number => new FloatValueSyntax(number),
        float number => new FloatValueSyntax(number),
        Enum name => new StringValueSyntax(name.ToString()),

        sbyte or byte or short or ushort or int or uint =>
            new IntegerValueSyntax(Convert.ToInt64(value, CultureInfo.InvariantCulture)),

        IEnumerable items => ArrayFor(items),
        _ => new StringValueSyntax(value.ToString() ?? string.Empty),
    };

    /// <summary>
    /// A null has no TOML spelling, so it is dropped rather than written as something
    /// else.
    /// </summary>
    private ValueSyntax ArrayFor(IEnumerable items)
    {
        var values = new List<object>();

        foreach (var item in items)
        {
            if (item is not null)
            {
                values.Add(item);
            }
        }

        if (values.Count > 0 && values.TrueForAll(item => item is string))
        {
            return new ArraySyntax(values.ConvertAll(item => (string)item).ToArray());
        }

        // A hand built array arrives with no brackets, unlike the string array above.
        var array = new ArraySyntax
        {
            OpenBracket = new SyntaxToken(TokenKind.OpenBracket, "["),
            CloseBracket = new SyntaxToken(TokenKind.CloseBracket, "]"),
        };

        for (var index = 0; index < values.Count; index++)
        {
            var item = new ArrayItemSyntax { Value = ValueFor(values[index]) };

            if (index < values.Count - 1)
            {
                item.Comma = new SyntaxToken(TokenKind.Comma, ",");
                item.Comma.AddTrailingWhitespace();
            }

            array.Items!.Add(item);
        }

        return array;
    }

    // Paths and tokens

    private static string[] Split(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return key.Split('.', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string[] PathOf(KeySyntax key)
    {
        var path = new List<string> { Name(key.Key) };

        foreach (var dotted in key.DotKeys!)
        {
            path.Add(Name(dotted.Key));
        }

        return [.. path];
    }

    /// <summary>The written key, without the whitespace that trails it or its quotes.</summary>
    private static string Name(SyntaxNode? key)
    {
        var text = key?.ToString().Trim() ?? string.Empty;

        return text.Length >= 2 && (text[0] == '"' || text[0] == '\'') && text[^1] == text[0]
            ? text[1..^1]
            : text;
    }

    private static bool Same(string[] left, string[] right) =>
        left.Length == right.Length && StartsWith(left, right);

    private static bool StartsWith(string[] path, string[] prefix)
    {
        if (prefix.Length > path.Length)
        {
            return false;
        }

        for (var index = 0; index < prefix.Length; index++)
        {
            if (!string.Equals(path[index], prefix[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static SyntaxToken? FirstToken(SyntaxNode node) =>
        node.Tokens(true).OfType<SyntaxToken>().FirstOrDefault();

    private static SyntaxToken? LastToken(SyntaxNode node) =>
        node.Tokens(true).OfType<SyntaxToken>().LastOrDefault();

    private static T? First<T>(SyntaxList<T>? items) where T : SyntaxNode
    {
        if (items is not null)
        {
            foreach (var item in items)
            {
                return item;
            }
        }

        return null;
    }

    private static T? Last<T>(SyntaxList<T>? items) where T : SyntaxNode
    {
        T? last = null;

        if (items is not null)
        {
            foreach (var item in items)
            {
                last = item;
            }
        }

        return last;
    }

    private SyntaxToken NewLineToken() => new(TokenKind.NewLine, _newLine);

    private SyntaxTrivia NewLineTrivia() => new(TokenKind.NewLine, _newLine);

    /// <summary>
    /// The file's first line ending decides. A file with none takes this machine's, which
    /// is the case of a file that does not exist yet.
    /// </summary>
    private static string NewLineOf(string text)
    {
        var index = text.IndexOf('\n');

        return index < 0
            ? Environment.NewLine
            : index > 0 && text[index - 1] == '\r' ? "\r\n" : "\n";
    }
}
