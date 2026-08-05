using Kitbash.Core.Platform.Openers;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Core.Tests.Openers;

/// <summary>
/// The whole seam the launcher uses, over this machine's real settings, so a failure
/// anywhere between the finder and the menu shows up here rather than as an empty menu.
/// </summary>
public sealed class WorkspaceOpenersTests
{
    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddKitbashWorkspaceOpeners();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ReadingGivesEverythingTheFinderGave()
    {
        using var provider = Provider();

        var found = await provider.GetRequiredService<IWorkspaceOpenerFinder>()
            .FindAsync(TestContext.Current.CancellationToken);
        var read = await provider.GetRequiredService<IWorkspaceOpeners>()
            .ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(found.Count, read.Count(opener => opener.Kind is not WorkspaceOpenerKind.Custom));
    }

    /// <summary>The hold has to hand back the same list rather than an empty second read.</summary>
    [Fact]
    public async Task ReadingTwiceGivesTheSameThing()
    {
        using var provider = Provider();
        var openers = provider.GetRequiredService<IWorkspaceOpeners>();

        var first = await openers.ReadAsync(TestContext.Current.CancellationToken);
        var second = await openers.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(first.Select(opener => opener.Id), second.Select(opener => opener.Id));
    }

    [Fact]
    public async Task ReadingConcurrentlyGivesTheSameThing()
    {
        using var provider = Provider();
        var openers = provider.GetRequiredService<IWorkspaceOpeners>();

        var reads = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => openers.ReadAsync(TestContext.Current.CancellationToken)));

        Assert.All(reads, read => Assert.Equal(reads[0].Count, read.Count));
    }
}
