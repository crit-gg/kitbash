using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Core.Platform;

namespace Kitbash.ViewModels;

/// <summary>
/// A <c>kitbash://git/patch</c> link being made for one workspace, from the address a
/// patch can be downloaded from.
/// </summary>
public partial class CopyPatchLinkViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _address = string.Empty;

    /// <param name="workspace">The launcher's own name for it, which is only for display.</param>
    /// <param name="linkName">What a link calls it, or null when nothing can.</param>
    public CopyPatchLinkViewModel(string workspace, string? linkName)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        Workspace = workspace;
        LinkName = linkName ?? string.Empty;
    }

    public string Workspace { get; }

    public string LinkName { get; }

    public bool HasLinkName => LinkName.Length > 0;

    /// <summary>True when the name is the repository's first commit, since nothing else names it.</summary>
    public bool IsNamedByCommit => LinkName.Length is 40 or 64 && LinkName.All(char.IsAsciiHexDigit);

    /// <summary>What is wrong with the address, or empty while it is good or blank.</summary>
    public string AddressProblem => Address.Trim().Length > 0 && Source is null ? "Use an https address." : string.Empty;

    public bool HasAddressProblem => AddressProblem.Length > 0;

    /// <summary>The link, or empty until there is an address and a name to put in it.</summary>
    public string Link => Source is { } source && HasLinkName
        ? DeepLink.Write("git", ["patch"], [new("url", source.ToString()), new("workspace", LinkName)])
        : string.Empty;

    public bool HasLink => Link.Length > 0;

    public bool CanCopy => HasLink;

    private WebAddress? Source =>
        Uri.TryCreate(Address.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? WebAddress.Parse(uri)
            : null;

    partial void OnAddressChanged(string value)
    {
        OnPropertyChanged(nameof(AddressProblem));
        OnPropertyChanged(nameof(HasAddressProblem));
        OnPropertyChanged(nameof(Link));
        OnPropertyChanged(nameof(HasLink));
        OnPropertyChanged(nameof(CanCopy));
    }
}
