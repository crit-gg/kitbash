using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Kitbash.Tests;

[assembly: AvaloniaTestApplication(typeof(HeadlessApp))]

namespace Kitbash.Tests;

/// <summary>
/// The launcher's own theme with no display behind it, so a menu is built and drawn for
/// real without anything else on screen being part of it.
/// </summary>
public sealed class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<ThemedApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class ThemedApp : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://Kitbash.Ui/"))
        {
            Source = new Uri("avares://Kitbash.Ui/Themes/KitbashTheme.axaml"),
        });
    }
}
