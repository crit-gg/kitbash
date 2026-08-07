using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Core.Platform;
using Kitbash.Tools;
using Kitbash.Ui.Controls;
using Kitbash.ViewModels;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>
/// The card, drawn for real out of the window's own template, so what is checked is the
/// markup rather than a copy of it made here.
/// </summary>
public sealed class ToolCardTests
{
    /// <summary>The tile's size on each card, which the markup pins.</summary>
    private const int Tile = 40;
    private const int ScriptTile = 32;

    [AvaloniaFact]
    public void ATileWithNoIconDrawsTheLetter()
    {
        var card = Card(icon: null);

        Assert.Equal("F", card.Mark);
        Assert.False(card.HasIcon);

        var letter = Assert.Single(
            Draw(card).GetLogicalDescendants().OfType<TextBlock>(),
            block => block.Text == "F");

        Assert.True(letter.IsVisible);
    }

    /// <summary>
    /// The corner of a square icon falls outside the tile's curve, so this reads a pixel
    /// the clip has to have taken and one it has to have left alone.
    /// </summary>
    [AvaloniaFact]
    public void TheIconIsClippedToTheRoundedTile()
    {
        Clipped(Card(Filled(Colors.Red)), Tile);
    }

    /// <summary>The smaller tile a script card carries clips the same way.</summary>
    [AvaloniaFact]
    public void TheScriptCardClipsItsIconToo()
    {
        Clipped(Card(Filled(Colors.Red), script: true), ScriptTile);
    }

    /// <summary>
    /// A script is run and gone, so its card carries a smaller tile, tighter padding and
    /// compact buttons. Both keep two lines of description, so what is left between them
    /// is the chrome alone and the script card is still the shorter of the two.
    /// </summary>
    [AvaloniaFact]
    public void AScriptCardIsShorterThanAToolCard()
    {
        var full = Card_(Draw(Card(icon: null))).Bounds.Height;
        var compact = Card_(Draw(Card(icon: null, script: true))).Bounds.Height;

        Assert.True(compact < full, $"a script card is {compact} and a tool card is {full}");
    }

    /// <summary>
    /// Two lines and no more, however much a tool has to say about itself. The whole of a
    /// trimmed summary is on hover, so nothing is lost by the card being short.
    /// </summary>
    [AvaloniaFact]
    public void AToolCardKeepsTwoLinesOfDescription()
    {
        const string Long =
            "Slices sprite sheets, writes the atlas Godot imports, and reports every frame "
            + "it could not place so a person can fix the source art before the next run. "
            + "It reads every sheet under the source folder, including the ones nested "
            + "inside it, and leaves anything it did not recognise exactly where it was.";

        var text = Description_(Draw(Card(icon: null, summary: Long)), Long);

        Assert.Equal(2, text.MaxLines);
        Assert.True(text.Bounds.Height <= 36, $"the description is {text.Bounds.Height} tall");
        Assert.Equal(Long, ToolTip.GetTip(text));
    }

    /// <summary>
    /// A card promoting actions out of its menu carries a second row of buttons and is
    /// taller for it. Every card in the section takes that height, so the lead buttons
    /// across a row land on one line rather than stepping. Four cards over three columns,
    /// with the tall one on the second row, so both rows are covered.
    /// </summary>
    [AvaloniaFact]
    public void EveryCardInASectionIsAsTallAsTheTallest()
    {
        var plain = Card(icon: null);
        var promoted = Card(icon: null, actions: ["Reveal", "Copy path"]);

        Assert.False(plain.HasExtras);
        Assert.True(promoted.HasExtras);

        var cards = Cards_(Draw(plain, Card(icon: null), Card(icon: null), promoted));

        Assert.Equal(4, cards.Count);
        Assert.Single(cards.Select(card => card.Bounds.Height).Distinct());
    }

    /// <summary>Nothing was made wider to buy the height back.</summary>
    [AvaloniaFact]
    public void ThreeCardsStillGoAcross()
    {
        var cards = Cards_(Draw(Card(icon: null), Card(icon: null), Card(icon: null)));

        Assert.Single(cards.Select(card => card.Bounds.Width).Distinct());
        Assert.Equal(3, cards.Select(card => card.TranslatePoint(default, cards[0])!.Value.X).Distinct().Count());
    }

    /// <summary>
    /// A name too long for its column is trimmed on the card, so the whole of it has to be
    /// somewhere. A name that fits carries no tooltip, since it would only repeat itself.
    /// </summary>
    [AvaloniaFact]
    public void ATrimmedNameSaysTheWholeOfItselfOnHover()
    {
        const string Long = "Dependency report writer for every scene in the workspace";

        Assert.Equal(Long, ToolTip.GetTip(Name_(Draw(Card(icon: null, name: Long)))));
        Assert.Null(ToolTip.GetTip(Name_(Draw(Card(icon: null)))));
    }

