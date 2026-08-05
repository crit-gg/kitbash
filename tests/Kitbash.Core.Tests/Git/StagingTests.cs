using Kitbash.Core.Git;

namespace Kitbash.Core.Tests.Git;

/// <summary>
/// Staging, and the part of it that has to be built rather than asked for: one hunk of a
/// file and not the rest. Every assertion here is made by asking git what it now holds.
/// </summary>
public class StagingTests
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    /// <summary>What the index holds against the head, read straight from git.</summary>
    private static string Staged(TestRepository repository) =>
        repository.Git("diff", "--no-color", "--cached", "--patch");

    /// <summary>What the working tree holds against the index.</summary>
    private static string Unstaged(TestRepository repository) =>
        repository.Git("diff", "--no-color", "--patch");

    [Fact]
    public async Task AWholePathIsStagedAndTakenBackOut()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Write("one.txt", "one\ntwo\n");

        Assert.True((await repository.Stager.StageAsync(repository.Root, ["one.txt"], Stop)).Succeeded);
        Assert.Contains("+two", Staged(repository), StringComparison.Ordinal);

        Assert.True((await repository.Stager.UnstageAsync(repository.Root, ["one.txt"], Stop)).Succeeded);
        Assert.Equal("", Staged(repository));
        Assert.Contains("+two", Unstaged(repository), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OneHunkGoesInAndTheOthersStayOut()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", TestRepository.Lines(40)));

        repository.Write("one.txt", TestRepository.Lines(
            40, (2, "TOP"), (20, "MIDDLE"), (38, "END")));

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Unstaged, "one.txt", null, Stop);

        Assert.NotNull(patch);
        Assert.Equal(3, patch.Hunks.Count);

        var staged = await repository.Stager.StageHunksAsync(repository.Root, patch, [1], Stop);

        Assert.True(staged.Succeeded, staged.Message);

        // The index took the middle change and nothing else.
        var inIndex = Staged(repository);

        Assert.Contains("+MIDDLE", inIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("+TOP", inIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("+END", inIndex, StringComparison.Ordinal);

        // The working tree still holds all three, so the other two are still to stage.
        var left = Unstaged(repository);

        Assert.Contains("+TOP", left, StringComparison.Ordinal);
        Assert.Contains("+END", left, StringComparison.Ordinal);
        Assert.DoesNotContain("+MIDDLE", left, StringComparison.Ordinal);

        // And the file on disk was not touched by any of it.
        Assert.Contains("MIDDLE", repository.Read("one.txt"), StringComparison.Ordinal);
        Assert.Contains("TOP", repository.Read("one.txt"), StringComparison.Ordinal);
    }

    // Staging the later hunks and not the first is the case that catches a wrong offset,
    // since every hunk after a skipped one starts somewhere else than the patch says.
    [Fact]
    public async Task SkippingTheFirstHunkStillPutsTheLaterOnesInTheRightPlace()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        // The first hunk grows the file, so everything after it moves.
        repository.Commit("start", ("one.txt", TestRepository.Lines(40)));

        var lines = TestRepository.Lines(40, (20, "MIDDLE"), (38, "END")).Split('\n').ToList();
        lines.Insert(1, "INSERTED ONE");
        lines.Insert(2, "INSERTED TWO");

        repository.Write("one.txt", string.Join('\n', lines));

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Unstaged, "one.txt", null, Stop);

        Assert.NotNull(patch);
        Assert.Equal(3, patch.Hunks.Count);

        var staged = await repository.Stager.StageHunksAsync(repository.Root, patch, [1, 2], Stop);

        Assert.True(staged.Succeeded, staged.Message);

        var inIndex = Staged(repository);

        Assert.Contains("+MIDDLE", inIndex, StringComparison.Ordinal);
        Assert.Contains("+END", inIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERTED", inIndex, StringComparison.Ordinal);

        // Git rewrote the index, so the file it now holds is the old one with two lines
        // changed. Reading it back is the proof the offsets landed where they should.
        var text = repository.Git("show", ":one.txt");

        Assert.Equal(TestRepository.Lines(40, (20, "MIDDLE"), (38, "END")), text);
    }

    [Fact]
    public async Task AHunkComesBackOutOfTheIndexOnItsOwn()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", TestRepository.Lines(40)));

        repository.Write("one.txt", TestRepository.Lines(
            40, (2, "TOP"), (20, "MIDDLE"), (38, "END")));

        repository.Git("add", "--all", "--", ".");

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Staged, "one.txt", null, Stop);

        Assert.NotNull(patch);
        Assert.Equal(3, patch.Hunks.Count);

        var unstaged = await repository.Stager.UnstageHunksAsync(repository.Root, patch, [0], Stop);

        Assert.True(unstaged.Succeeded, unstaged.Message);

        var inIndex = Staged(repository);

        Assert.DoesNotContain("+TOP", inIndex, StringComparison.Ordinal);
        Assert.Contains("+MIDDLE", inIndex, StringComparison.Ordinal);
        Assert.Contains("+END", inIndex, StringComparison.Ordinal);

        Assert.Equal(
            TestRepository.Lines(40, (20, "MIDDLE"), (38, "END")),
            repository.Git("show", ":one.txt"));
    }

    // Taking the last hunk out is the mirror of skipping the first one going in, and it is
    // the case that fails if the drift is applied to the wrong side of the header.
    [Fact]
    public async Task TakingAnEarlyHunkOutLeavesTheLaterOnesWhereTheyBelong()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", TestRepository.Lines(40)));

        var lines = TestRepository.Lines(40, (20, "MIDDLE"), (38, "END")).Split('\n').ToList();
        lines.Insert(1, "INSERTED ONE");
        lines.Insert(2, "INSERTED TWO");

        var whole = string.Join('\n', lines);

        repository.Write("one.txt", whole);
        repository.Git("add", "--all", "--", ".");

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Staged, "one.txt", null, Stop);

        Assert.NotNull(patch);

        var unstaged = await repository.Stager.UnstageHunksAsync(repository.Root, patch, [0], Stop);

        Assert.True(unstaged.Succeeded, unstaged.Message);

        Assert.Equal(
            TestRepository.Lines(40, (20, "MIDDLE"), (38, "END")),
            repository.Git("show", ":one.txt"));
    }

    [Fact]
    public async Task AnUntrackedFileIsReadWithoutStagingIt()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Write("new.txt", "alpha\nbeta\n");

        var patch = await repository.Diffs.ReadUntrackedPatchAsync(repository.Root, "new.txt", Stop);

        Assert.NotNull(patch);
        Assert.Equal("new.txt", patch.Path);
        Assert.Equal(GitChangeKind.Added, patch.Kind);
        Assert.Equal(2, patch.AddedLines);

        // Nothing was staged to look at it.
        Assert.Equal("", Staged(repository));
    }

    [Fact]
    public async Task AHunkOfANewFileIsStagedOnceGitIsToldTheFileIsComing()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Write("new.txt", TestRepository.Lines(40, (2, "TOP"), (38, "END")));

        Assert.True((await repository.Stager.BeginTrackingAsync(repository.Root, ["new.txt"], Stop)).Succeeded);

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Unstaged, "new.txt", null, Stop);

        Assert.NotNull(patch);

        var staged = await repository.Stager.StageHunksAsync(repository.Root, patch, [0], Stop);

        Assert.True(staged.Succeeded, staged.Message);
        Assert.Contains("+TOP", Staged(repository), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscardingPutsTheIndexVersionBack()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Write("one.txt", "one\nunwanted\n");

        Assert.True((await repository.Stager.DiscardAsync(repository.Root, ["one.txt"], Stop)).Succeeded);
        Assert.Equal("one\n", repository.Read("one.txt"));
    }

    [Fact]
    public async Task DeletingAnUntrackedFileRemovesIt()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Write("scratch.txt", "throw me away\n");

        Assert.True((await repository.Stager.DeleteUntrackedAsync(repository.Root, ["scratch.txt"], Stop)).Succeeded);
        Assert.False(File.Exists(Path.Combine(repository.Root, "scratch.txt")));
        Assert.True(File.Exists(Path.Combine(repository.Root, "one.txt")));
    }

    // A repository with no commits has no HEAD to resolve, which is what restore --staged
    // needs. Unstaging the very first file staged in a fresh repository goes through here.
    [Fact]
    public async Task AFileIsUnstagedInARepositoryWithNoCommitsYet()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Write("one.txt", "one\n");
        repository.Git("add", "--", "one.txt");

        var unstaged = await repository.Stager.UnstageAsync(repository.Root, ["one.txt"], Stop);

        Assert.True(unstaged.Succeeded, unstaged.Message);

        var file = Assert.Single(await repository.Files.ReadAsync(repository.Root, false, Stop));

        Assert.True(file.IsUntracked);
        Assert.True(File.Exists(Path.Combine(repository.Root, "one.txt")));
    }

    // The same for a file git has never had a version of, where there is nothing in the
    // head to put back.
    [Fact]
    public async Task ANewFileIsUnstagedWithoutBeingDeleted()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Write("two.txt", "two\n");
        repository.Git("add", "--", "two.txt");

        var unstaged = await repository.Stager.UnstageAsync(repository.Root, ["two.txt"], Stop);

        Assert.True(unstaged.Succeeded, unstaged.Message);
        Assert.Equal("two\n", repository.Read("two.txt"));
        Assert.Equal("", Staged(repository));
    }

    // Paths go to git through its input, separated by the null byte, so a name holding a
    // space or a newline is one path rather than two.
    [Fact]
    public async Task APathWithASpaceAndOneWithANewlineAreEachOnePath()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows has no newline in a file name");

        repository.Commit("start", ("plain.txt", "one\n"));

        repository.Write("a name with spaces.txt", "two\n");
        repository.Write("odd\nname.txt", "three\n");

        var staged = await repository.Stager.StageAsync(
            repository.Root, ["a name with spaces.txt", "odd\nname.txt"], Stop);

        Assert.True(staged.Succeeded, staged.Message);

        var files = await repository.Files.ReadAsync(repository.Root, false, Stop);

        Assert.Equal(2, files.Count);
        Assert.All(files, f => Assert.Equal(GitChangeKind.Added, f.Staged));

        var back = await repository.Stager.UnstageAsync(
            repository.Root, ["odd\nname.txt"], Stop);

        Assert.True(back.Succeeded, back.Message);
        Assert.True(
            Assert.Single(
                await repository.Files.ReadAsync(repository.Root, false, Stop),
                f => f.Path == "odd\nname.txt").IsUntracked);
    }

    // Every operating system caps a command line. A selection large enough to pass it is
    // what the pathspec file exists for.
    [Fact]
    public async Task ASelectionTooLargeForACommandLineStillStages()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        // Long names, so the whole set is well past what a command line takes.
        List<string> paths = [];

        for (var i = 0; i < 3000; i++)
        {
            var path = $"generated/{i:D5}/a-rather-long-file-name-so-the-set-is-large-{i:D5}.txt";

            repository.Write(path, "line\n");
            paths.Add(path);
        }

        Assert.True(
            paths.Sum(p => p.Length + 1) > 200_000,
            "the set is not large enough to prove anything");

        var staged = await repository.Stager.StageAsync(repository.Root, paths, Stop);

        Assert.True(staged.Succeeded, staged.Message);
        Assert.Equal(3000, (await repository.Files.ReadAsync(repository.Root, false, Stop)).Count);
    }

    [Fact]
    public async Task StagingNothingIsNotAnError()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        Assert.True((await repository.Stager.StageAsync(repository.Root, [], Stop)).Succeeded);
    }

    /// <summary>Where a line with this text sits, as the hunk and the place in it.</summary>
    private static GitPatchLine Find(GitPatch patch, string text)
    {
        for (var hunk = 0; hunk < patch.Hunks.Count; hunk++)
        {
            var lines = patch.Hunks[hunk].Lines;

            for (var line = 0; line < lines.Count; line++)
            {
                if (string.Equals(lines[line].Text, text, StringComparison.Ordinal))
                {
                    return new GitPatchLine(hunk, line);
                }
            }
        }

        throw new InvalidOperationException($"no line reading {text}");
    }

    /// <summary>Forty numbered lines with extra ones put in after the numbers named.</summary>
    private static string Inserted(params (int After, string Text)[] extras)
    {
        List<string> lines = [];

        for (var number = 1; number <= 40; number++)
        {
            lines.Add("line " + number.ToString(System.Globalization.CultureInfo.InvariantCulture));

            foreach (var (after, text) in extras)
            {
                if (after == number)
                {
                    lines.Add(text);
                }
            }
        }

        return string.Join('\n', lines) + "\n";
    }

    // One added line of a run and not the ones around it. The index has to end up holding
    // that line alone, with the rest still only in the working tree.
    [Fact]
    public async Task OneAddedLineOfAHunkGoesInWithoutTheRest()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "a\nb\nc\n"));
        repository.Write("one.txt", "a\nX\nY\nZ\nb\nc\n");

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Unstaged, "one.txt", null, Stop);

        Assert.NotNull(patch);

        var staged = await repository.Stager.StageLinesAsync(
            repository.Root, patch, [Find(patch, "Y")], Stop);

        Assert.True(staged.Succeeded, staged.Message);
        Assert.Equal("a\nY\nb\nc\n", repository.Git("show", ":one.txt"));

        // The file on disk was not touched, so X and Z are still there to stage.
        Assert.Equal("a\nX\nY\nZ\nb\nc\n", repository.Read("one.txt"));
    }

    // The mirror. An unpicked removal has to survive as context, since the line really is
    // still in the file git is applying this to.
    [Fact]
    public async Task OneRemovedLineOfAHunkGoesInWithoutTheRest()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "a\nX\nY\nZ\nb\n"));
        repository.Write("one.txt", "a\nb\n");

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Unstaged, "one.txt", null, Stop);

        Assert.NotNull(patch);

        var staged = await repository.Stager.StageLinesAsync(
            repository.Root, patch, [Find(patch, "Y")], Stop);

        Assert.True(staged.Succeeded, staged.Message);
        Assert.Equal("a\nX\nZ\nb\n", repository.Git("show", ":one.txt"));
    }

    [Fact]
    public async Task AddedAndRemovedLinesArePickedTogether()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "a\nX\nY\nb\n"));
        repository.Write("one.txt", "a\nP\nQ\nb\n");

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Unstaged, "one.txt", null, Stop);

        Assert.NotNull(patch);

        var staged = await repository.Stager.StageLinesAsync(
            repository.Root, patch, [Find(patch, "X"), Find(patch, "Q")], Stop);

        Assert.True(staged.Succeeded, staged.Message);

        // X went out and Q came in. Y and P were not picked and are untouched.
        Assert.Equal("a\nY\nQ\nb\n", repository.Git("show", ":one.txt"));
    }

    // Taking one line back out. Reverse is where an unpicked addition has to become context
    // and an unpicked removal has to be dropped, which is the opposite of going in.
    [Fact]
    public async Task OneLineComesBackOutOfTheIndexOnItsOwn()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "a\nb\nc\n"));
        repository.Write("one.txt", "a\nX\nY\nZ\nb\nc\n");
        repository.Git("add", "--all", "--", ".");

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Staged, "one.txt", null, Stop);

        Assert.NotNull(patch);

        var unstaged = await repository.Stager.UnstageLinesAsync(
            repository.Root, patch, [Find(patch, "Y")], Stop);

        Assert.True(unstaged.Succeeded, unstaged.Message);
        Assert.Equal("a\nX\nZ\nb\nc\n", repository.Git("show", ":one.txt"));

        // And the working tree still holds all three, so Y is unstaged rather than lost.
        Assert.Equal("a\nX\nY\nZ\nb\nc\n", repository.Read("one.txt"));
    }

    // The case that catches a wrong offset. A hunk only part of which was taken moves the
    // file by less than the whole diff would, so every later hunk starts somewhere else.
    [Fact]
    public async Task ALaterHunkLandsRightAfterAnEarlierOneWasOnlyPartlyTaken()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", TestRepository.Lines(40)));
        repository.Write("one.txt", Inserted((5, "FIVE A"), (5, "FIVE B"), (30, "THIRTY A"), (30, "THIRTY B")));

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Unstaged, "one.txt", null, Stop);

        Assert.NotNull(patch);
        Assert.Equal(2, patch.Hunks.Count);

        var staged = await repository.Stager.StageLinesAsync(
            repository.Root, patch, [Find(patch, "FIVE A"), Find(patch, "THIRTY B")], Stop);

        Assert.True(staged.Succeeded, staged.Message);

        // One line from each hunk, and the second landed where it belongs even though the
        // first hunk went in one line shorter than the diff said.
        Assert.Equal(
            Inserted((5, "FIVE A"), (30, "THIRTY B")),
            repository.Git("show", ":one.txt"));
    }

    [Fact]
    public async Task PickingNoChangedLineIsNotAnError()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "a\nb\nc\n"));
        repository.Write("one.txt", "a\nX\nb\nc\n");

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Unstaged, "one.txt", null, Stop);

        Assert.NotNull(patch);

        // A context line, which is never a change however it was picked.
        var staged = await repository.Stager.StageLinesAsync(
            repository.Root, patch, [Find(patch, "a")], Stop);

        Assert.True(staged.Succeeded, staged.Message);
        Assert.Equal("", Staged(repository));
    }
}
