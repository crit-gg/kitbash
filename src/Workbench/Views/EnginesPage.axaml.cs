using Avalonia.Controls;
using Avalonia.Interactivity;
using Workbench.ViewModels;

namespace Workbench.Views;

public partial class EnginesPage : UserControl
{
    public EnginesPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The first read happens when the page is first shown rather than when the window is
    /// built. It fetches over a network, so doing it at launch would make every start wait
    /// on one whether or not anyone opened this page.
    /// </summary>
    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is EnginesViewModel engines)
        {
            await engines.LoadAsync();
        }
    }
}