    /// <summary>The lead button says what pressing it does, and for a script that is Run.</summary>
    [AvaloniaFact]
    public void AScriptCardLeadsWithRun()
    {
        var card = Card(icon: null, script: true);

        Assert.Equal("Run", card.LaunchLabel);
        Assert.Contains(
            Draw(card).GetLogicalDescendants().OfType<Button>(),
            button => button.IsVisible && Equals(button.Content, "Run"));
    }

    /// <summary>
    /// A tool no global list offers goes when the workspace offering it does, so the card
    /// carries a mark saying which workspaces bring it.
    /// </summary>
    [AvaloniaFact]
    public void AToolAWorkspaceOffersIsMarked()
    {
        var card = Offered("Art", "Foundry", "Docs");

        Assert.True(card.IsFromWorkspace);
        Assert.Equal("Provided by the Art, Foundry and Docs workspaces", card.WorkspaceTip);

        var drawn = Draw(card);
        var mark = Mark_(drawn);

        Assert.True(mark.IsVisible);
        Assert.Equal(card.WorkspaceTip, ToolTip.GetTip(mark));

        // Top right, and drawn rather than merely present.
        var at = mark.TranslatePoint(new Point(0, 0), Card_(drawn));

        Assert.NotNull(at);
        Assert.True(mark.Bounds.Width >= 12 && mark.Bounds.Height >= 12, $"the mark is {mark.Bounds.Size}");
        Assert.True(at!.Value.Y < 30, $"the mark sits {at.Value.Y} down the card");
        Assert.True(
            at.Value.X + mark.Bounds.Width > Card_(drawn).Bounds.Width - 60,
            $"the mark ends {at.Value.X + mark.Bounds.Width} across a card {Card_(drawn).Bounds.Width} wide");
    }

    /// <summary>
    /// A tool declaring itself the loose way is one somebody is building, which is worth
    /// saying on the card, since it runs from a folder rather than from anything installed.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AToolInDevelopmentIsMarked(bool script)
    {
        var card = Card(icon: null, script: script, development: true);

        Assert.True(card.IsInDevelopment);

        var drawn = Draw(card);
        var mark = DevelopmentMark_(drawn);

        Assert.True(mark.IsVisible);
        Assert.Equal(card.DevelopmentTip, ToolTip.GetTip(mark));

        // The warning tint, so it reads as a state rather than as another quiet mark.
        Assert.True(drawn.TryFindResource("Warn", out var warn));
        Assert.Equal(warn, Assert.Single(mark.GetLogicalDescendants().OfType<Icon>()).Foreground);

        // Top right, beside the workspace mark and the menu.
        var at = mark.TranslatePoint(new Point(0, 0), Card_(drawn));

        Assert.NotNull(at);
        Assert.True(at!.Value.Y < 30, $"the mark sits {at.Value.Y} down the card");
        Assert.True(
            at.Value.X + mark.Bounds.Width > Card_(drawn).Bounds.Width - 60,
            $"the mark ends {at.Value.X + mark.Bounds.Width} across a card {Card_(drawn).Bounds.Width} wide");
    }

    /// <summary>A tool that published a manifest is not one being built, so it says nothing.</summary>
    [AvaloniaFact]
    public void APublishedToolIsNotMarked()
    {
        var card = Card(icon: null);

        Assert.False(card.IsInDevelopment);
        Assert.False(DevelopmentMark_(Draw(card)).IsVisible);
    }

    /// <summary>One workspace reads as one rather than as a list of one.</summary>
    [AvaloniaFact]
    public void OneWorkspaceIsNamedOnItsOwn()
    {
        Assert.Equal("Provided by the Art workspace", Offered("Art").WorkspaceTip);
    }

    /// <summary>
    /// The global list offers a tool wherever a person is, so there is nothing for the mark
    /// to say and it is not drawn.
    /// </summary>
    [AvaloniaFact]
    public void AToolTheGlobalListOffersIsNotMarked()
    {
        var card = Offered();

        Assert.False(card.IsFromWorkspace);
        Assert.False(Mark_(Draw(card)).IsVisible);
    }

