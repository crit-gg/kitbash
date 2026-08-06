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
using Kitbash.Tools;
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
    /// A script is run and gone, so its card keeps two lines of description where a tool
    /// card keeps four, and drops the promoted actions. Plainly shorter is the point of a
    /// separate section, not an incidental few pixels.
    /// </summary>
    [AvaloniaFact]
    public void AScriptCardIsMuchShorterThanAToolCard()
    {
        var full = Card_(Draw(Card(icon: null))).Bounds.Height;
        var compact = Card_(Draw(Card(icon: null, script: true))).Bounds.Height;

        Assert.True(compact <= full * 0.65, $"a script card is {compact} and a tool card is {full}");
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
    /// shown in the launcher itself, since the tile's stroke and radius are styles the
    /// launcher window carries and a card hosted anywhere else draws without them.
    /// </summary>
    private static Control Draw(ToolCardViewModel card)
    {
        var launcher = new LauncherWindow();

        var groups = Assert.Single(
            launcher.GetLogicalDescendants().OfType<ItemsControl>(),
            items => items.Name == "ToolGroups");

        var group = new ToolGroupViewModel("INSTALLED TOOLS", [card], offersUpdates: false);
        var built = Assert.IsAssignableFrom<Control>(groups.ItemTemplate!.Build(group));

        built.DataContext = group;
        launcher.Content = built;

        launcher.Show();

        Dispatcher.UIThread.RunJobs();

        launcher.UpdateLayout();

        return built;
    }

    /// <summary>
    /// The bordered tile itself, found from the border that does the clipping. The clip sits
    /// inside the tile's own one pixel stroke, which is what its radius is a step under.
    /// </summary>
    private static Border Tile_(Control drawn, int side)
    {
        var clip = Assert.Single(
            drawn.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "ToolIconClip");

        Assert.True(clip.ClipToBounds);
        Assert.Equal(new CornerRadius(7), clip.CornerRadius);
        Assert.Equal(new Size(side - 2, side - 2), clip.Bounds.Size);

        var tile = clip.GetVisualAncestors().OfType<Border>().First(border => border.Classes.Contains("toolMark"));

        Assert.Equal(new Size(side, side), tile.Bounds.Size);

        return tile;
    }

    /// <summary>The card's name, whichever of the two templates drew it.</summary>
    private static TextBlock Name_(Control drawn) =>
        Assert.Single(
            drawn.GetLogicalDescendants().OfType<TextBlock>(),
            block => block.Classes.Contains("toolName"));

    /// <summary>The card itself, whichever of the two templates drew it.</summary>
    private static Border Card_(Control drawn) =>
        Assert.Single(drawn.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("toolCard"));

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
    private static ToolCardViewModel Card(Bitmap? icon, bool script = false, string name = "Foundry")
    {
        Assert.True(ToolId.TryParse("foundry", out var id));
        Assert.True(ToolVersion.TryParse("1.0.0", out var version));

        var payload = new ToolPayload(ToolPayload.AnyRuntime, null, 0, null, "run");

        var manifest = new ToolManifest(
            script ? 2 : 1,
            id.Name,
            name,
            "A tool",
            "Tools",
            "foundry.png",
            version,
            Required: false,
            [payload],
            script ? ToolKind.Script : ToolKind.App);

        return new ToolCardViewModel(new InstalledTool(id, version, "/tools/foundry", manifest, payload), null)
        {
            Icon = icon,
            IsCompact = script,
        };
    }

    /// <summary>One pixel, so a failure names three numbers rather than a packed integer.</summary>
    private readonly record struct Colour(byte R, byte G, byte B)
    {
        public static implicit operator Colour(Color colour) => new(colour.R, colour.G, colour.B);
    }
}
