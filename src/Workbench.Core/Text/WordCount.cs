namespace Workbench.Core.Text;

/// <summary>
/// A number and the noun it counts, so the noun agrees with the number.
/// </summary>
/// <remarks>
/// Every count a person reads goes through this. Never "1 conflict(s)", and never a noun
/// left singular because the plural was awkward to build at the call site.
/// <para>
/// Only nouns take a plural. A count of something described rather than named, such as
/// twelve modified files, keeps its word: "12 modified", not "12 modifieds".
/// </para>
/// </remarks>
/// <param name="Value">How many.</param>
/// <param name="Singular">The noun for one of them.</param>
/// <param name="Plural">The noun for any other number, when adding an s is wrong.</param>
public readonly record struct WordCount(int Value, string Singular, string? Plural = null)
{
    /// <summary>The noun on its own, so a view can style the number and the word apart.</summary>
    public string Word => Value == 1 ? Singular : Plural ?? Singular + "s";

    public override string ToString() => $"{Value} {Word}";
}
