using Kitbash.Core.Platform;
using Kitbash.Core.Platform.Openers;
using Kitbash.Settings;
using Kitbash.Tools;
using Kitbash.Ui.Toasts;
using Kitbash.ViewModels;

namespace Kitbash.Tests;

/// <summary>
/// What the menu offers and what pressing a row opens, decided over fakes so the answer
/// does not depend on what happens to be installed on this machine.
/// </summary>
public sealed class OpenInRowTests
{
    private const string Root = "/home/a/ws";

    private static Harness Built(
        IReadOnlyList<WorkspaceOpener> openers,
        IReadOnlyDictionary<string, IReadOnlyList<OpenChoice>>? choices = null,
        AfterLaunchAction afterExternalTool = AfterLaunchAction.DoNothing,
        bool startFails = false)
    {
        var fake = new FakeOpeners(
            openers,
            choices ?? new Dictionary<string, IReadOnlyList<OpenChoice>>(),
            startFails);

        var platform = new FakePlatform();
        var toasts = new FakeToasts();
        var actions = new FakeAfterLaunchActions();

        var model = new OpenInViewModel(
            fake, platform, toasts, new FakeAfterLaunchSettings(afterExternalTool), actions);

        return new Harness(model, fake, platform, toasts, actions);
    }

    private static OpenInViewModel Model(
        IReadOnlyList<WorkspaceOpener> openers,
        IReadOnlyDictionary<string, IReadOnlyList<OpenChoice>>? choices = null) =>
        Built(openers, choices).Model;

    /// <summary>The view model and every fake behind it, so a test can read both ends.</summary>
    private sealed record Harness(
        OpenInViewModel Model,
        FakeOpeners Opened,
        FakePlatform Platform,
        FakeToasts Toasts,
        FakeAfterLaunchActions Actions);

    private static WorkspaceOpener Editor(string id, string name) =>
        new(id, name, id, $"/usr/bin/{id}", WorkspaceOpenerKind.Editor);

    private static WorkspaceOpener Terminal(string id, string name) =>
        new(id, name, $"terminal/{id}", $"/usr/bin/{id}", WorkspaceOpenerKind.Terminal)
        {
            TakesPathArgument = false,
        };

    [Fact]
    public async Task NoWorkspaceMeansNoRowsAndNoButton()
    {
        var model = Model([Editor("code", "Visual Studio Code")]);

        await model.RefreshAsync(null, TestContext.Current.CancellationToken);

        Assert.Empty(model.Rows);
        Assert.False(model.HasItems);
    }

    [Fact]
    public async Task TheFolderIsOfferedWheneverAnythingElseIs()
    {
        var model = Model([Editor("code", "Visual Studio Code")]);

        await model.RefreshAsync(Root, TestContext.Current.CancellationToken);

        Assert.Equal("Open folder", model.Rows[0].Header);
        Assert.True(model.HasItems);
    }

    /// <summary>Nothing to open the workspace in means no button at all.</summary>
    [Fact]
    public async Task NothingFoundMeansNoButton()
    {
        var model = Model([]);

        await model.RefreshAsync(Root, TestContext.Current.CancellationToken);

        Assert.Empty(model.Rows);
        Assert.False(model.HasItems);
    }

    /// <summary>A terminal on its own is still something worth opening in.</summary>
    [Fact]
    public async Task ATerminalAloneKeepsTheButton()
    {
        var model = Model([Terminal("konsole", "Konsole")]);

        await model.RefreshAsync(Root, TestContext.Current.CancellationToken);

        Assert.True(model.HasItems);
    }

    /// <summary>The menu is one flat list, however many terminals there are.</summary>
    [Fact]
    public async Task EveryTerminalIsItsOwnRow()
    {
        var model = Model([Terminal("konsole", "Konsole"), Terminal("foot", "Foot")]);

        await model.RefreshAsync(Root, TestContext.Current.CancellationToken);

        Assert.Contains(model.Rows, row => row.Header == "Konsole");
        Assert.Contains(model.Rows, row => row.Header == "Foot");
        Assert.DoesNotContain(model.Rows, row => row.Header == "Terminal");
    }

    /// <summary>Pressing a tool that found a solution opens the solution, not the folder.</summary>
    [Fact]
    public async Task PressingATooOpensTheFirstSolutionFound()
    {
        var solution = new OpenChoice("Game.sln", ["/home/a/ws/Game.sln"]);
        var (model, opened, _, _, _) = Built(
            [Editor("rider", "Rider")],
            new Dictionary<string, IReadOnlyList<OpenChoice>>
            {
                ["rider"] = [solution, new OpenChoice("Other.slnx", ["/home/a/ws/Other.slnx"])],
            });

        await model.RefreshAsync(Root, TestContext.Current.CancellationToken);
        Assert.Single(model.Rows, row => row.Header == "Rider").Invoke!();

        Assert.Same(solution, await opened.Next);
    }

    /// <summary>Nothing found means the same row opens the workspace folder.</summary>
    [Fact]
    public async Task PressingATooWithNoSolutionOpensTheFolder()
    {
        var (model, opened, _, _, _) = Built([Editor("rider", "Rider")]);

        await model.RefreshAsync(Root, TestContext.Current.CancellationToken);
        Assert.Single(model.Rows, row => row.Header == "Rider").Invoke!();

        Assert.Null(await opened.Next);
    }

    /// <summary>A rule sits between groups and never before, after or beside another.</summary>
    [Fact]
    public async Task SeparatorsNeverDoubleOrDangle()
    {
        var model = Model([Terminal("konsole", "Konsole"), Editor("code", "Visual Studio Code")]);

        await model.RefreshAsync(Root, TestContext.Current.CancellationToken);

        Assert.False(model.Rows[0].IsSeparator);
        Assert.False(model.Rows[^1].IsSeparator);

        for (var index = 1; index < model.Rows.Count; index++)
        {
            Assert.False(model.Rows[index].IsSeparator && model.Rows[index - 1].IsSeparator);
        }
    }

