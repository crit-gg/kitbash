using Avalonia.Controls;
using Avalonia.Interactivity;
using Kitbash.Ui.Controls;

namespace Kitbash.Gallery.Views.Pages;

/// <summary>Chips, badges, status pills and both progress forms.</summary>
public partial class ChipsPage : GalleryPage
{
    private readonly List<Control> _chips = [];
    private readonly List<Control> _chipKinds = [];

    public ChipsPage()
    {
        InitializeComponent();

        Removable(Chips, _chips);
        Removable(ChipKinds, _chipKinds);

        ShowValue();
    }

    /// <summary>Remembers a row of chips and wires each one to take itself out.</summary>
    private static void Removable(Panel row, List<Control> kept)
    {
        kept.AddRange(row.Children);

        foreach (var chip in kept.OfType<Chip>())
        {
            chip.RemoveCommand = new Run(() => row.Children.Remove(chip));
        }
    }

    private void OnRestoreChips(object? sender, RoutedEventArgs e)
    {
        Restore(Chips, _chips);
        Restore(ChipKinds, _chipKinds);
    }

    private static void Restore(Panel row, List<Control> kept)
    {
        row.Children.Clear();

        foreach (var chip in kept)
        {
            row.Children.Add(chip);
        }
    }

    private void OnLess(object? sender, RoutedEventArgs e) => Move(-10);

    private void OnMore(object? sender, RoutedEventArgs e) => Move(10);

    private void Move(double by)
    {
        Determinate.Value = Math.Clamp(Determinate.Value + by, Determinate.Minimum, Determinate.Maximum);
        ShowValue();
    }

    private void ShowValue() => DeterminateValue.Text = $"{Determinate.Value:0}%";

    /// <summary>A command that runs one action. The gallery has no view models.</summary>
    private sealed class Run(Action action) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => action();
    }
}
