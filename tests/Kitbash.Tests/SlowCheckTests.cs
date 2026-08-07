namespace Kitbash.Tests;

/// <summary>
/// When the launcher says out loud that it is checking for an update. The rule is here
/// rather than in the window, so it can be driven without a feed or a clock.
/// </summary>
public class SlowCheckTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task AnAnswerAlreadyInHandIsNeverAnnounced()
    {
        var said = 0;

        var found = await App.SayIfSlow(Task.FromResult("0.6.1"), Patience, () => said++);

        Assert.Equal("0.6.1", found);
        Assert.Equal(0, said);
    }

    [Fact]
    public async Task WorkStillGoingAfterThePatienceIsAnnouncedOnce()
    {
        var answer = new TaskCompletionSource<string>();
        var said = 0;

        var waiting = App.SayIfSlow(answer.Task, Patience, () => said++);

        // The announcement is what unblocks the answer, so it cannot have been reached by
        // the work finishing first.
        while (Volatile.Read(ref said) == 0)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        answer.SetResult("0.6.1");

        Assert.Equal("0.6.1", await waiting);
        Assert.Equal(1, said);
    }

    [Fact]
    public async Task NothingIsSaidASecondTimeWhileTheWorkFinishes()
    {
        var answer = new TaskCompletionSource<string?>();
        var said = 0;

        var waiting = App.SayIfSlow(answer.Task, Patience, () => said++);

        await Task.Delay(Patience * 4, TestContext.Current.CancellationToken);

        answer.SetResult(null);

        Assert.Null(await waiting);
        Assert.Equal(1, said);
    }
}