    private static void Clipped(ToolCardViewModel card, int side)
    {
        var page = Draw(card);

        Assert.True(card.HasIcon);

        var drawn = Pixels(page, Tile_(page, side));

        // Inside the curve is the icon and the corner is not, whatever the tile drew there.
        Assert.Equal(Colors.Red, drawn[side / 2, side / 2]);
        Assert.NotEqual(Colors.Red, drawn[2, 2]);
        Assert.NotEqual(Colors.Red, drawn[side - 3, 2]);
        Assert.NotEqual(Colors.Red, drawn[2, side - 3]);
        Assert.NotEqual(Colors.Red, drawn[side - 3, side - 3]);
    }

    /// <summary>
    /// A tool's own art is the tile. Art is rarely square to its own edge, so a fill behind
    /// it shows through the transparent room around it and reads as a border around the art.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArtHasNothingBehindIt(bool script)
    {
        var tile = Tile_(Draw(Card(Filled(Colors.Red), script: script)), script ? ScriptTile : Tile);

        Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(tile.Background).Color);
    }

    /// <summary>The tile is the plate a tool with no art of its own falls back to.</summary>
    [AvaloniaFact]
    public void ALetterKeepsItsPlate()
    {
        var drawn = Draw(Card(icon: null));
        var tile = Assert.Single(
            drawn.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("toolMark"));

        Assert.True(drawn.TryFindResource("AccentTint", out var tint));
        Assert.Equal(
            Assert.IsAssignableFrom<ISolidColorBrush>(tint).Color,
            Assert.IsAssignableFrom<ISolidColorBrush>(tile.Background).Color);
    }

    /// <summary>The letter goes when there is art, so the two never draw over each other.</summary>
    [AvaloniaFact]
    public void TheLetterGivesWayToTheIcon()
    {
        var letter = Assert.Single(
            Draw(Card(Filled(Colors.Red))).GetLogicalDescendants().OfType<TextBlock>(),
            block => block.Text == "F");

        Assert.False(letter.IsVisible);
    }

    /// <summary>
    /// One card, built out of the real card template inside the real group template and
    /// shown in the launcher itself, since the tile's fill and radius are styles the
    /// launcher window carries and a card hosted anywhere else draws without them.
    /// </summary>
    private static Control Draw(params ToolCardViewModel[] cards)
    {
        var launcher = new LauncherWindow();

        var groups = Assert.Single(
            launcher.GetLogicalDescendants().OfType<ItemsControl>(),
            items => items.Name == "ToolGroups");

        var group = new ToolGroupViewModel("INSTALLED TOOLS", cards, offersUpdates: false);
        var built = Assert.IsAssignableFrom<Control>(groups.ItemTemplate!.Build(group));

        built.DataContext = group;
        launcher.Content = built;

        launcher.Show();

        Dispatcher.UIThread.RunJobs();

        launcher.UpdateLayout();

        return built;
    }

    /// <summary>
    /// The tile itself, found from the border that does the clipping. The tile draws no
    /// stroke, so the clip fills it and the two carry the same radius.
    /// </summary>
    private static Border Tile_(Control drawn, int side)
    {
        var clip = Assert.Single(
            drawn.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "ToolIconClip");

        Assert.True(clip.ClipToBounds);
        Assert.Equal(new CornerRadius(8), clip.CornerRadius);
        Assert.Equal(new Size(side, side), clip.Bounds.Size);

        var tile = clip.GetVisualAncestors().OfType<Border>().First(border => border.Classes.Contains("toolMark"));

        Assert.Equal(new Thickness(0), tile.BorderThickness);
        Assert.Equal(new Size(side, side), tile.Bounds.Size);

        return tile;
    }

    /// <summary>The mark saying the tool is one somebody is building.</summary>
    private static Border DevelopmentMark_(Control drawn) =>
        Assert.Single(
            drawn.GetLogicalDescendants().OfType<Border>(),
            border => border.Name == "DevelopmentMark");

    /// <summary>The mark saying a workspace is what offers the tool.</summary>
    private static Border Mark_(Control drawn) =>
        Assert.Single(
            drawn.GetLogicalDescendants().OfType<Border>(),
            border => border.Name == "WorkspaceMark");

    /// <summary>The card's name, whichever of the two templates drew it.</summary>
    private static TextBlock Name_(Control drawn) =>
        Assert.Single(
            drawn.GetLogicalDescendants().OfType<TextBlock>(),
            block => block.Classes.Contains("toolName"));

    /// <summary>The card itself, whichever of the two templates drew it.</summary>
    private static Border Card_(Control drawn) => Assert.Single(Cards_(drawn));

    /// <summary>Every card the group drew, in the order the grid laid them out.</summary>
    private static IReadOnlyList<Border> Cards_(Control drawn) =>
        [.. drawn.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("toolCard"))];

    /// <summary>The card's description, found by what it says.</summary>
    private static TextBlock Description_(Control drawn, string text) =>
        Assert.Single(drawn.GetLogicalDescendants().OfType<TextBlock>(), block => block.Text == text);

    /// <summary>
    /// The tile as drawn, indexed by column and row. The whole page is what gets rendered
    /// and the tile is cut out of it, since rendering one visual on its own loses where it
    /// sits and the corners then read from the wrong pixels.
    /// </summary>
    private static Colour[,] Pixels(Control page, Border tile)
    {
        var side = (int)tile.Bounds.Width;
        var size = new PixelSize((int)page.Bounds.Width, (int)page.Bounds.Height);
        var origin = tile.TranslatePoint(new Point(0, 0), page);

        Assert.NotNull(origin);

        using var target = new RenderTargetBitmap(size);

        target.Render(page);

        var buffer = new byte[size.Width * size.Height * 4];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

        var stride = size.Width * 4;

        try
        {
            target.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), buffer.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        var drawn = new Colour[side, side];

        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var at = (((int)origin!.Value.Y + y) * stride) + (((int)origin.Value.X + x) * 4);

                // Bgra8888, which is what a render target is on every platform.
                drawn[x, y] = new Colour(buffer[at + 2], buffer[at + 1], buffer[at]);
            }
        }

        return drawn;
    }

    /// <summary>A square of one colour, standing in for a tool's own art.</summary>
    private static Bitmap Filled(Color colour)
    {
        var target = new RenderTargetBitmap(new PixelSize(64, 64));
        var fill = new Border { Background = new SolidColorBrush(colour), Width = 64, Height = 64 };

        fill.Measure(new Size(64, 64));
        fill.Arrange(new Rect(0, 0, 64, 64));

        target.Render(fill);

        return target;
    }

    /// <param name="script">A script, which is what the compact card is drawn for.</param>
    /// <param name="development">The tool declares itself the loose way.</param>
    /// <param name="actions">What the card promotes out of its menu, which grows it.</param>
    private static ToolCardViewModel Card(
        Bitmap? icon,
        bool script = false,
        string name = "Foundry",
        string summary = "A tool",
        bool development = false,
        params string[] actions)
    {
        Assert.True(ToolId.TryParse("foundry", out var id));

        var payload = new ToolPayload(ToolPayload.AnyRuntime, null, 0, null, "run");
        var manifest = Manifest(id, name, script) with
        {
            Summary = summary,
            IsDevelopment = development,
        };

        return new ToolCardViewModel(
            new InstalledTool(
                id,
                manifest.Version,
                "/tools/foundry",
                manifest,
                ToolCommand.For(payload, "/tools/foundry")),
            null,
            actions: actions)
        {
            Icon = icon,
            IsCompact = script,
        };
    }

    /// <summary>
    /// A card for a tool nothing has installed, offered by the repository the given
    /// workspaces list. None of them means the global list is what offers it.
    /// </summary>
    private static ToolCardViewModel Offered(params string[] workspaces)
    {
        Assert.True(ToolId.TryParse("github.foundry", out var id));

        var payload = new ToolPayload(ToolPayload.AnyRuntime, null, 0, null, "run");
        var manifest = Manifest(id, "Foundry", script: false);
        var url = WebAddress.Parse("https://github.com/owner/foundry");

        var source = new ToolRepositorySource(
            ToolRepositorySource.GitHub,
            url,
            workspaces.Length == 0 ? "the global config" : $"the {workspaces[0]} workspace",
            workspaces.FirstOrDefault());

        var release = new ToolRelease(
            manifest.Version,
            manifest.Version.ToString(),
            IsPrerelease: false,
            new Dictionary<string, WebAddress>(StringComparer.Ordinal));

        var offer = new OfferedTool(id, manifest.Version, manifest, "{}", payload, release, source)
        {
            Workspaces = workspaces,
        };

        return new ToolCardViewModel(null, offer);
    }

    private static ToolManifest Manifest(ToolId id, string name, bool script)
    {
        Assert.True(ToolVersion.TryParse("1.0.0", out var version));

        return new ToolManifest(
            script ? 2 : 1,
            id.Name,
            name,
            "A tool",
            "Tools",
            "foundry.png",
            version,
            Required: false,
            [new ToolPayload(ToolPayload.AnyRuntime, null, 0, null, "run")],
            script ? ToolKind.Script : ToolKind.App);
    }

    /// <summary>One pixel, so a failure names three numbers rather than a packed integer.</summary>
    private readonly record struct Colour(byte R, byte G, byte B)
    {
        public static implicit operator Colour(Color colour) => new(colour.R, colour.G, colour.B);
    }
}
