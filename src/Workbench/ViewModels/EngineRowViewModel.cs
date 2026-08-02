using System.Globalization;
using Workbench.Core.Godot;
using Workbench.Core.IO;

namespace Workbench.ViewModels;

/// <summary>
/// One row on the engines page. The same shape serves both tabs, since a release and an
/// install read the same way: a title, the pills for what the title does not say, and a
/// mono line under it.
/// </summary>
public sealed class EngineRowViewModel
{
    private EngineRowViewModel(string title, string channel, bool isMono, bool isDefault, string subtitle, string mark)
    {
        Title = title;
        Channel = channel;
        IsMono = isMono;
        IsDefault = isDefault;
        Subtitle = subtitle;
        Mark = mark;
    }

    public string Title { get; }

    /// <summary>The channel, upper case, or empty for stable, which needs no pill.</summary>
    public string Channel { get; }

    public bool IsMono { get; }

    public bool IsDefault { get; }

    public string Subtitle { get; }

    /// <summary>What the right of the row reports. Empty draws nothing.</summary>
    public string Mark { get; }

    public bool HasChannel => Channel.Length > 0;

    public bool HasMark => Mark.Length > 0;

    /// <summary>A release, with a note saying how much of it is already here.</summary>
    public static EngineRowViewModel For(EngineRelease release, IReadOnlyList<InstalledEngine> installed)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(installed);

        var here = installed.Count(engine => engine.Tag == release.Tag);

        return new EngineRowViewModel(
            NameOf(release.Tag),
            ChannelOf(release.Tag),
            isMono: false,
            isDefault: false,
            $"released {release.Released.ToString("d", CultureInfo.CurrentCulture)}",
            here switch
            {
                0 => string.Empty,
                1 => "1 build installed",
                _ => $"{here} builds installed",
            });
    }

    /// <summary>An install, with what the disk says about it.</summary>
    public static EngineRowViewModel For(InstalledEngine engine, EngineId? theDefault, IPathShortener paths)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(paths);

        // The processor earns its place here, since other architectures are installable and
        // this row is the only thing that says which one was taken.
        var subtitle = engine.IsMissing
            ? $"{paths.Shorten(engine.Directory, 52)}   missing"
            : $"{paths.Shorten(engine.Directory, 52)}   {engine.ArchitectureText}";

        return new EngineRowViewModel(
            NameOf(engine.Tag),
            ChannelOf(engine.Tag),
            engine.IsMono,
            theDefault == engine.Id,
            subtitle,
            engine.IsImported ? "imported" : string.Empty);
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
}
