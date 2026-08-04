using Kitbash.Core.Godot;
using Kitbash.Ui.Controls;

namespace Kitbash.ViewModels;

/// <summary>
/// One version in the new workspace dialog's engine list. A row is a release rather than
/// an install, so a release with any build of it on this machine reads as installed.
/// </summary>
public sealed class EngineChoiceViewModel
{
    public EngineChoiceViewModel(EngineTag tag, bool isInstalled)
    {
        Tag = tag;
        IsInstalled = isInstalled;
        Title = EngineRowViewModel.NameOf(tag);
        Channel = EngineRowViewModel.ChannelOf(tag);
    }

    public EngineTag Tag { get; }

    public string Title { get; }

    /// <summary>Empty for a stable release, which is drawn without a badge.</summary>
    public string Channel { get; }

    public bool HasChannel => Channel.Length > 0;

    public BadgeTier ChannelTier => Tag.Channel switch
    {
        EngineChannel.Rc => BadgeTier.Modified,
        EngineChannel.Beta => BadgeTier.Graph,
        EngineChannel.Alpha => BadgeTier.Error,
        _ => BadgeTier.Neutral,
    };

    public bool IsInstalled { get; }

    public bool IsNotInstalled => !IsInstalled;
}
