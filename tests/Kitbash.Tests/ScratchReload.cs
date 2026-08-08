using Avalonia.Headless.XUnit;
using Kitbash.Core;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Core.Workspaces;
using Kitbash.Settings;
using Kitbash.Ui.Settings;
using Kitbash.ViewModels;
using Kitbash.Workspaces;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Tests;

public sealed class ScratchReload(Xunit.ITestOutputHelper output)
{
    [AvaloniaFact]
    public async Task StagedSurvivesAReload()
    {
        var home = Path.Combine(Path.GetTempPath(), $"kitbash-reload-{Guid.NewGuid():N}");

        using var services = new ServiceCollection()
            .AddSingleton<IUserDirectories>(new Dirs(home))
            .AddKitbashSettingsSchema()
            .AddKitbashKnownWorkspaceSettings()
            .AddSingleton<WorkspaceLog>()
            .AddSingleton<WorkspaceLinkIcons>()
            .AddSingleton<IWorkspaceLinks, WorkspaceLinks>()
            .AddSingleton<WorkspaceLinksSettingsSchema>()
            .BuildServiceProvider();

        var root = Path.Combine(home, "workspaces", "Art");
        var file = new WorkspacePaths(root).FileFor(SettingsScope.Global, SettingsLayer.User);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(
            file,
            $"[[workspace.links]]{Environment.NewLine}label = \"Design docs\"{Environment.NewLine}url = \"https://example.com/design\"{Environment.NewLine}");

        services.GetRequiredService<IWorkspaceRegistry>().Add(root);

        var model = new SettingsWindowViewModel(
            new SettingsSchema(
                SettingsScope.Global,
                "Kitbash Settings",
                [services.GetRequiredService<WorkspaceLinksSettingsSchema>().Page]),
            services.GetRequiredService<ISettingsInspector>(),
            services.GetRequiredService<ISettingsWriter>(),
            services.GetRequiredService<ISettingsValueConverter>(),
            services.GetRequiredService<IPathShortener>(),
            new NoRestart());

        await model.OpenAsync(model.First());

        var editor = model.Page!.Sections
            .SelectMany(section => section.Rows)
            .OfType<SettingsEditorRowViewModel>()
            .Select(row => row.Editor)
            .OfType<WorkspaceLinksEditor>()
            .Single();

        editor.Rows[0].Label = "The design docs";

        output.WriteLine($"before reload: {editor.Rows[0].Label}, dirty {model.DirtyCount}");

        // What the window does when it comes back to the front.
        await model.ReloadAsync();

        output.WriteLine($"after reload:  {editor.Rows[0].Label}, dirty {model.DirtyCount}");

        Directory.Delete(home, recursive: true);
    }

    private sealed class NoRestart : IApplicationRestart
    {
        public bool Restart() => false;
    }

    private sealed class Dirs(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state");

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "runtime");
    }
}
