using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Tools;

namespace Kitbash.ViewModels;

/// <summary>
/// The global repository list, edited in the settings window. It is its own editor
/// because <c>tools.repositories</c> is an array of tables and no descriptor can
/// describe one. A workspace's own list is read elsewhere and never written here.
/// </summary>
public sealed partial class ToolRepositoriesEditor : ObservableObject, ISettingsEditor
{
    private readonly IToolRepositoryList _repositories;

    /// <summary>What the file said when it was last read. Dirty is measured against it.</summary>
    private IReadOnlyList<(string Kind, string Address)> _stored = [];

    [ObservableProperty]
    private bool _isPageWritable = true;

    public ToolRepositoriesEditor(IToolRepositoryList repositories)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        _repositories = repositories;
    }

    public ObservableCollection<ToolRepositoryRowViewModel> Rows { get; } = [];

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
                if (!string.Equals(Rows[index].Kind, _stored[index].Kind, StringComparison.Ordinal)
                    || !string.Equals(Rows[index].Address, _stored[index].Address, StringComparison.Ordinal))
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

    /// <summary>Unused. This page is the application's own file, which has no layers.</summary>
    public SettingsLayer? Layer { get; set; }


    public async Task LoadAsync(CancellationToken token = default)
    {
        var stored = await Task.Run(_repositories.ReadGlobal, token).ConfigureAwait(true);

        _stored = [.. stored.Select(repository => (repository.Type, repository.Url.ToString()))];
        Fill();
    }

    public async Task SaveAsync(CancellationToken token = default)
    {
        var writing = Rows.Select(row => row.Source).OfType<ToolRepositorySource>().ToArray();

        await Task.Run(() => _repositories.WriteGlobal(writing), token).ConfigureAwait(true);
    }

    public void Discard() => Fill();

    [RelayCommand]
    private void Add()
    {
        Rows.Add(Row(ToolRepositorySource.GitHub, string.Empty));
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

        foreach (var (kind, address) in _stored)
        {
            Rows.Add(Row(kind, address));
        }

        Announce();
    }

    private ToolRepositoryRowViewModel Row(string kind, string address) =>
        new(kind, address, Announce, Remove) { CanEdit = IsPageWritable };

    private void Remove(ToolRepositoryRowViewModel row)
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
