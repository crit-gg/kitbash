using Avalonia.Headless.XUnit;
using Kitbash.Core;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Tests;

/// <summary>
/// Opening the settings window on a named page, which is what a kitbash link asks for.
/// The real window over a schema of three pages, drawn with no display behind it.
/// </summary>
public sealed class SettingsPageLinkTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"kitbash-settings-link-{Guid.NewGuid():N}");

    private readonly ServiceProvider _services;

    public SettingsPageLinkTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IUserDirectories>(new FakeUserDirectories(_root))
            .AddKitbashSettingsSchema()
            .AddSingleton<IApplicationRestart>(new FakeRestart())
            .BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task AWindowOpensOnThePageItWasNamed()
    {
        var (window, model) = await Shown("third");

        try
        {
            Assert.Equal("Third", model.Page?.Title);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Naming nothing is what an app that simply opened its settings does.</summary>
    [AvaloniaFact]
    public async Task NoPageOpensTheFirstOne()
    {
        var (window, model) = await Shown(null);

        try
        {
            Assert.Equal("First", model.Page?.Title);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A link can name a page a schema does not hold, since a link outlives the version
    /// that was current when somebody wrote it.
    /// </summary>
    [AvaloniaFact]
    public async Task APageTheSchemaDoesNotHoldOpensTheFirstOne()
    {
        var (window, model) = await Shown("gone");

        try
        {
            Assert.Equal("First", model.Page?.Title);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A second link goes to a window that is already up rather than opening one.</summary>
    [AvaloniaFact]
    public async Task AWindowAlreadyOpenIsMovedToThePage()
    {
        var (window, model) = await Shown("first");

        try
        {
            window.ShowPage("second");

            Assert.Equal("Second", model.Page?.Title);

            // An id nothing answers for leaves the window where it is.
            window.ShowPage("gone");

            Assert.Equal("Second", model.Page?.Title);
        }
        finally
        {
            window.Close();
        }
    }

    private async Task<(SettingsWindow Window, SettingsWindowViewModel Model)> Shown(string? page)
    {
        var model = new SettingsWindowViewModel(
            new SettingsSchema(
                SettingsScope.Global,
                "Kitbash Settings",
                [Page("first", "First"), Page("second", "Second"), Page("third", "Third")]),
            _services.GetRequiredService<ISettingsInspector>(),
            _services.GetRequiredService<ISettingsWriter>(),
            _services.GetRequiredService<ISettingsValueConverter>(),
            _services.GetRequiredService<IPathShortener>(),
            _services.GetRequiredService<IApplicationRestart>());

        var window = new SettingsWindow
        {
            StartPage = page,
            DataContext = model,
            Width = 900,
            Height = 600,
        };

        window.Show();

        // Opening a page reads its files, and Show does not wait for that.
        await Task.Yield();

        return (window, model);
    }

    private static SettingsPage Page(string id, string title) => new()
    {
        Id = id,
        Title = title,
        Home = SettingsHome.Application,
        Sections =
        [
            new SettingsSection(title, [new SettingDescriptor<string>
            {
                Key = $"link.{id}",
                Name = title,
                Description = "A setting drawn for this test.",
                Default = string.Empty,
            }]),
        ],
    };

    private sealed class FakeRestart : IApplicationRestart
    {
        public bool Restart() => throw new NotSupportedException();
    }

    private sealed class FakeUserDirectories(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state");

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "runtime");
    }
}
