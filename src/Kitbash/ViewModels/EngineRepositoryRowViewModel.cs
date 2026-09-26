using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Core.Godot;
using Kitbash.Core.Platform;

namespace Kitbash.ViewModels;

/// <summary>One engine repository in the settings window, while it is being edited.</summary>
public sealed partial class EngineRepositoryRowViewModel : SettingsListRow
{
    private (string Name, string Address)? _before;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _address;

    public EngineRepositoryRowViewModel(string name, string address)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(address);

        _name = name;
        _address = address;
    }

    /// <summary>The entry this row would be written as, or null while it says nothing usable.</summary>
    public EngineRepositorySource? Source
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Address))
            {
                return null;
            }

            try
            {
                var url = WebAddress.Parse(Address.Trim());

                return EngineRepositoryAddress.ForGitHub(url) is { } address
                    ? new EngineRepositorySource(Name.Trim(), address, url, string.Empty)
                    : null;
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return null;
            }
        }
    }

    public override string Problem
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return "Give this repository a name a workspace can use.";
            }

            if (string.IsNullOrWhiteSpace(Address))
            {
                return "Give this repository an address.";
            }

            return Source is null ? "Use the address of a repository on github.com." : string.Empty;
        }
    }

    public override void BeginEdit() => _before = (Name, Address);

    public override void CancelEdit()
    {
        if (_before is not { } before)
        {
            return;
        }

        Name = before.Name;
        Address = before.Address;
        _before = null;
    }

    public override void EndEdit() => _before = null;

    partial void OnNameChanged(string value) => Announce();

    partial void OnAddressChanged(string value) => Announce();
}
