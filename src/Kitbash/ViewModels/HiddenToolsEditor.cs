using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Core.Platform.Openers;
using Kitbash.Core.Settings.Schema;

namespace Kitbash.ViewModels;

/// <summary>
/// Which of the tools Kitbash found are offered by the Open in button. It is its own editor
/// because <c>tools.hidden</c> is a list of ids and no descriptor can describe one.
/// </summary>
public sealed partial class HiddenToolsEditor : ObservableObject, ISettingsEditor
{
    private readonly IWorkspaceOpeners _openers;
    private readonly IHiddenOpeners _hidden;

    /// <summary>What the file said when it was last read. Dirty is measured against it.</summary>
    private IReadOnlyList<string> _stored = [];

    [ObservableProperty]
    private bool _isPageWritable = true;

    public HiddenToolsEditor(IWorkspaceOpeners openers, IHiddenOpeners hidden)
    {
        ArgumentNullException.ThrowIfNull(openers);
        ArgumentNullException.ThrowIfNull(hidden);

        _openers = openers;
        _hidden = hidden;
    }

    /// <summary>One row per detected tool. A tool a person added is not here.</summary>
    public ObservableCollection<HiddenToolRowViewModel> Rows { get; } = [];

    public bool IsEmpty => Rows.Count == 0;

    public bool IsDirty
    {
        get
        {
            var staged = Staged();

            return staged.Count != _stored.Count
                || staged.Where((id, index) => !string.Equals(id, _stored[index], StringComparison.Ordinal)).Any();
        }
    }

    /// <summary>A row is on or off, so there is nothing to refuse.</summary>
    public bool IsValid => true;

    public event EventHandler? Changed;

    public async Task LoadAsync(CancellationToken token = default)
    {
        _stored = await Task.Run(_hidden.Read, token).ConfigureAwait(true);

        // Detection is held after the first read, so only the first load does any work.
        Fill(await _openers.ReadAllAsync(token).ConfigureAwait(true));
    }

    public async Task SaveAsync(CancellationToken token = default)
    {
        var writing = Staged();

        await Task.Run(() => _hidden.Write(writing), token).ConfigureAwait(true);
    }

    public void Discard()
    {
        foreach (var row in Rows)
        {
            row.IsOffered = !_stored.Contains(row.Id, StringComparer.Ordinal);
        }

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

    /// <summary>
    /// What a save would write. A stored id keeps its place, and one nothing on this machine
    /// answers to is kept, so hiding a tool survives uninstalling and reinstalling it.
    /// </summary>
    private List<string> Staged()
    {
        List<string> staged =
        [
            .. _stored.Where(id =>
                Rows.FirstOrDefault(row => string.Equals(row.Id, id, StringComparison.Ordinal))
                    is not { IsOffered: true }),
        ];

        staged.AddRange(Rows
            .Where(row => !row.IsOffered && !_stored.Contains(row.Id, StringComparer.Ordinal))
            .Select(row => row.Id));

        return staged;
    }

    private void Fill(IReadOnlyList<WorkspaceOpener> openers)
    {
        Rows.Clear();

        foreach (var opener in openers
            .Where(opener => opener.Kind is not WorkspaceOpenerKind.Custom)
            .OrderBy(opener => opener.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Rows.Add(new HiddenToolRowViewModel(
                opener.Id,
                opener.Name,
                opener.Program,
                !_stored.Contains(opener.Id, StringComparer.Ordinal),
                Announce)
            {
                CanEdit = IsPageWritable,
            });
        }

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