    /// <summary>A tool that really started is what the after launch setting follows.</summary>
    [Fact]
    public async Task StartingAToolAppliesTheAfterLaunchAction()
    {
        var harness = Built(
            [Editor("rider", "Rider")],
            afterExternalTool: AfterLaunchAction.Minimize);

        await harness.Model.RefreshAsync(Root, TestContext.Current.CancellationToken);
        Assert.Single(harness.Model.Rows, row => row.Header == "Rider").Invoke!();

        Assert.Equal(AfterLaunchAction.Minimize, await harness.Actions.Next);
    }

    /// <summary>A start that failed leaves the launcher up with its toast on screen.</summary>
    [Fact]
    public async Task AToolThatWouldNotStartAppliesNothing()
    {
        var harness = Built(
            [Editor("rider", "Rider")],
            afterExternalTool: AfterLaunchAction.Close,
            startFails: true);

        await harness.Model.RefreshAsync(Root, TestContext.Current.CancellationToken);
        Assert.Single(harness.Model.Rows, row => row.Header == "Rider").Invoke!();

        await harness.Toasts.Posted;

        Assert.Empty(harness.Actions.Applied);
    }

    /// <summary>Showing the workspace folder is not opening an external tool.</summary>
    [Fact]
    public async Task TheFolderRowAppliesNothing()
    {
        var harness = Built(
            [Editor("rider", "Rider")],
            afterExternalTool: AfterLaunchAction.Close);

        await harness.Model.RefreshAsync(Root, TestContext.Current.CancellationToken);
        Assert.Single(harness.Model.Rows, row => row.Header == "Open folder").Invoke!();

        await harness.Platform.Browsed;

        Assert.Empty(harness.Actions.Applied);
    }

    private sealed class FakeOpeners(
        IReadOnlyList<WorkspaceOpener> openers,
        IReadOnlyDictionary<string, IReadOnlyList<OpenChoice>> choices,
        bool startFails = false) : IWorkspaceOpeners
    {
        private readonly TaskCompletionSource<OpenChoice?> _opened = new();

        /// <summary>What the next press opens. Opening runs off the caller's thread.</summary>
        public Task<OpenChoice?> Next => _opened.Task;

        public Task<IReadOnlyList<WorkspaceOpener>> ReadAsync(CancellationToken cancellation = default) =>
            Task.FromResult(openers);

        // Nothing here hides one, so the menu's list and the whole list are the same.
        public Task<IReadOnlyList<WorkspaceOpener>> ReadAllAsync(CancellationToken cancellation = default) =>
            Task.FromResult(openers);

        public IReadOnlyList<OpenChoice> ChoicesFor(WorkspaceOpener opener, string workspaceRoot) =>
            choices.TryGetValue(opener.Id, out var found) ? found : [];

        public void Open(WorkspaceOpener opener, OpenChoice? choice, string workspaceRoot)
        {
            if (startFails)
            {
                throw new ProcessStartException(opener.Program, new InvalidOperationException());
            }

            _opened.TrySetResult(choice);
        }
    }

    private sealed class FakePlatform : IPlatformServices
    {
        private readonly TaskCompletionSource<DirectoryLocation> _browsed = new();

        /// <summary>The folder the next press shows. Opening runs off the caller's thread.</summary>
        public Task<DirectoryLocation> Browsed => _browsed.Task;

        public PlatformKind Kind => PlatformKind.Linux;

        public void OpenInBrowser(WebAddress address)
        {
        }

        public void OpenInFileBrowser(DirectoryLocation location) => _browsed.TrySetResult(location);

        public void StartDetached(ProcessRequest request)
        {
        }
    }

    private sealed class FakeToasts : IToastService
    {
        private readonly TaskCompletionSource<ToastRequest> _posted = new();

        /// <summary>What the next failure says. Reporting runs off the caller's thread.</summary>
        public Task<ToastRequest> Posted => _posted.Task;

        public Toast Show(ToastRequest request) => throw new NotSupportedException();

        public void Post(ToastRequest request) => _posted.TrySetResult(request);

        public IToastRegion Region(ToastAnchor anchor) => throw new NotSupportedException();

        public void DismissAll()
        {
        }
    }

    private sealed class FakeAfterLaunchSettings(AfterLaunchAction externalTool) : IAfterLaunchSettings
    {
        public AfterLaunchAction AfterProjectManager => AfterLaunchAction.DoNothing;

        public AfterLaunchAction AfterEditor => AfterLaunchAction.DoNothing;

        public AfterLaunchAction AfterPlay => AfterLaunchAction.DoNothing;

        public AfterLaunchAction AfterExternalTool => externalTool;

        public AfterLaunchAction AfterTool => AfterLaunchAction.DoNothing;

        public AfterLaunchAction ForTool(ToolId id) => AfterLaunchAction.DoNothing;
    }

    private sealed class FakeAfterLaunchActions : IAfterLaunchActions
    {
        private readonly List<AfterLaunchAction> _applied = [];
        private readonly TaskCompletionSource<AfterLaunchAction> _next = new();

        /// <summary>What the next press applies. Opening runs off the caller's thread.</summary>
        public Task<AfterLaunchAction> Next => _next.Task;

        public IReadOnlyList<AfterLaunchAction> Applied
        {
            get
            {
                lock (_applied)
                {
                    return [.. _applied];
                }
            }
        }

        public void Apply(AfterLaunchAction action)
        {
            lock (_applied)
            {
                _applied.Add(action);
            }

            _next.TrySetResult(action);
        }
    }
}
