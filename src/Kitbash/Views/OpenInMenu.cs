using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Kitbash.ViewModels;

namespace Kitbash.Views;

/// <summary>
/// Turns the rows the view model decided into menu controls. It is code rather than a
/// bound ItemsSource because the menu carries bitmaps and separators at once.
/// </summary>
public sealed class OpenInMenu
{
    private const int IconSize = 16;

    private readonly ExternalToolIcons _icons;

    public OpenInMenu(ExternalToolIcons icons)
    {
        ArgumentNullException.ThrowIfNull(icons);
        _icons = icons;
    }

    /// <summary>
    /// Rebuilds the flyout. Touches no disk, since everything it draws was gathered when
    /// the workspace was read.
    /// </summary>
    public void Fill(MenuFlyout flyout, IReadOnlyList<OpenInRow> rows)
    {
        ArgumentNullException.ThrowIfNull(flyout);
        ArgumentNullException.ThrowIfNull(rows);

        flyout.Items.Clear();

        foreach (var row in rows)
        {
            flyout.Items.Add(Control(row));
        }
    }

    private object Control(OpenInRow row)
    {
        if (row.IsSeparator)
        {
            return new Separator();
        }

        var item = new MenuItem { Header = row.Header };

        if (row.IconKey is { } key && _icons.Get(key) is { } mark)
        {
            item.Icon = Mark(mark);
        }

        if (row.Invoke is { } invoke)
        {
            item.Click += (_, e) =>
            {
                invoke();
                e.Handled = true;
            };
        }

        return item;
    }

    private static Image Mark(Bitmap source)
    {
        var image = new Image
        {
            Width = IconSize,
            Height = IconSize,
            Source = source,
        };

        // A brand mark is a bitmap being scaled down from 64, so it needs the better filter.
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

        return image;
    }
}
