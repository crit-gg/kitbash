using Kitbash.Core.IO;
using Kitbash.Core.Platform.Openers;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Core.Tests.Openers;

/// <summary>
/// Runs the real finder against this machine, so what it asserts is what is true anywhere
/// rather than what happens to be installed here.
/// </summary>
public sealed class OpenerFinderTests
{
    /// <summary>
    /// Whether this machine starts a found program through LaunchServices rather than
    /// directly. Only macOS does, since an application bundle is a directory.
    /// </summary>
    private static bool LaunchServices => OperatingSystem.IsMacOS();

    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddKitbashWorkspaceOpeners();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task EveryOpenerFoundNamesARunnableProgram()
    {
        using var provider = Provider();
        var fileSystem = provider.GetRequiredService<IFileSystem>();

        var found = await provider.GetRequiredService<IWorkspaceOpenerFinder>().FindAsync(TestContext.Current.CancellationToken);

        Assert.All(found, opener => Assert.True(
            fileSystem.IsExecutableFile(opener.Program),
            $"{opener.Name} points at {opener.Program}, which is not runnable"));
    }

    [Fact]
    public async Task EveryOpenerFoundIsFullyDescribed()
    {
        using var provider = Provider();

        var found = await provider.GetRequiredService<IWorkspaceOpenerFinder>().FindAsync(TestContext.Current.CancellationToken);

        Assert.All(found, opener =>
        {
            Assert.False(string.IsNullOrWhiteSpace(opener.Id));
            Assert.False(string.IsNullOrWhiteSpace(opener.Name));
            Assert.False(string.IsNullOrWhiteSpace(opener.IconKey));
        });

        Assert.Distinct(found.Select(opener => opener.Id));
    }

    /// <summary>
    /// A terminal is told where it is exactly once. Run directly it is told by the working
    /// directory, and a path in argv would be read as a command. Started through open it is
    /// told by the path, since LaunchServices ignores the working directory.
    /// </summary>
    [Fact]
    public async Task ATerminalIsToldWhereItIsExactlyOnce()
    {
        using var provider = Provider();

        var found = await provider.GetRequiredService<IWorkspaceOpenerFinder>().FindAsync(TestContext.Current.CancellationToken);

        Assert.All(
            found.Where(opener => opener.Kind is WorkspaceOpenerKind.Terminal),
            opener => Assert.Equal(LaunchServices, opener.TakesPathArgument));
    }

    /// <summary>Nothing detected is a quiet machine, not a failure.</summary>
    [Fact]
    public async Task FindingNothingIsNotAnError()
    {
        using var provider = Provider();

        var found = await provider.GetRequiredService<IWorkspaceOpenerFinder>().FindAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(found);
    }
}
