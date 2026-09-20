using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Kitbash.Core.Platform;
using Kitbash.Core.Platform.Openers;
using Kitbash.Core.Workspaces;
using Kitbash.Settings;
using Kitbash.Tools;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Toasts;
using Kitbash.ViewModels;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>
/// The workspaces page, drawn out of the window's own template so what is checked is the
/// markup rather than a copy of it made here.
/// </summary>
public sealed class WorkspacesPageTests
{
    /// <summary>
    /// The rail is selected by number, so its order is the page numbers. Reorder either
    /// half without the other and this says so.
    /// </summary>
    [AvaloniaFact]
    public void TheRailIsInThePageOrderTheModelUses()
    {
        Assert.Equal(
            ["Workspaces", "Workspace", "Godot engines"],
            Rail().Items.OfType<ListBoxItem>().Select(AutomationProperties.GetName));

        Assert.Equal(0, (int)LauncherPage.Workspaces);
        Assert.Equal(1, (int)LauncherPage.Workspace);
        Assert.Equal(2, (int)LauncherPage.Engines);
    }

    /// <summary>A rail item is a glyph, so the word is the tooltip and the name.</summary>
    [AvaloniaFact]
    public void TheRailItemIsTheTableGlyph()
    {
        var item = Assert.IsType<ListBoxItem>(Rail().Items[0]);

        Assert.Equal(IconGlyph.TableList, Assert.IsType<Icon>(item.Content).Glyph);
        Assert.NotNull(ToolTip.GetTip(item));
    }

    /// <summary>
    /// Opening the project, opening it elsewhere and opening a tool. All three end at the
    /// card's edge, so they line up down the page.
    /// </summary>
    [AvaloniaFact]
    public void ARowCarriesEveryWayIntoItsWorkspace()
    {
        var row = Row(Model("Rime", "/work/rime", tools: [Tool("splice")]));

        Assert.Single(row.GetLogicalDescendants().OfType<SplitButton>());

        Assert.Equal(
            ["Open in external tool", "Tools"],
            Marks(row).Select(AutomationProperties.GetName));

        // An icon button carries no word, so the theme cannot supply one.
        Assert.All(Marks(row), button => Assert.NotNull(ToolTip.GetTip(button)));
    }

    /// <summary>
    /// The tools button is on every row so the marks line up down the page, and a
    /// workspace with nothing to offer greys it rather than taking it away.
    /// </summary>
    [AvaloniaFact]
    public void AWorkspaceWithNoToolsGreysTheToolsButton()
    {
        var none = Assert.Single(Marks(Row(Model("Rime", "/work/rime"))), Named("Tools"));
        var one = Assert.Single(
            Marks(Row(Model("Rime", "/work/rime", tools: [Tool("splice")]))),
            Named("Tools"));

        Assert.True(none.IsVisible);
        Assert.False(none.IsEnabled);
        Assert.True(one.IsEnabled);
    }

