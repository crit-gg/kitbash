using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Kitbash.ViewModels;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>
/// Builds the real menu out of real controls, so what is checked is what a person opens.
/// </summary>
public sealed class OpenInMenuTests
{
    private static MenuFlyout Built(IReadOnlyList<OpenInRow> rows)
    {
        var flyout = new MenuFlyout();
        new OpenInMenu(new ExternalToolIcons()).Fill(flyout, rows);

        return flyout;
    }

    [AvaloniaFact]
    public void ASeparatorRowBecomesASeparator()
    {
        var flyout = Built([new OpenInRow { Header = "One" }, OpenInRow.Separator]);

        Assert.IsType<MenuItem>(flyout.Items[0]);
        Assert.IsType<Separator>(flyout.Items[1]);
    }

    /// <summary>The menu is one flat list, so nothing in it opens further.</summary>
    [AvaloniaFact]
    public void NoRowCarriesASubmenu()
    {
        var flyout = Built([
            new OpenInRow { Header = "Rider" },
            OpenInRow.Separator,
            new OpenInRow { Header = "Visual Studio Code" },
        ]);

        Assert.All(flyout.Items.OfType<MenuItem>(), item => Assert.Empty(item.Items));
    }

    [AvaloniaFact]
    public void PressingARowRunsWhatItWasGiven()
    {
        var ran = 0;
        var flyout = Built([new OpenInRow { Header = "Open folder", Invoke = () => ran++ }]);

        ((MenuItem)flyout.Items[0]!).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));

        Assert.Equal(1, ran);
    }

    /// <summary>Every mark the finders can name has to be in the launcher's assets.</summary>
    [AvaloniaTheory]
    [InlineData("vscode")]
    [InlineData("vscode_insiders")]
    [InlineData("codium")]
    [InlineData("cursor")]
    [InlineData("vs")]
    [InlineData("vs_preview")]
    [InlineData("custom")]
    [InlineData("jetbrains/RD")]
    [InlineData("jetbrains/WS")]
    [InlineData("terminal/konsole")]
    [InlineData("terminal/wezterm")]
    [InlineData("terminal/git_bash")]
    [InlineData("terminal/gnome_terminal")]
    public void ABrandMarkIsThere(string key)
    {
        Assert.NotNull(new ExternalToolIcons().Get(key));
    }

    /// <summary>A mark that is missing draws no icon and still draws the row.</summary>
    [AvaloniaFact]
    public void AMissingMarkIsNotAFailure()
    {
        var flyout = Built([new OpenInRow { Header = "Nothing", IconKey = "no/such/mark" }]);

        Assert.Null(new ExternalToolIcons().Get("no/such/mark"));
        Assert.Null(Assert.IsType<MenuItem>(flyout.Items[0]).Icon);
    }

    [AvaloniaFact]
    public void AKnownMarkIsDrawn()
    {
        var flyout = Built([new OpenInRow { Header = "Visual Studio Code", IconKey = "vscode" }]);

        Assert.NotNull(Assert.IsType<MenuItem>(flyout.Items[0]).Icon);
    }
}
