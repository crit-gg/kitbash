using Avalonia.Media;

namespace Workbench.ViewModels;

/// <summary>One count in the git strip, such as 12 modified.</summary>
public sealed class GitStatViewModel
{
    public GitStatViewModel(string value, string label, string color)
    {
        Value = value;
        Label = label;
        Color = SolidColorBrush.Parse(color);
    }

    public string Value { get; }

    public string Label { get; }

    public IBrush Color { get; }
}