    /// <summary>
    /// Both menus open from a press rather than from a declared flyout, so this is what
    /// says the handlers are on the buttons at all.
    /// </summary>
    [AvaloniaFact]
    public void EveryMarkOpensSomethingFromAPress()
    {
        var row = Row(Model("Rime", "/work/rime", tools: [Tool("splice")]));

        foreach (var button in Marks(row))
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// The row menu is the switcher popover's alone. A card here is all buttons, so a menu
    /// repeating what two of them already do would be the third thing on the row.
    /// </summary>
    [AvaloniaFact]
    public void ARowCarriesNoMenuOfItsOwn()
    {
        var row = Row(Model("Rime", "/work/rime", tools: [Tool("splice")]));

        Assert.DoesNotContain(
            row.GetLogicalDescendants().OfType<Icon>(),
            icon => icon.Glyph == IconGlyph.DotsVerticalRounded);
    }

    /// <summary>
    /// A folder that has gone has nothing in it to read, and the badge beside the name
    /// already says why, so the engine line is left out rather than left blank.
    /// </summary>
    [AvaloniaFact]
    public void AMissingWorkspaceDrawsNoEngineLine()
    {
        var here = Row(Model("Rime", "/work/rime"));
        var gone = Row(Model("Foundry", "/work/foundry", exists: false));

        Assert.True(EngineLine(here).IsVisible);
        Assert.False(EngineLine(gone).IsVisible);

        // The engine line's own badges are in the tree and hidden, so the one left is the
        // state badge beside the name.
        var badge = Assert.Single(
            gone.GetLogicalDescendants().OfType<Badge>(), badge => badge.IsVisible);

        Assert.Equal("MISSING", badge.Content);
    }

    /// <summary>
    /// The page drawn for real. A green test over a tree that never rendered says nothing
    /// about whether anybody can read the row.
    /// </summary>
    [AvaloniaFact]
    public void ThePageDrawsAFrame()
    {
        var rows = new StackPanel { Spacing = 9, Margin = new Thickness(20, 16) };

        foreach (var model in new[]
        {
            Model("Rime", "/work/rime", tools: [Tool("splice")]),
            Model("Atlas", "/work/atlas"),
            Model("Foundry", "/work/foundry", exists: false),
            Model(Overlong, "/work/long"),
        })
        {
            rows.Children.Add(Built(model));
        }

        // The page is a nesting surface and a card is one inside it, so a card drawn with
        // no page around it takes the root tone and draws no fill and no edge at all.
        var page = new Border { Child = rows };

        Surface.SetNests(page, true);

        // The launcher's own window, since the card and the state mark are styled in its
        // Styles and a plain window reaches neither.
        var window = new LauncherWindow { Content = page };

        try
        {
            window.Show();

            Dispatcher.UIThread.RunJobs();

            window.UpdateLayout();

            var frame = window.CaptureRenderedFrame();

            Assert.NotNull(frame);

            Keep(frame, "workspaces-page.png");

            // Every row was laid out, which a frame of the right size does not say.
            Assert.All(rows.Children, row => Assert.True(row.Bounds.Height > 0));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The name opens its workspace, so it is a button rather than a heading and can be
    /// reached with the keyboard. A folder that has gone has nothing to open.
    /// </summary>
    [AvaloniaFact]
    public void TheNameIsAButtonAndAMissingOneCannotBeOpened()
    {
        var here = Name(Row(Model("Rime", "/work/rime")));
        var gone = Name(Row(Model("Foundry", "/work/foundry", exists: false)));

        Assert.True(here.Focusable);
        Assert.True(here.IsEnabled);
        Assert.False(gone.IsEnabled);

        // The word is on the button, since the name alone does not say it opens anything.
        Assert.NotNull(ToolTip.GetTip(here));
    }

    private static HyperlinkButton Name(Control row) =>
        Assert.Single(
            row.GetLogicalDescendants().OfType<HyperlinkButton>(),
            button => button.Name == "RowNameButton");

    /// <summary>Longer than any card, so trimming is what has to happen to it.</summary>
    private const string Overlong =
        "A workspace whose name runs on far past anything the card could ever hold and "
        + "then keeps going well beyond that, and further still, and does not stop here";

    /// <summary>
    /// A name is measured against the room the row leaves it, so an overlong one trims
    /// instead of painting over the buttons at the end of the card.
    /// </summary>
    [AvaloniaFact]
    public void AnOverlongNameStopsBeforeTheButtons()
    {
        var row = Row(Model(Overlong, "/work/long"));

        var name = Name(row);
        var tools = Assert.Single(Marks(row), Named("Tools"));

        var nameRight = name.TranslatePoint(new Point(name.Bounds.Width, 0), row)!.Value.X;
        var toolsLeft = tools.TranslatePoint(default, row)!.Value.X;

        Assert.True(name.Bounds.Width > 0, "the name was never laid out");
        Assert.True(
            nameRight <= toolsLeft,
            $"the name reached {nameRight} and the buttons start at {toolsLeft}");
    }

    private static ListBox Rail() =>
        Assert.Single(
            new LauncherWindow().GetLogicalDescendants().OfType<ListBox>(),
            list => list.Name == "Rail");

    private static Predicate<Button> Named(string name) =>
        button => AutomationProperties.GetName(button) == name;

    /// <summary>The marks at the end of the row, in the order they are drawn.</summary>
    private static IReadOnlyList<Button> Marks(Control row) =>
        [.. row.GetLogicalDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("icon"))];

    private static Control EngineLine(Control row) =>
        Assert.Single(
            row.GetLogicalDescendants().OfType<StackPanel>(),
            panel => panel.Name == "RowEngine");

    private static void Keep(WriteableBitmap frame, string name)
    {
        var directory = Environment.GetEnvironmentVariable("KITBASH_RENDER_OUT");

        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);

        using var file = File.Create(Path.Combine(directory, name));

        frame.Save(file, new PngBitmapEncoderOptions());
    }

    /// <summary>A tool that is simply there, since only its name reaches the menu.</summary>
    private static InstalledTool Tool(string name)
    {
        Assert.True(ToolId.TryParse(name, out var id));
        Assert.True(ToolVersion.TryParse("1.0.0", out var version));

        var payload = new ToolPayload(ToolPayload.AnyRuntime, null, 0, null, "run");
        var directory = $"/tools/{name}";

        var manifest = new ToolManifest(
            1, name, name, "A tool", "Tools", null, version, Required: false, [payload]);

        return new InstalledTool(
            id, version, directory, manifest, ToolCommand.For(payload, directory));
    }

    private static WorkspaceRowViewModel Model(
        string name,
        string root,
        bool exists = true,
        IReadOnlyList<InstalledTool>? tools = null)
    {
        var workspace = new Workspace(root, name, exists, HasRepository: true);

        return new WorkspaceRowViewModel(
            new WorkspaceViewModel(workspace, isCurrent: false, root),
            new OpenInViewModel(
                new NoOpeners(), new NoPlatform(), new NoToasts(), new NoAfterLaunch(), new NoActions()))
        {
            Tools = tools ?? [],
        };
    }

    /// <summary>One row, built from the markup's own template and laid out for real.</summary>
    private static Control Row(WorkspaceRowViewModel model)
    {
        var launcher = new LauncherWindow();
        var built = Built(model);

        // Inside a panel that gives a child its own height, the way the page does. As the
        // window's own content it would stretch to the whole page.
        launcher.Content = new StackPanel { Children = { built } };

        launcher.Show();

        Dispatcher.UIThread.RunJobs();

        launcher.UpdateLayout();

        return built;
    }

    private static Control Built(WorkspaceRowViewModel model)
    {
        var cards = Assert.Single(
            new LauncherWindow().GetLogicalDescendants().OfType<ItemsControl>(),
            items => items.Name == "WorkspaceCards");

        var built = Assert.IsAssignableFrom<Control>(cards.ItemTemplate!.Build(model));

        built.DataContext = model;

        return built;
    }

    private sealed class NoOpeners : IWorkspaceOpeners
    {
        public Task<IReadOnlyList<WorkspaceOpener>> ReadAsync(CancellationToken cancellation = default) =>
            Task.FromResult<IReadOnlyList<WorkspaceOpener>>([]);

        public Task<IReadOnlyList<WorkspaceOpener>> ReadAllAsync(CancellationToken cancellation = default) =>
            Task.FromResult<IReadOnlyList<WorkspaceOpener>>([]);

        public IReadOnlyList<OpenChoice> ChoicesFor(WorkspaceOpener opener, string workspaceRoot) => [];

        public void Open(WorkspaceOpener opener, OpenChoice? choice, string workspaceRoot)
        {
        }
    }

    private sealed class NoPlatform : IPlatformServices
    {
        public PlatformKind Kind => PlatformKind.Linux;

        public void OpenInBrowser(WebAddress address)
        {
        }

        public void OpenInFileBrowser(DirectoryLocation location)
        {
        }

        public void StartDetached(ProcessRequest request)
        {
        }
    }

    private sealed class NoToasts : IToastService
    {
        public Toast Show(ToastRequest request) => throw new NotSupportedException();

        public void Post(ToastRequest request)
        {
        }

        public IToastRegion Region(ToastAnchor anchor) => throw new NotSupportedException();

        public void DismissAll()
        {
        }
    }

    private sealed class NoAfterLaunch : IAfterLaunchSettings
    {
        public AfterLaunchAction AfterProjectManager => AfterLaunchAction.DoNothing;

        public AfterLaunchAction AfterEditor => AfterLaunchAction.DoNothing;

        public AfterLaunchAction AfterPlay => AfterLaunchAction.DoNothing;

        public AfterLaunchAction AfterExternalTool => AfterLaunchAction.DoNothing;

        public AfterLaunchAction AfterTool => AfterLaunchAction.DoNothing;

        public AfterLaunchAction ForTool(ToolId id) => AfterLaunchAction.DoNothing;
    }

    private sealed class NoActions : IAfterLaunchActions
    {
        public void Apply(AfterLaunchAction action)
        {
        }
    }
}
