using Kitbash.Core.Godot;
using Kitbash.Ui.Controls;

namespace Kitbash.ViewModels;

/// <summary>
/// One engine in the new workspace dialog's list. A row is a build rather than a release,
/// so a version installed both ways is two rows and the .NET one pins the .NET one.
/// </summary>
public sealed class EngineChoiceViewModel
{
    public EngineChoiceViewModel(EngineId id, bool isInstalled)
    {
        Id = id;
        IsInstalled = isInstalled;
        Title = EngineRowViewModel.NameOf(id.Tag);
        Channel = EngineRowViewModel.ChannelOf(id.Tag);
    }

    /// <summary>The build this row is, which is what the workspace is pinned to.</summary>
    public EngineId Id { get; }

    public EngineTag Tag => Id.Tag;

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

    public bool IsMono => Id.IsMono;

    public bool IsInstalled { get; }

    public bool IsNotInstalled => !IsInstalled;
}
