using System.Globalization;

namespace Kitbash.Tools;

/// <summary>
/// A tool's version, which is a semantic version. Build metadata is dropped on the way
/// in, since the specification says it takes no part in comparison.
/// </summary>
public sealed record ToolVersion(int Major, int Minor, int Patch, string Prerelease)
    : IComparable<ToolVersion>
{
    /// <summary>Reads a tag or a folder name. A leading v is allowed and dropped.</summary>
    public static bool TryParse(string? value, out ToolVersion version)
    {
        version = new ToolVersion(0, 0, 0, string.Empty);

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();

        if (text[0] is 'v' or 'V')
        {
            text = text[1..];
        }

        var build = text.IndexOf('+', StringComparison.Ordinal);

        if (build >= 0)
        {
            text = text[..build];
        }

        var prerelease = string.Empty;
        var dash = text.IndexOf('-', StringComparison.Ordinal);

        if (dash >= 0)
        {
            prerelease = text[(dash + 1)..];
            text = text[..dash];

            if (prerelease.Length == 0)
            {
                return false;
            }
        }

        var parts = text.Split('.');

        if (parts.Length != 3)
        {
            return false;
        }

        if (!Number(parts[0], out var major) || !Number(parts[1], out var minor) || !Number(parts[2], out var patch))
        {
            return false;
        }

        version = new ToolVersion(major, minor, patch, prerelease);

        return true;
    }

    public int CompareTo(ToolVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var numbers = Major.CompareTo(other.Major);

        if (numbers != 0)
        {
            return numbers;
        }

        numbers = Minor.CompareTo(other.Minor);

        if (numbers != 0)
        {
            return numbers;
        }

        numbers = Patch.CompareTo(other.Patch);

        return numbers != 0 ? numbers : ComparePrerelease(Prerelease, other.Prerelease);
    }

    public override string ToString() =>
        Prerelease.Length == 0
            ? $"{Major}.{Minor}.{Patch}"
            : $"{Major}.{Minor}.{Patch}-{Prerelease}";

    // A leading zero is not a number here, which is the specification's rule and is what
    // keeps 1.01.0 from reading as a version somebody meant.
    private static bool Number(string part, out int value)
    {
        value = 0;

        if (part.Length == 0 || (part.Length > 1 && part[0] == '0'))
        {
            return false;
        }

        foreach (var character in part)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    // A version carrying a prerelease is below the same version without one. Otherwise the
    // dot separated identifiers are compared one at a time, numbers below anything else.
    private static int ComparePrerelease(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return left.Length == right.Length ? 0 : left.Length == 0 ? 1 : -1;
        }

        var ours = left.Split('.');
        var theirs = right.Split('.');

        for (var index = 0; index < Math.Min(ours.Length, theirs.Length); index++)
        {
            var order = CompareIdentifier(ours[index], theirs[index]);

            if (order != 0)
            {
                return order;
            }
        }

        return ours.Length.CompareTo(theirs.Length);
    }

    private static int CompareIdentifier(string left, string right)
    {
        var ours = Number(left, out var ourNumber);
        var theirs = Number(right, out var theirNumber);

        if (ours && theirs)
        {
            return ourNumber.CompareTo(theirNumber);
        }

        return ours == theirs
            ? string.CompareOrdinal(left, right)
            : ours ? -1 : 1;
    }
}
