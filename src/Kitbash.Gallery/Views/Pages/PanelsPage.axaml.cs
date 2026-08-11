using Avalonia.Interactivity;
using Kitbash.Ui.Controls;

namespace Kitbash.Gallery.Views.Pages;

/// <summary>The depth ramp, the panel shapes and everything that floats.</summary>
public partial class PanelsPage : GalleryPage
{
    public PanelsPage() => InitializeComponent();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        ShowNomadLevel();
    }

    private void OnTogglePane(object? sender, RoutedEventArgs e) =>
        Sidebar.IsPaneOpen = !Sidebar.IsPaneOpen;

    /// <summary>
    /// Moves a panel between two grounds of different depth, which is what docking will
    /// do to it. Nothing tells it its new tone and nothing recounts anything.
    /// </summary>
    private void OnMovePanel(object? sender, RoutedEventArgs e)
    {
        var home = ReferenceEquals(Nomad.Parent, ShallowGround);

        ShallowGround.Content = home ? null : Nomad;
        DeeperGround.Content = home ? Nomad : null;

        ShowNomadLevel();
    }

    private void ShowNomadLevel() =>
        NomadLevel.Text = $"it is on level {Surface.GetLevel(Nomad)}";
}
