using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Kitbash.Core.Platform;
using Kitbash.Ui.Settings;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>
/// What the real window does with a link. The window is built headlessly with no view
/// model, which is the state a link can arrive in.
/// </summary>
public sealed class DeepLinkRoutingTests
{
    [AvaloniaFact]
    public async Task ASettingsLinkOpensThatPage()
    {
        var settings = new FakeSettingsWindows();
        var window = new LauncherWindow { Settings = settings };

        await Follow(window, "kitbash://settings/toolRepositories");

        Assert.Equal("toolRepositories", settings.Page);
    }

    /// <summary>A page id is camel case, so folding it would name nothing.</summary>
    [AvaloniaFact]
    public async Task ThePageIdKeepsItsCase()
    {
        var settings = new FakeSettingsWindows();
        var window = new LauncherWindow { Settings = settings };

        await Follow(window, "kitbash://settings/workspaceGodot");

        Assert.Equal("workspaceGodot", settings.Page);
    }

    /// <summary>Naming no page opens the window on the first one, which is what null means.</summary>
    [AvaloniaFact]
    public async Task ASettingsLinkWithNoPageStillOpens()
    {
        var settings = new FakeSettingsWindows();
        var window = new LauncherWindow { Settings = settings };

        await Follow(window, "kitbash://settings");

        Assert.True(settings.Opened);
        Assert.Null(settings.Page);
    }

    [AvaloniaFact]
    public async Task AVerbNothingAnswersForOpensNothing()
    {
        var settings = new FakeSettingsWindows();
        var window = new LauncherWindow { Settings = settings };

        await Follow(window, "kitbash://something-else/x");

        Assert.False(settings.Opened);
    }

    /// <summary>Every other verb needs a view model, and a link can arrive before there is one.</summary>
    [AvaloniaTheory]
    [InlineData("kitbash://clone?repo=https://example.com/team/game.git")]
    [InlineData("kitbash://engine/4.7.1")]
    [InlineData("kitbash://tool/foundry")]
    [InlineData("kitbash://tools/repository?add=https://example.com/tools")]
    public async Task AWindowWithNoModelSurvivesEveryVerb(string text)
    {
        await Follow(new LauncherWindow(), text);
    }

    private static async Task Follow(LauncherWindow window, string text)
    {
        Assert.True(DeepLink.TryParse(text, out var link));

        await window.FollowAsync(link);
    }

    private sealed class FakeSettingsWindows : ISettingsWindows
    {
        public bool Opened { get; private set; }

        public string? Page { get; private set; }

        // Nothing here closes, and the window only subscribes.
        public event EventHandler? Closed
        {
            add { }
            remove { }
        }

        public void Open(Window owner, string? page = null)
        {
            Opened = true;
            Page = page;
        }
    }
}
