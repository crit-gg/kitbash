using Kitbash.Core.Platform;

namespace Kitbash.Core.Tests.Platform;

/// <summary>
/// The list the Windows user PATH is edited through. Pure, so it runs on every OS even
/// though only Windows writes one.
/// </summary>
public sealed class PathVariableTests
{
    private const string Bin = @"C:\Users\someone\AppData\Local\KitbashData\bin";

    private static bool IsBin(string entry) =>
        string.Equals(entry.TrimEnd('\\'), Bin, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void AFolderIsAddedOnTheEnd()
    {
        var path = PathVariable.Parse(@"%USERPROFILE%\.cargo\bin;C:\tools", ';');

        Assert.Equal(@"%USERPROFILE%\.cargo\bin;C:\tools;" + Bin, path.With(Bin, IsBin).ToString());
    }

    [Fact]
    public void AFolderAlreadyThereInAnyCaseIsNotAddedAgain()
    {
        var written = @"C:\tools;" + Bin.ToUpperInvariant() + @"\";
        var path = PathVariable.Parse(written, ';');

        Assert.Same(path, path.With(Bin, IsBin));
        Assert.Equal(written, path.ToString());
    }

    [Fact]
    public void AnEmptyValueTakesTheFolderAlone()
    {
        Assert.Equal(Bin, PathVariable.Parse(null, ';').With(Bin, IsBin).ToString());
        Assert.Equal(Bin, PathVariable.Parse(string.Empty, ';').With(Bin, IsBin).ToString());
    }

    [Fact]
    public void RemovingLeavesEveryOtherEntryAsWritten()
    {
        var path = PathVariable.Parse(@"%USERPROFILE%\bin;" + Bin + @";C:\tools", ';');

        Assert.Equal(@"%USERPROFILE%\bin;C:\tools", path.Without(IsBin).ToString());
    }

    [Fact]
    public void EmptyEntriesAreDropped()
    {
        var path = PathVariable.Parse(@";C:\tools;;  ;C:\more;", ';');

        Assert.Equal([@"C:\tools", @"C:\more"], path.Entries);
    }

    [Fact]
    public void AColonListReadsTheSameWay()
    {
        var path = PathVariable.Parse("/usr/local/bin:/usr/bin::/bin", ':');

        Assert.Equal(["/usr/local/bin", "/usr/bin", "/bin"], path.Entries);
    }
}
