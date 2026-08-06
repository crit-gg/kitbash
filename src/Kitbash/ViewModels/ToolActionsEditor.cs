using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Core.Settings.Schema;
using Kitbash.Settings;
using Kitbash.Tools;

namespace Kitbash.ViewModels;

/// <summary>
/// What each installed tool does to the launcher, edited in the settings window. It is its
/// own editor because <c>launcher.after.tools</c> is an array of tables and no descriptor
/// can describe one.
/// </summary>
public sealed partial class ToolActionsEditor : ObservableObject, ISettingsEditor
{
    private readonly IInstalledTools _tools;
    private readonly IAfterLaunchOverrides _overrides;

    /// <summary>What the file said when it was last read. Dirty is measured against it.</summary>
    private IReadOnlyList<AfterLaunchOverride> _stored = [];

    [ObservableProperty]
    private bool _isPageWritable = true;

    public ToolActionsEditor(IInstalledTools tools, IAfterLaunchOverrides overrides)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(overrides);

        _tools = tools;
        _overrides = overrides;
    }

    /// <summary>One row per installed tool. A tool that is not here has no row.</summary>
    public ObservableCollection<ToolActionRowViewModel> Rows { get; } = [];

    public bool IsEmpty => Rows.Count == 0;

    public bool IsDirty
    {
        get
        {
            var staged = Staged();

            return staged.Count != _stored.Count
                || staged.Where((chosen, index) => chosen != _stored[index]).Any();
        }
    }

    /// <summary>Every row holds one of four options, so there is nothing to refuse.</summary>
    public bool IsValid => true;

    public event EventHandler? Changed;

    public async Task LoadAsync(CancellationToken token = default)
    {
        // Both touch a disk. The tools are folders under the state directory and the
        // overrides are the settings file read again.
        var (installed, stored) = await Task.Run(
            () => (_tools.Read(), _overrides.Read()),
            token).ConfigureAwait(true);

        _stored = stored;
        Fill(installed);
    }

    public async Task SaveAsync(CancellationToken token = default)
    {
        var writing = Staged();

        await Task.Run(() => _overrides.Write(writing), token).ConfigureAwait(true);
    }

    public void Discard()
    {
        foreach (var row in Rows)
        {
            row.Action = Stored(row.Id);
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
    /// What a save would write. Stored rows keep their place so a file that did not change
    /// comes back as it was, and a tool that is not installed keeps its answer for the day
    /// it comes back.
    /// </summary>
    private List<AfterLaunchOverride> Staged()
    {
        List<AfterLaunchOverride> staged = [];

        foreach (var chosen in _stored)
        {
            if (Rows.FirstOrDefault(row => row.Id == chosen.Id) is not { } row)
            {
                staged.Add(chosen);
                continue;
            }

            if (row.Action is { } action)
            {
                staged.Add(new AfterLaunchOverride(chosen.Id, action));
            }
        }

        foreach (var row in Rows)
        {
            if (row.Action is { } action && !_stored.Any(chosen => chosen.Id == row.Id))
            {
                staged.Add(new AfterLaunchOverride(row.Id, action));
            }
        }

        return staged;
    }

    private void Fill(IReadOnlyList<InstalledTool> installed)
    {
        Rows.Clear();

        foreach (var tool in installed.OrderBy(tool => tool.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Rows.Add(new ToolActionRowViewModel(tool.Id, tool.Name, Stored(tool.Id), Announce)
            {
                CanEdit = IsPageWritable,
            });
        }

        Announce();
    }

    private AfterLaunchAction? Stored(ToolId id) =>
        _stored.FirstOrDefault(chosen => chosen.Id == id)?.Action;

    private void Announce()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsValid));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
