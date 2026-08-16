using Kitbash.Core.IO;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Core.Tests.Platform;

/// <summary>
/// Whether two paths mean one place. This is the one thing macOS answers the way Windows
/// does rather than the way Linux does, since the filesystem it ships with ignores case.
/// </summary>
public sealed class MacPathRulesTests
{
    public static bool OnMac => OperatingSystem.IsMacOS();

    private const string MacOnly = "The default filesystem here ignores case.";

    private static IPathRules Rules(ServiceProvider provider) =>
        provider.GetRequiredService<IPathRules>();

    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddKitbashIO();

        return services.BuildServiceProvider();
    }

    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void CaseDoesNotMakeTwoPlaces()
    {
        using var provider = Provider();

        Assert.True(Rules(provider).AreSame("/Users/someone/Games", "/Users/someone/games"));
    }

    /// <summary>The separator is part of the test, or game-tools would sit inside game.</summary>
    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void ASharedPrefixIsNotContainment()
    {
        using var provider = Provider();

        Assert.False(Rules(provider).Contains("/Users/someone/game", "/Users/someone/game-tools"));
        Assert.True(Rules(provider).Contains("/Users/someone/game", "/Users/someone/Game/art"));
    }
}
