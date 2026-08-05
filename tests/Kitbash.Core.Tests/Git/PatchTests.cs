using Kitbash.Core.Git;

namespace Kitbash.Core.Tests.Git;

/// <summary>
/// The unified diff reader and writer. The gate is that git's own output, read and written
/// back, is the same bytes, since anything the reader drops is a hunk that cannot be staged.
/// </summary>
public class PatchTests
{
    private readonly GitPatchReader _reader = new();
    private readonly GitPatchWriter _writer = new();

    [Fact]
    public void GitsOwnOutputSurvivesBeingReadAndWrittenBack()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit(
            "start",
            ("one.txt", TestRepository.Lines(40)),
            ("two.txt", "alpha\nbeta\ngamma\n"));

        // Three changes far enough apart that git writes three hunks.
        repository.Write("one.txt", TestRepository.Lines(
            40, (2, "changed near the top"), (20, "changed in the middle"), (38, "changed near the end")));

        repository.Write("two.txt", "alpha\nbeta\ngamma\ndelta");

        var raw = repository.Git("-c", "diff.noprefix=false", "diff", "--no-color", "--patch");

        var patches = _reader.Read(raw);

        Assert.Equal(2, patches.Count);
        Assert.Equal(3, patches[0].Hunks.Count);

        var written = string.Concat(patches.Select(p => _writer.Write(p)));

        Assert.Equal(raw, written);
    }

    [Fact]
    public void AFileWithNoNewlineAtTheEndKeepsThatNote()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("ends.txt", "one\ntwo"));
        repository.Write("ends.txt", "one\ntwo\nthree");

        var raw = repository.Git("-c", "diff.noprefix=false", "diff", "--no-color", "--patch");
        var patch = Assert.Single(_reader.Read(raw));

        Assert.Contains(patch.Hunks[0].Lines, l => l.Kind == GitDiffLineKind.NoNewline);
        Assert.Equal(raw, _writer.Write(patch));
    }

    [Fact]
    public void ARenameIsReadWithBothOfItsPaths()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("before.txt", TestRepository.Lines(20)));
        repository.Git("mv", "before.txt", "after.txt");

        var raw = repository.Git(
            "-c", "diff.noprefix=false", "diff", "--no-color", "--cached", "--patch", "--find-renames");

        var patch = Assert.Single(_reader.Read(raw));

        Assert.Equal(GitChangeKind.Renamed, patch.Kind);
        Assert.Equal("after.txt", patch.Path);
        Assert.Equal("before.txt", patch.OldPath);
        Assert.Empty(patch.Hunks);
    }

    [Fact]
    public void ABinaryFileIsSaidToBeOneRatherThanReadAsLines()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("readme.txt", "hello\n"));

        File.WriteAllBytes(
            Path.Combine(repository.Root, "art.bin"), [0, 1, 2, 3, 0, 255, 7, 0]);

        repository.Git("add", "--all", "--", ".");

        var raw = repository.Git("-c", "diff.noprefix=false", "diff", "--no-color", "--cached", "--patch");
        var patch = Assert.Single(_reader.Read(raw), p => p.Path == "art.bin");

        Assert.True(patch.IsBinary);
        Assert.Empty(patch.Hunks);
    }

    [Fact]
    public void APathWithASpaceAndOneWithAQuoteAreBothReadWhole()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit(
            "start",
            ("a name with spaces.txt", "one\n"),
            ("quote\".txt", "one\n"));

        repository.Write("a name with spaces.txt", "one\ntwo\n");
        repository.Write("quote\".txt", "one\ntwo\n");

        var raw = repository.Git("-c", "diff.noprefix=false", "diff", "--no-color", "--patch");
        var patches = _reader.Read(raw);

        Assert.Contains(patches, p => p.Path == "a name with spaces.txt");
        Assert.Contains(patches, p => p.Path == "quote\".txt");
    }

    // The line numbers are what a view draws down its gutter, so they are read rather than
    // counted from the top of the hunk.
    [Fact]
    public void EveryLineCarriesTheNumberItHasOnItsOwnSide()
    {
        var patch = Assert.Single(_reader.Read(
            """
            diff --git a/x.txt b/x.txt
            index 1111111..2222222 100644
            --- a/x.txt
            +++ b/x.txt
            @@ -10,4 +10,5 @@ void Method()
             ten
            -eleven
            +ELEVEN
            +eleven and a half
             twelve
             thirteen

            """.ReplaceLineEndings("\n")));

        var hunk = Assert.Single(patch.Hunks);

        Assert.Equal(10, hunk.OldStart);
        Assert.Equal(4, hunk.OldCount);
        Assert.Equal(10, hunk.NewStart);
        Assert.Equal(5, hunk.NewCount);
        Assert.Equal("void Method()", hunk.Heading);

        Assert.Equal(
            [(10, 10), (11, null), (null, 11), (null, 12), (12, 13), (13, 14)],
            hunk.Lines.Select(l => (l.OldLine, l.NewLine)));
    }

    // Git writes an empty context line as a lone space, so a file with blank lines in it is
    // where a reader that trims lines goes wrong.
    [Fact]
    public void AnEmptyContextLineIsKept()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("gaps.txt", "one\n\n\ntwo\n\n\nthree\n"));
        repository.Write("gaps.txt", "one\n\n\nTWO\n\n\nthree\n");

        var raw = repository.Git("-c", "diff.noprefix=false", "diff", "--no-color", "--patch");
        var patch = Assert.Single(_reader.Read(raw));

        // Two blank lines either side of the change, all four inside the context.
        Assert.Equal(4, patch.Hunks[0].Lines.Count(l => l is { Kind: GitDiffLineKind.Context, Text: "" }));
        Assert.Equal(raw, _writer.Write(patch));
    }
}
