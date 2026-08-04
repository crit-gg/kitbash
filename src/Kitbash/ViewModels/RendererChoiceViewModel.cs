using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Core.Godot;

namespace Kitbash.ViewModels;

/// <summary>
/// One renderer row. The row reads which radio is checked, so the option and its
/// container answer each other rather than the list being told twice.
/// </summary>
public sealed partial class RendererChoiceViewModel : ObservableObject
{
    private readonly Action<GodotRenderer> _pick;

    [ObservableProperty]
    private bool _isChosen;

    public RendererChoiceViewModel(
        GodotRenderer renderer, string name, string description, Action<GodotRenderer> pick)
    {
        ArgumentNullException.ThrowIfNull(pick);

        Renderer = renderer;
        Name = name;
        Description = description;
        _pick = pick;
    }

    public GodotRenderer Renderer { get; }

    public string Name { get; }

    public string Description { get; }

    partial void OnIsChosenChanged(bool value)
    {
        if (value)
        {
            _pick(Renderer);
        }
    }
}
