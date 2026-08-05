using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Core.Platform.Openers;
using Kitbash.Core.Settings.Schema;

namespace Kitbash.ViewModels;

/// <summary>
/// The tools a person added, edited in the settings window. It is its own editor because
/// <c>tools.custom</c> is an array of tables and no descriptor can describe one.
/// </summary>
public sealed partial class CustomToolsEditor : ObservableObject, ISettingsEditor
{
    private readonly ICustomOpeners _openers;

    /// <summary>What the file said when it was last read. Dirty is measured against it.</summary>
    private IReadOnlyList<CustomOpener> _stored = [];

    [ObservableProperty]
    private bool _isPageWritable = true;

    public CustomToolsEditor(ICustomOpeners openers)
    {
        ArgumentNullException.ThrowIfNull(openers);
        _openers = openers;
    }

    public ObservableCollection<CustomToolRowViewModel> Rows { get; } = [];

    public bool IsEmpty => Rows.Count == 0;

    public bool IsDirty
    {
        get
        {
            if (Rows.Count != _stored.Count)
            {
                return true;
            }

            for (var index = 0; index < Rows.Count; index++)
            {
                if (Rows[index].Opener != _stored[index])
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// A row that says nothing usable stops the save rather than being dropped, since
    /// dropping it would throw away what a person typed without saying so.
    /// </summary>
    public bool IsValid => Rows.All(row => row.IsValid);

    public event EventHandler? Changed;

    public async Task LoadAsync(CancellationToken token = default)
    {
        _stored = await Task.Run(_openers.Read, token).ConfigureAwait(true);
        Fill();
    }

    public async Task SaveAsync(CancellationToken token = default)
    {
        var writing = Rows.Select(row => row.Opener).OfType<CustomOpener>().ToArray();

        await Task.Run(() => _openers.Write(writing), token).ConfigureAwait(true);
    }

    public void Discard() => Fill();

    [RelayCommand]
    private void Add()
    {
        Rows.Add(Row(new CustomOpener(string.Empty, string.Empty, string.Empty)));
        Announce();
    }

    partial void OnIsPageWritableChanged(bool value)
    {
        foreach (var row in Rows)
        {
            row.CanEdit = value;
        }

        Announce();
    }

    /// <summary>Puts the rows back to what was read, which is both load and discard.</summary>
    private void Fill()
    {
        Rows.Clear();

        foreach (var opener in _stored)
        {
            Rows.Add(Row(opener));
        }

        Announce();
    }

    private CustomToolRowViewModel Row(CustomOpener opener) =>
        new(opener.Name, opener.Path, opener.Arguments, Announce, Remove) { CanEdit = IsPageWritable };

    private void Remove(CustomToolRowViewModel row)
    {
        Rows.Remove(row);
        Announce();
    }

    private void Announce()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsValid));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
