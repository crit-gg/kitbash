using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// How two cell values order when a column names no comparer. The two that used to be
/// wrong are a number against a number of another type, and a name carrying a number.
/// </summary>
public sealed class GridValueComparerTests
{
    private readonly GridValueComparer order = new();

    /// <summary>
    /// The one that ordered as text. An int against a long used to compare their
    /// spellings, so 10 came before 9.
    /// </summary>
    [Fact]
    public void NumbersOfDifferentTypesCompareAsNumbers()
    {
        Assert.True(order.Compare(9, 10L) < 0);
        Assert.True(order.Compare(10L, 9) > 0);
        Assert.True(order.Compare(2.5, 3) < 0);
        Assert.True(order.Compare(1m, 1.0) == 0);
    }

    /// <summary>The way a person reads a list of names that end in a number.</summary>
    [Fact]
    public void TextOrdersNaturally()
    {
        string[] names = ["item10", "item2", "item1", "item20"];

        Assert.Equal(
            ["item1", "item2", "item10", "item20"],
            names.OrderBy(name => (object?)name, order));
    }

    /// <summary>A run of digits is a number however long it is, and leading zeros do not change it.</summary>
    [Fact]
    public void LongRunsOfDigitsStayNumbers()
    {
        Assert.True(order.Compare("v9", "v10") < 0);
        Assert.True(order.Compare("a007", "a7") == 0 || order.Compare("a007", "a7") < 0);
        Assert.True(order.Compare("x99999999999999999999", "x100000000000000000000") < 0);
    }

    /// <summary>Nulls sort before values, and a value never compares equal to nothing.</summary>
    [Fact]
    public void NullsSortFirst()
    {
        Assert.True(order.Compare(null, "a") < 0);
        Assert.True(order.Compare("a", null) > 0);
        Assert.Equal(0, order.Compare(null, null));
    }

    /// <summary>Anything comparable to itself still uses its own ordering.</summary>
    [Fact]
    public void SameTypesUseTheirOwnOrder()
    {
        var early = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var late = new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);

        Assert.True(order.Compare(early, late) < 0);
        Assert.True(order.Compare(late, early) > 0);
    }
}
