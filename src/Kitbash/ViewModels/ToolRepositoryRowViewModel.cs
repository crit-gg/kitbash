using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Core.Platform;
using Kitbash.Tools;

namespace Kitbash.ViewModels;

/// <summary>One repository in the settings window, while it is being edited.</summary>
public sealed partial class ToolRepositoryRowViewModel : SettingsListRow
{
    private (string Kind, string Address)? _before;

    [ObservableProperty]
    private string _kind;

    [ObservableProperty]
    private string _address;

    /// <param name="kind">
    /// The type as the file spells it. A type Kitbash does not know is still offered, so
    /// a file naming a forge this copy cannot read survives being saved.
    /// </param>
    public ToolRepositoryRowViewModel(string kind, string address)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(address);

        _kind = kind;
        _address = address;

        Kinds = kind.Length > 0 && !ToolRepositorySource.Kinds.Contains(kind, StringComparer.Ordinal)
            ? [.. ToolRepositorySource.Kinds, kind]
            : ToolRepositorySource.Kinds;
    }

    /// <summary>What the type dropdown offers, which is every kind plus this row's own.</summary>
    public IReadOnlyList<string> Kinds { get; }

    /// <summary>The entry this row would be written as, or null while it says nothing usable.</summary>
    public ToolRepositorySource? Source
    {
        get
        {
            if (Kind.Length == 0 || string.IsNullOrWhiteSpace(Address))
            {
                return null;
            }

            try
            {
                return new ToolRepositorySource(Kind, WebAddress.Parse(Address.Trim()), string.Empty);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return null;
            }
        }
    }

    /// <summary>Said under the field, so a person is told why the row cannot be kept.</summary>
    public override string Problem
    {
        get
        {
            if (Kind.Length == 0)
            {
                return "Say where this repository is.";
            }

            if (string.IsNullOrWhiteSpace(Address))
            {
                return "Give this repository an address.";
            }

            return Source is null ? "This is not a web address. Use http or https." : string.Empty;
        }
    }

    public override void BeginEdit() => _before = (Kind, Address);

    public override void CancelEdit()
    {
        if (_before is not { } before)
        {
            return;
        }

        Kind = before.Kind;
        Address = before.Address;
        _before = null;
    }

    public override void EndEdit() => _before = null;

    partial void OnKindChanged(string value) => Announce();

    partial void OnAddressChanged(string value) => Announce();
}
