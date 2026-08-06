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
/// The card's tile, drawn for real out of the window's own template, so what is checked is
/// the markup rather than a copy of it made here.
/// </summary>
public sealed class ToolIconTileTests
{
    /// <summary>The tile's size, which the markup pins.</summary>
    private const int Tile = 40;

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
        var card = Card(Filled(Colors.Red));
        var page = Draw(card);

        Assert.True(card.HasIcon);

        var drawn = Pixels(page, Tile_(page));

        // Inside the curve is the icon and the corner is not, whatever the tile drew there.
        Assert.Equal(Colors.Red, drawn[Tile / 2, Tile / 2]);
        Assert.NotEqual(Colors.Red, drawn[2, 2]);
        Assert.NotEqual(Colors.Red, drawn[Tile - 3, 2]);
        Assert.NotEqual(Colors.Red, drawn[2, Tile - 3]);
        Assert.NotEqual(Colors.Red, drawn[Tile - 3, Tile - 3]);
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

    /// <summary>The bordered tile itself, found from the border that does the clipping.</summary>
    private static Border Tile_(Control drawn)
    {
        var clip = Assert.Single(
            drawn.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "ToolIconClip");

        Assert.True(clip.ClipToBounds);
        Assert.Equal(new CornerRadius(7), clip.CornerRadius);
        Assert.Equal(new Size(38, 38), clip.Bounds.Size);

        return clip.GetVisualAncestors().OfType<Border>().First(border => border.Classes.Contains("toolMark"));
    }

    /// <summary>
    /// The tile as drawn, indexed by column and row. The whole page is what gets rendered
    /// and the tile is cut out of it, since rendering one visual on its own loses where it
    /// sits and the corners then read from the wrong pixels.
    /// </summary>
    private static Colour[,] Pixels(Control page, Border tile)
    {
        Assert.Equal(new Size(Tile, Tile), tile.Bounds.Size);

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

        var drawn = new Colour[Tile, Tile];

        for (var y = 0; y < Tile; y++)
        {
            for (var x = 0; x < Tile; x++)
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

    private static ToolCardViewModel Card(Bitmap? icon)
    {
        Assert.True(ToolId.TryParse("foundry", out var id));
        Assert.True(ToolVersion.TryParse("1.0.0", out var version));

        var payload = new ToolPayload(ToolPayload.AnyRuntime, null, 0, null, "run");

        var manifest = new ToolManifest(
            1,
            id.Name,
            "Foundry",
            "A tool",
            "Tools",
            "foundry.png",
            version,
            Required: false,
            [payload]);

        return new ToolCardViewModel(new InstalledTool(id, version, "/tools/foundry", manifest, payload), null)
        {
            Icon = icon,
        };
    }

    /// <summary>One pixel, so a failure names three numbers rather than a packed integer.</summary>
    private readonly record struct Colour(byte R, byte G, byte B)
    {
        public static implicit operator Colour(Color colour) => new(colour.R, colour.G, colour.B);
    }
}
