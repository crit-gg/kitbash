using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Kitbash.Core;
using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Settings;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Tests;

/// <summary>
/// The row under the PATH setting, in words and drawn by the real settings window with no
/// display behind it.
/// </summary>
public sealed class EngineCommandReadoutTests : IDisposable
{
    private const string Command = "/opt/people/bin/godot";
    private const string Folder = "/opt/people/bin";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"kitbash-readout-{Guid.NewGuid():N}");
    private readonly FakeCommand _command = new();
    private readonly ServiceProvider _services;
    private readonly EngineCommandReadout _readout;

    public EngineCommandReadoutTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IUserDirectories>(new FakeUserDirectories(_root))
            .AddKitbashSettingsSchema()
            .AddSingleton<IApplicationRestart>(new FakeRestart())
            .BuildServiceProvider();

        _readout = new EngineCommandReadout(_command, _services.GetRequiredService<IPathShortener>());
    }

    public void Dispose()
    {
        _services.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void OffSaysNothing()
    {
        Assert.Empty(Lines(new EngineCommandState(EngineCommandKind.Off, Command)));
    }

    [Fact]
    public void APlacedCommandNamesTheEngineItOpens()
    {
        var lines = _readout.Describe(Placed(PathReach.OnPath));

        var line = Assert.Single(lines);
        Assert.Equal($"{Command} opens Godot 4.7.1 stable .NET", line.Text);
        Assert.True(line.IsCurrent);
        Assert.Equal(Command, line.Copied);
    }

    [Fact]
    public void AFolderJustAddedAsksForANewTerminal()
    {
        Assert.Equal(
            [$"{Command} opens Godot 4.7.1 stable .NET", "Open a new terminal to use it."],
            Lines(Placed(PathReach.Added)));
    }

    [Fact]
    public void AFolderOffPathSaysSo()
    {
        var lines = _readout.Describe(Placed(PathReach.NotOnPath));

        Assert.Equal($"{Folder} is not on PATH. Add it to run godot from a terminal.", lines[1].Text);
        Assert.Equal(Folder, lines[1].Copied);
    }

    [Fact]
    public void ARefusalSaysHowToBeAskedAgain()
    {
        Assert.Equal(
            $"{Folder} is not on PATH. Turn this off and on to be asked again.",
            Lines(Placed(PathReach.Declined))[1]);
    }

    [Fact]
    public void AnotherGodotFirstOnPathIsNamed()
    {
        var state = Placed(PathReach.OnPath) with { Reach = new CommandReach(PathReach.OnPath, "/usr/bin/godot") };

        Assert.Equal("/usr/bin/godot comes first on PATH, so godot runs that one.", Lines(state)[1]);
    }

    [Fact]
    public void EveryOtherStateIsOneSentence()
    {
        Assert.Equal(
            [$"{Command} was not made by Kitbash. Remove it to let Kitbash write one."],
            Lines(new EngineCommandState(EngineCommandKind.Conflict, Command)));
        Assert.Equal(
            ["No default engine is set. Name one on the engines page."],
            Lines(new EngineCommandState(EngineCommandKind.NoDefault, Command)));
        Assert.Equal(
            ["This copy of Kitbash cannot put Godot on PATH."],
            Lines(new EngineCommandState(EngineCommandKind.Unavailable, Command)));
        Assert.Equal(
            ["There is no home folder to put it in."],
            Lines(new EngineCommandState(EngineCommandKind.Unavailable)));
        Assert.Equal(
            [$"Could not write {Command}. Permission denied"],
            Lines(new EngineCommandState(EngineCommandKind.Failed, Command, Failure: "Permission denied")));
    }

    /// <summary>The readout reads by syncing, so what it draws is what the sync answered.</summary>
    [AvaloniaFact]
    public async Task ThePageDrawsWhatTheSyncAnswered()
    {
        _command.State = Placed(PathReach.NotOnPath);

        var (window, model) = await Shown();

        try
        {
            var row = Row(model);

            Assert.True(row.IsShown);
            Assert.Equal(1, _command.Synced);

            var drawn = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(text => text.Text)
                .ToList();

            Assert.Contains($"{Command} opens Godot 4.7.1 stable .NET", drawn);
            Assert.Contains($"{Folder} is not on PATH. Add it to run godot from a terminal.", drawn);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Off has nothing to say, so the row is not drawn at all.</summary>
    [AvaloniaFact]
    public async Task OffLeavesTheRowOut()
    {
        _command.State = new EngineCommandState(EngineCommandKind.Off, Command);

        var (window, model) = await Shown();

        try
        {
            Assert.False(Row(model).IsShown);

            var names = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(text => text.Text == "godot command" && text.IsEffectivelyVisible)
                .ToList();

            Assert.Empty(names);
        }
        finally
        {
            window.Close();
        }
    }

    private static EngineCommandState Placed(PathReach reach) => new(
        EngineCommandKind.Placed,
        Command,
        Engine(),
        new CommandReach(reach, null));

    private static InstalledEngine Engine()
    {
        var record = new EngineRecord(
            EngineId.Parse("4.7.1-stable-mono"),
            EnginePlatform.Linux,
            EngineArchitecture.X64,
            "4.7.1.stable.mono.official",
            "Godot",
            string.Empty,
            string.Empty,
            DateTimeOffset.UnixEpoch);

        return new InstalledEngine(record, "/engines/4.7.1-stable-mono", "/engines/4.7.1-stable-mono/Godot", 0, false, false);
    }

    private List<string> Lines(EngineCommandState state) => [.. _readout.Describe(state).Select(line => line.Text)];

    private static SettingsReadoutRowViewModel Row(SettingsWindowViewModel model) =>
        model.Page!.Sections
            .SelectMany(section => section.Rows)
            .OfType<SettingsReadoutRowViewModel>()
            .Single(row => row.Name == "godot command");

    private async Task<(SettingsWindow Window, SettingsWindowViewModel Model)> Shown()
    {
        var page = _services.GetRequiredService<GodotSettingsSchema>().PageWith(_readout.Row);

        var model = new SettingsWindowViewModel(
            new SettingsSchema(SettingsScope.Global, "Kitbash Settings", [page]),
            _services.GetRequiredService<ISettingsInspector>(),
            _services.GetRequiredService<ISettingsWriter>(),
            _services.GetRequiredService<ISettingsValueConverter>(),
            _services.GetRequiredService<IPathShortener>(),
            _services.GetRequiredService<IApplicationRestart>());

        await model.OpenAsync(model.First());

        Assert.Equal(string.Empty, model.Problem);

        var window = new SettingsWindow { DataContext = model, Width = 900, Height = 600 };

        window.Show();

        return (window, model);
    }

    private sealed class FakeCommand : IEngineCommand
    {
        public EngineCommandState State { get; set; } = new(EngineCommandKind.Off);

        public int Synced { get; private set; }

        public Task<EngineCommandState> SyncAsync(CancellationToken cancellation)
        {
            Synced++;

            return Task.FromResult(State);
        }
    }

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
