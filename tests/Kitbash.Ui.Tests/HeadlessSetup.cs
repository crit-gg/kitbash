using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Kitbash.Ui.Tests;

[assembly: AvaloniaTestApplication(typeof(HeadlessApp))]

namespace Kitbash.Ui.Tests;

/// <summary>
/// An application carrying nothing but the library's own theme, so what a test measures is
/// the theme rather than anything an app put over it. Skia draws for real with no display.
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
