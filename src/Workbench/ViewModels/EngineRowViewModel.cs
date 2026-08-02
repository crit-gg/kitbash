using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Workbench.Core.Godot;
using Workbench.Core.IO;
using Workbench.Ui.Controls;

namespace Workbench.ViewModels;

/// <summary>
/// One engine on the Installed tab.
/// </summary>
/// <remarks>
/// The subtitle is parts rather than a joined string, since the design spaces them and a
/// string cannot be spaced. The processor earns its place among them: other architectures
/// are installable, so this row is the only thing that says which one was taken.
/// </remarks>
public sealed partial class EngineRowViewModel : ViewModelBase
{
    private readonly EnginesViewModel _page;

    [ObservableProperty]
    private bool _isMenuOpen;

    public EngineRowViewModel(
        InstalledEngine engine,
        EngineId? theDefault,
        IPathShortener paths,
        EnginesViewModel page)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(page);

        Engine = engine;
        _page = page;

        Title = NameOf(engine.Tag);
        Channel = ChannelOf(engine.Tag);
        IsDefault = theDefault == engine.Id;
        Path = paths.Shorten(engine.Directory, 52);

        Parts =
        [
            Path,
            engine.IsMissing ? "missing" : Size(engine.SizeOnDisk),
            $"{page.HostPlatformText} {engine.ArchitectureText}",
        ];
    }

    public InstalledEngine Engine { get; }

    public string Title { get; }

    public string Channel { get; }

    public bool HasChannel => Channel.Length > 0;

    public BadgeTier ChannelTier => Engine.Tag.Channel switch
    {
        EngineChannel.Rc => BadgeTier.Modified,
        EngineChannel.Beta => BadgeTier.Graph,
        EngineChannel.Alpha => BadgeTier.Error,
        _ => BadgeTier.Neutral,
    };

    public bool IsDefault { get; }

    public bool IsMono => Engine.IsMono;

    /// <summary>Only what Workbench installed is deleted. An imported engine is forgotten.</summary>
    public bool IsImported => Engine.IsImported;

    public string Path { get; }

    public IReadOnlyList<string> Parts { get; }

    /// <summary>Hidden on the row that already is the default, which is the design's rule.</summary>
    public bool CanSetDefault => !IsDefault && !Engine.IsMissing;

    public bool HasNotes => _page.NotesFor(Engine.Tag) is not null;

    [RelayCommand]
    private void SetDefault() => _page.SetDefault(Engine);

    [RelayCommand]
    private Task Remove() => _page.RemoveAsync(Engine);

    [RelayCommand]
    private void ToggleMenu() => IsMenuOpen = !IsMenuOpen;

    [RelayCommand]
    private void OpenFolder()
    {
        IsMenuOpen = false;
        _page.OpenFolder(Engine.Directory);
    }

    [RelayCommand]
    private Task CopyPath()
    {
        IsMenuOpen = false;

        return _page.CopyAsync(Engine.Directory);
    }

    [RelayCommand]
    private void ReleaseNotes()
    {
        IsMenuOpen = false;

        if (_page.NotesFor(Engine.Tag) is { } notes)
        {
            _page.OpenNotes(notes);
        }
    }

    /// <summary>The version alone, such as <c>4.7.1</c>.</summary>
    /// <remarks>
    /// How a tag reads on screen lives here rather than on <see cref="EngineTag"/>, which
    /// spells a tag the way Godot's own files do and should keep doing so.
    /// </remarks>
    public static string VersionOf(EngineTag tag) =>
        tag.Patch > 0 ? $"{tag.Major}.{tag.Minor}.{tag.Patch}" : $"{tag.Major}.{tag.Minor}";

    /// <summary>The version and its channel, such as <c>4.8 dev 2</c>.</summary>
    public static string NameOf(EngineTag tag) =>
        tag.Channel == EngineChannel.Stable
            ? $"{VersionOf(tag)} stable"
            : $"{VersionOf(tag)} {tag.ChannelText} {tag.Number}";

    /// <summary>The channel for a pill, or empty for stable, which needs none.</summary>
    public static string ChannelOf(EngineTag tag) =>
        tag.Channel == EngineChannel.Stable ? string.Empty : tag.ChannelText.ToUpperInvariant();

    /// <summary>Bytes as a person reads them.</summary>
    public static string Size(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / 1024d / 1024 / 1024:0.0} GB",
        >= 1024 * 1024 => $"{bytes / 1024 / 1024} MB",
        _ => $"{bytes / 1024} KB",
    };
}
