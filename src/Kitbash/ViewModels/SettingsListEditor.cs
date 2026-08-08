using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Ui.Controls;

namespace Kitbash.ViewModels;

/// <summary>
/// The half of a settings list that is the same for every one: the grid it is drawn in,
/// and adding and removing a row. What a row holds and where it is kept belongs to the
/// editor deriving from this.
/// </summary>
public abstract partial class SettingsListEditor<TRow> : ObservableObject, ISettingsEditor
    where TRow : SettingsListRow
{
    [ObservableProperty]
    private bool _isPageWritable = true;

    [ObservableProperty]
    private TRow? _selected;

    protected SettingsListEditor()
    {
        // The grid takes a snapshot, so every change here has to say so.
        Grid = new GridRows(Rows);

        Rows.CollectionChanged += OnRowsChanged;
    }

    public ObservableCollection<TRow> Rows { get; } = [];

    /// <summary>Which rows are being listened to, so none is subscribed twice or leaked.</summary>
    private readonly HashSet<TRow> _heard = [];

    /// <summary>What the grid is given, the way a tree is given a TreeRows.</summary>
    public GridRows Grid { get; }

    public bool IsEmpty => Rows.Count == 0;

    /// <summary>A row has to be picked before it can be taken off the list.</summary>
    public bool CanRemove => Selected is not null && IsPageWritable;

    /// <summary>Unused unless the page layers. The window sets it either way.</summary>
    public virtual SettingsLayer? Layer { get; set; }

    public abstract bool IsDirty { get; }

    /// <summary>
    /// A row that says nothing usable stops the save rather than being dropped, since
    /// dropping it would throw away what a person typed without saying so.
    /// </summary>
    public virtual bool IsValid => Rows.All(row => row.IsValid);

    public event EventHandler? Changed;

    public abstract Task LoadAsync(CancellationToken token = default);

    public abstract Task SaveAsync(CancellationToken token = default);

    public abstract void Discard();

    /// <summary>A row with nothing in it, for the person who pressed Add.</summary>
    protected abstract TRow NewRow();

    [RelayCommand]
    private void Add()
    {
        var row = NewRow();

        Rows.Add(row);
        Announce();

        Selected = row;
    }

    [RelayCommand]
    private void Remove()
    {
        if (Selected is not { } row)
        {
            return;
        }

        Rows.Remove(row);
        Selected = null;
        Announce();
    }

    /// <summary>Rebuilds the grid and tells the page. Call after a row is added or removed.</summary>
    protected void Announce()
    {
        Grid.Refresh();

        OnPropertyChanged(nameof(IsEmpty));
        Notify();
    }

    /// <summary>
    /// Tells the page there may be something to save. Separate from <see cref="Announce"/>
    /// because a field changing must not rebuild the grid under the cell being typed in.
    /// </summary>
    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// A row's own fields are what a person edits, so the page hears about them through
    /// here. Without it the unsaved count never moves once a cell is left.
    /// </summary>
    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Clear raises a reset and names no old items, so the whole set is dropped and
        // built again rather than trusted to say what went.
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var row in _heard)
            {
                row.PropertyChanged -= OnRowChanged;
            }

            _heard.Clear();
        }

        foreach (var row in e.OldItems?.OfType<TRow>() ?? [])
        {
            row.PropertyChanged -= OnRowChanged;
            _heard.Remove(row);
        }

        foreach (var row in Rows.Where(row => _heard.Add(row)))
        {
            row.PropertyChanged += OnRowChanged;
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e) => Notify();

    partial void OnSelectedChanged(TRow? value) => OnPropertyChanged(nameof(CanRemove));

    partial void OnIsPageWritableChanged(bool value) => OnPropertyChanged(nameof(CanRemove));
}
