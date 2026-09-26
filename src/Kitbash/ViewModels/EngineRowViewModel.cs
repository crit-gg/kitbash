using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Ui.Controls;

namespace Kitbash.ViewModels;

/// <summary>
/// One engine on the Installed tab.
/// </summary>
public sealed partial class EngineRowViewModel : ViewModelBase
{
    /// <summary>Roughly what fits a row's subtitle beside the size and the processor.</summary>
    private const int PathLength = 52;

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

        Title = DisplayName(engine.Id);
        Channel = engine.Repository is { } address
            ? page.RepositoryLabel(address).ToUpperInvariant()
            : ChannelOf(engine.Tag);
        IsDefault = theDefault == engine.Id;
        Path = paths.Shorten(engine.Directory, PathLength);

        List<string> parts =
        [
            Path,
            engine.IsMissing ? "missing" : Size(engine.SizeOnDisk),
            $"{page.HostPlatformText} {engine.ArchitectureText}",
        ];

        // A slot moves on its own, so the row says which pin it follows.
        if (engine.IsSlot)
        {
            parts.Add($"follows {engine.Record.Slot}");
        }

        if (page.IsAddingTemplates(engine))
        {
            parts.Add("adding templates");
        }
        else if (HasTemplates)
        {
            parts.Add("templates");
        }

        Parts = parts;
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

    /// <summary>Only what Kitbash installed is deleted. An imported engine is forgotten.</summary>
    public bool IsImported => Engine.IsImported;

    public string Path { get; }

    public IReadOnlyList<string> Parts { get; }

    /// <summary>
    /// Hidden on the row that already is the default, and on a repository build, whose name
    /// changes each time its slot moves and which a default cannot name.
    /// </summary>
    public bool CanSetDefault => !IsDefault && !Engine.IsMissing && Engine.Id.IsOfficial;

    public bool HasNotes => _page.NotesFor(Engine) is not null;

    /// <summary>True when Kitbash installed export templates for this engine.</summary>
    public bool HasTemplates => Engine.Record.Templates.Length > 0;

    /// <summary>Templates come from the release an install came from, so a found engine has none to offer.</summary>
    public bool CanAddTemplates =>
        !HasTemplates && !Engine.IsImported && !Engine.IsMissing && !_page.IsAddingTemplates(Engine);

    [RelayCommand]
    private Task AddTemplates()
    {
        IsMenuOpen = false;

        return _page.AddTemplatesAsync(Engine);
    }

    [RelayCommand]
    private Task RemoveTemplates()
    {
        IsMenuOpen = false;

        return _page.RemoveTemplatesAsync(Engine);
    }

    [RelayCommand]
    private void SetDefault()
    {
        IsMenuOpen = false;
        _page.SetDefault(Engine);
    }

    /// <summary>An engine whose folder has gone cannot be started.</summary>
    public bool CanOpen => !Engine.IsMissing;

    [RelayCommand]
    private void OpenProjectManager() => _page.OpenProjectManager(Engine);

    [RelayCommand]
    private Task Remove()
    {
        IsMenuOpen = false;

        return _page.RemoveAsync(Engine);
    }

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

        if (_page.NotesFor(Engine) is { } notes)
        {
            _page.OpenNotes(notes);
        }
    }

    /// <summary>
    /// What an install is called: the version and channel for an official build, such as
    /// <c>4.7.1 stable</c>, and the version and build for a repository one, such as
    /// <c>4.7.2 18d5d19</c>.
    /// </summary>
    public static string DisplayName(EngineId id) =>
        id.Repository is null ? NameOf(id.Tag) : $"{VersionOf(id.Tag)} {id.Build}";

    /// <summary>The version alone, such as <c>4.7.1</c>.</summary>
    public static string VersionOf(EngineTag tag) =>
        tag.Patch > 0 ? $"{tag.Major}.{tag.Minor}.{tag.Patch}" : $"{tag.Major}.{tag.Minor}";

    /// <summary>The version and its channel, such as <c>4.8 dev 2</c>.</summary>
    public static string NameOf(EngineTag tag) => tag.Channel switch
    {
        EngineChannel.Stable => $"{VersionOf(tag)} stable",
        EngineChannel.Custom => VersionOf(tag),
        _ => $"{VersionOf(tag)} {tag.ChannelText} {tag.Number}",
    };

    /// <summary>
    /// The channel for a pill, or empty for stable, which needs none. Empty for a repository
    /// build too, whose pill names the repository instead.
    /// </summary>
    public static string ChannelOf(EngineTag tag) =>
        tag.Channel is EngineChannel.Stable or EngineChannel.Custom
            ? string.Empty
            : tag.ChannelText.ToUpperInvariant();

    /// <summary>Bytes as a person reads them.</summary>
    public static string Size(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / 1024d / 1024 / 1024:0.0} GB",
        >= 1024 * 1024 => $"{bytes / 1024 / 1024} MB",
        _ => $"{bytes / 1024} KB",
    };
}
