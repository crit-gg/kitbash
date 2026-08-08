using System.Text;
using Kitbash.Ui.Controls;

namespace Kitbash.Workspaces;

/// <summary>
/// The glyph names a workspace config may write, in both directions. Box Icons spells a
/// name with hyphens and the enum does not, so the two forms are converted here.
/// </summary>
public sealed class WorkspaceLinkIcons
{
    /// <summary>Every glyph, in the order the enum declares them.</summary>
    public IReadOnlyList<IconGlyph> All { get; } = [.. Enum.GetValues<IconGlyph>()];

    /// <summary>Reads a name as a file spells it, hyphens, underscores and case ignored.</summary>
    public bool TryRead(string name, out IconGlyph glyph)
    {
        ArgumentNullException.ThrowIfNull(name);

        var member = name.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return Enum.TryParse(member, ignoreCase: true, out glyph) && Enum.IsDefined(glyph);
    }

    /// <summary>The name a file gets, which is the icon set's own spelling.</summary>
    public string Write(IconGlyph glyph)
    {
        var name = glyph.ToString();
        var text = new StringBuilder(name.Length + 4);

        foreach (var letter in name)
        {
            if (char.IsUpper(letter) && text.Length > 0)
            {
                text.Append('-');
            }

            text.Append(char.ToLowerInvariant(letter));
        }

        return text.ToString();
    }
}
