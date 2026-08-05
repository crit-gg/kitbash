using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// Which words inside a changed line actually differ. Pure text, so none of this needs a
/// window.
/// </summary>
public class WordDiffTests
{
    private readonly TextDiffWords _words = new();

    /// <summary>The runs read back as the text they cover, which is what a reader checks.</summary>
    private static IReadOnlyList<string> Reads(string text, IReadOnlyList<TextDiffSpan> runs) =>
        [.. runs.Select(r => text.Substring(r.Start, r.Length))];

    [Fact]
    public void OneWordInTheMiddleIsTheOnlyThingMarked()
    {
        var (before, after) = _words.Compare(
            "position = Vector2(0, -6)",
            "position = Vector2(0, -8)");

        Assert.Equal(["6"], Reads("position = Vector2(0, -6)", before));
        Assert.Equal(["8"], Reads("position = Vector2(0, -8)", after));
    }

    [Fact]
    public void AWordAddedIsMarkedOnTheNewSideAlone()
    {
        const string Before = "var speed = 10";
        const string After = "var speed = 10.5";

        var (old, now) = _words.Compare(Before, After);

        Assert.Empty(old);
        Assert.Equal([".5"], Reads(After, now));
    }

    [Fact]
    public void AWordTakenOutIsMarkedOnTheOldSideAlone()
    {
        const string Before = "public static void Main()";
        const string After = "public void Main()";

        var (old, now) = _words.Compare(Before, After);

        Assert.Equal(["static "], Reads(Before, old));
        Assert.Empty(now);
    }

    // The ends of a line usually agree, and trimming them is what keeps the comparison
    // small enough to run on every line of a large diff.
    [Fact]
    public void OnlyThePartBetweenTheSharedEndsIsCompared()
    {
        const string Before = "    if (body.is_in_group(\"hazard\")):";
        const string After = "    if (body.is_in_group(\"hurt\")):";

        var (old, now) = _words.Compare(Before, After);

        Assert.Equal(["hazard"], Reads(Before, old));
        Assert.Equal(["hurt"], Reads(After, now));
    }

    [Fact]
    public void TwoLinesThatAgreeHaveNothingMarked()
    {
        var (before, after) = _words.Compare("the same line", "the same line");

        Assert.Empty(before);
        Assert.Empty(after);
    }

    // Marking nearly the whole line says nothing the line kind did not already say, and it
    // reads as noise across a run of them.
    [Fact]
    public void TwoLinesWithAlmostNothingInCommonAreLeftAlone()
    {
        var (before, after) = _words.Compare(
            "extends CharacterBody2D",
            "@onready var hurt := $HurtBox");

        Assert.Empty(before);
        Assert.Empty(after);
    }

    [Fact]
    public void AnEmptyLineOnEitherSideIsLeftAlone()
    {
        Assert.Empty(_words.Compare("", "something").New);
        Assert.Empty(_words.Compare("something", "").Old);
    }

    // Runs that touch are one run, or a changed word beside changed punctuation would draw
    // as two marks with a seam down the middle.
    [Fact]
    public void RunsThatTouchAreJoined()
    {
        const string Before = "call(a, b)";
        const string After = "call(xy, b)";

        var (_, now) = _words.Compare(Before, After);

        Assert.Equal(["xy"], Reads(After, now));
    }

    [Fact]
    public void AVeryLongPairIsLeftAloneRatherThanCompared()
    {
        var before = string.Join(' ', Enumerable.Range(0, 900).Select(i => "word" + i));
        var after = string.Join(' ', Enumerable.Range(0, 900).Select(i => "term" + i));

        var (old, now) = _words.Compare(before, after);

        Assert.Empty(old);
        Assert.Empty(now);
    }

    [Fact]
    public void OneSideIsAskedForOnItsOwn()
    {
        const string Now = "position = Vector2(0, -8)";

        Assert.Equal(["8"], Reads(Now, _words.Against(Now, "position = Vector2(0, -6)")));
    }

    // Every character lands in exactly one word, or a run could not be turned back into a
    // position in the line.
    [Theory]
    [InlineData("a.b", "a.c")]
    [InlineData("\tif x:", "\tif y:")]
    [InlineData("[node name=\"A\"]", "[node name=\"B\"]")]
    [InlineData("shader_parameter/flash = 0.0", "shader_parameter/flash = 1.0")]
    public void EveryRunSitsInsideTheLineItCameFrom(string before, string after)
    {
        var (old, now) = _words.Compare(before, after);

        Assert.All(old, r => Assert.True(r.Start >= 0 && r.End <= before.Length, $"{r.Start},{r.Length}"));
        Assert.All(now, r => Assert.True(r.Start >= 0 && r.End <= after.Length, $"{r.Start},{r.Length}"));
        Assert.All(old, r => Assert.True(r.Length > 0));
        Assert.All(now, r => Assert.True(r.Length > 0));
    }
}
