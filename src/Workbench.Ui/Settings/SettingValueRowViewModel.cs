using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Workbench.Core.Settings;
using Workbench.Core.Settings.Schema;

namespace Workbench.Ui.Settings;

/// <summary>
/// One setting as a page draws it. It holds what is on disk, what a person has typed
/// over it, and which of the two is being shown.
/// </summary>
public sealed partial class SettingValueRowViewModel : SettingsRowViewModel
{
    private readonly ISettingDescriptor _descriptor;
    private readonly ISettingsValueConverter _converter;
    private readonly Action _changed;

    [ObservableProperty]
    private bool _isPageWritable = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateLabel))]
    private bool _boolValue;

    [ObservableProperty]
    private string _textValue = string.Empty;

    [ObservableProperty]
    private decimal? _numberValue;

    [ObservableProperty]
    private IReadOnlyList<SettingsListEntry> _lines = [];

    private SettingValueView? _view;
    private SettingOptionViewModel? _chosen;

    /// <summary>The display is being written from the model, so nothing it does is an edit.</summary>
    private bool _filling;

    private bool _staged;
    private bool _isReset;
    private object? _stagedRaw;
    private object? _stagedValue;
    private string? _stagedProblem;

    /// <param name="changed">Told whenever this row gains or loses an unsaved change.</param>
    public SettingValueRowViewModel(
        ISettingDescriptor descriptor,
        ISettingsValueConverter converter,
        Action changed)
        : base(descriptor)
    {
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(changed);

        _descriptor = descriptor;
        _converter = converter;
        _changed = changed;

        Editor = descriptor.Editor;
        Bound(descriptor, out var minimum, out var maximum);
        Minimum = minimum;
        Maximum = maximum;
    }

    public string Key => _descriptor.Key;

    public SettingEditor Editor { get; }

    /// <summary>Keeps one row's options from grouping with the next row's.</summary>
    public string GroupName => $"setting:{_descriptor.Key}";

    public ObservableCollection<SettingOptionViewModel> Options { get; } = [];

    public string? Unit => _descriptor.Unit;

    public bool HasUnit => !string.IsNullOrWhiteSpace(_descriptor.Unit);

    /// <summary>What a toggle reads back, since the track alone does not say which way it is.</summary>
    public string StateLabel => BoolValue ? "true" : "false";

    public decimal Minimum { get; }

    public decimal Maximum { get; }

    public bool IsToggle => Editor is SettingEditor.Toggle;

    public bool IsSelect => Editor is SettingEditor.Select;

    public bool IsSegment => Editor is SettingEditor.Segment;

    public bool IsNumber => Editor is SettingEditor.Number;

    public bool IsText => Editor is SettingEditor.Text;

    public bool IsList => Editor is SettingEditor.List;

    /// <summary>A list is what a file holds, drawn. There is no editor for one yet.</summary>
    public bool IsEditable => IsPageWritable && !_descriptor.IsReadOnly && !IsList;

    /// <summary>Nothing running rereads this, so a change to it waits for a fresh start.</summary>
    public bool NeedsRestart => _descriptor.NeedsRestart;

    /// <summary>There is an unsaved change here and it is one that waits for a restart.</summary>
    public bool IsRestartStaged => _staged && NeedsRestart;

    public SettingOptionViewModel? ChosenOption
    {
        get => _chosen;
        set => Choose(value);
    }

    public bool IsDirty => _staged;

    public bool IsValid => !_staged || _stagedProblem is null;

    public bool CanDiscard => _staged;

    /// <summary>
    /// Reset takes the key out of the file, so there has to be a key somewhere to take.
    /// </summary>
    public bool CanReset => !_staged && IsEditable && (_view?.CanReset ?? false);

    /// <summary>
    /// One slot, two causes. A rule or conversion failure means the value did not
    /// survive. Anything else here is what the file holds and could not be used.
    /// </summary>
    public string? Message => _staged ? _stagedProblem : _view?.Problem;

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public bool IsPending => _staged;

    public bool OriginIsUser =>
        !_staged && _view?.Origin is SettingOrigin.User or SettingOrigin.File;

    public bool OriginIsTeam => !_staged && _view?.Origin is SettingOrigin.TeamShared;

    public bool OriginIsInvalid =>
        _staged ? _stagedProblem is not null : _view?.Origin is SettingOrigin.Invalid;

    public bool ShowOrigin => IsPending || OriginIsInvalid || OriginIsUser || OriginIsTeam;

    public string OriginTip => (_staged, _view?.Origin) switch
    {
        (true, _) when _stagedProblem is not null => "Unsaved change, and it cannot be saved",
        (true, _) => "Unsaved change",
        (_, SettingOrigin.User) => "Set in your personal file",
        (_, SettingOrigin.TeamShared) => "Set in the team file",
        (_, SettingOrigin.File) => "Set in the file",
        (_, SettingOrigin.Invalid) => "A file holds a value here that could not be used",
        _ => string.Empty,
    };

    /// <summary>Takes on what the inspector read, keeping any unsaved change over the top.</summary>
    public void Apply(SettingValueView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        _view = view;
        Fill();
        Announce();
    }

    /// <summary>The options this setting offers, discovered off the UI thread.</summary>
    public void Offer(IReadOnlyList<SettingChoice> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);

        foreach (var option in Options)
        {
            option.PropertyChanged -= OnOptionChanged;
        }

        Options.Clear();

        foreach (var choice in choices)
        {
            Add(new SettingOptionViewModel(choice.Value, choice.Label, choice.Description));
        }

        Fill();
    }

    /// <summary>Throws the unsaved change away, back to whatever the files say.</summary>
    [RelayCommand]
    public void Discard()
    {
        if (!_staged)
        {
            return;
        }

        _staged = false;
        _isReset = false;
        _stagedRaw = null;
        _stagedValue = null;
        _stagedProblem = null;

        Fill();
        Announce();
        _changed();
    }

    /// <summary>
    /// Stages a removal. Reset takes the key out rather than writing today's default,
    /// which would pin it and opt this person out of every future change to it.
    /// </summary>
    [RelayCommand]
    public void Reset()
    {
        if (!CanReset)
        {
            return;
        }

        _staged = true;
        _isReset = true;
        _stagedRaw = _descriptor.Default;
        _stagedValue = null;
        _stagedProblem = null;

        Fill();
        Announce();
        _changed();
    }

    /// <summary>What this row would write, or null when it has nothing to say.</summary>
    public SettingsEdit? Edit()
    {
        if (!_staged || _stagedProblem is not null)
        {
            return null;
        }

        return _isReset || _stagedValue is null
            ? SettingsEdit.Remove(_descriptor.Key)
            : SettingsEdit.Set(_descriptor.Key, _stagedValue);
    }

    partial void OnBoolValueChanged(bool value) => Stage(value);

    partial void OnTextValueChanged(string value) => Stage(value);

    partial void OnNumberValueChanged(decimal? value) => Stage(value);

    partial void OnIsPageWritableChanged(bool value) => Announce();

    private void Choose(SettingOptionViewModel? option)
    {
        if (ReferenceEquals(_chosen, option))
        {
            return;
        }

        _chosen = option;
        OnPropertyChanged(nameof(ChosenOption));
        Mark();

        if (option is not null)
        {
            Stage(option.Value);
        }
    }

    /// <summary>
    /// Judges what the editor handed back and keeps it either way. An unsaved change that
    /// cannot be saved is still what a person typed, and taking it away as they type would
    /// be worse than refusing to save it.
    /// </summary>
    private void Stage(object? raw)
    {
        if (_filling || raw is null)
        {
            return;
        }

        var check = _descriptor.Check(raw, _converter, out var typed);

        // A row whose file holds a value that could not be used shows the fallback, so
        // picking that same fallback has to stage a write that replaces the bad value.
        var same = check.IsUsable
            && _view is { Problem: null }
            && Equals(typed, _view.Effective);

        if (same)
        {
            Discard();
            return;
        }

        _staged = true;
        _isReset = false;
        _stagedRaw = raw;
        _stagedValue = typed;
        _stagedProblem = check.Message;

        Announce();
        _changed();
    }

    /// <summary>Writes the editors from the model without any of it counting as an edit.</summary>
    private void Fill()
    {
        _filling = true;

        try
        {
            var value = _staged ? _stagedRaw : _view?.Effective;

            switch (Editor)
            {
                case SettingEditor.Toggle:
                    BoolValue = value is true;
                    break;

                case SettingEditor.Number:
                    NumberValue = AsNumber(value);
                    break;

                case SettingEditor.Select:
                case SettingEditor.Segment:
                    Fit(value);
                    break;

                case SettingEditor.List:
                    Lines = AsLines(value);
                    break;

                default:
                    TextValue = AsText(value);
                    break;
            }
        }
        finally
        {
            _filling = false;
        }
    }

    /// <summary>
    /// Finds the option holding this value, keeping a stored value nothing offered. A
    /// pinned version that is not installed is still the right value.
    /// </summary>
    private void Fit(object? value)
    {
        if (value is null)
        {
            _chosen = null;
            OnPropertyChanged(nameof(ChosenOption));
            Mark();
            return;
        }

        var match = Options.FirstOrDefault(option => Same(option.Value, value));

        if (match is null)
        {
            foreach (var stale in Options.Where(option => option.IsMissing).ToArray())
            {
                stale.PropertyChanged -= OnOptionChanged;
                Options.Remove(stale);
            }

            match = new SettingOptionViewModel(value, AsText(value), null, isMissing: true);
            Add(match);
        }

        _chosen = match;
        OnPropertyChanged(nameof(ChosenOption));
        Mark();
    }

    private void Add(SettingOptionViewModel option)
    {
        option.PropertyChanged += OnOptionChanged;
        Options.Add(option);
    }

    /// <summary>The radios and the dropdown read the same answer, so both are written.</summary>
    private void Mark()
    {
        var was = _filling;
        _filling = true;

        try
        {
            foreach (var option in Options)
            {
                option.IsChosen = ReferenceEquals(option, _chosen);
            }
        }
        finally
        {
            _filling = was;
        }
    }

    private void OnOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_filling
            || e.PropertyName is not nameof(SettingOptionViewModel.IsChosen)
            || sender is not SettingOptionViewModel { IsChosen: true } option)
        {
            return;
        }

        Choose(option);
    }

    /// <summary>
    /// Everything drawn here is worked out from the staged change and the file, so the
    /// empty name is the honest one. Avalonia reads it as every binding on this row.
    /// </summary>
    private void Announce() => OnPropertyChanged(string.Empty);

    private static void Bound(ISettingDescriptor descriptor, out decimal minimum, out decimal maximum)
    {
        minimum = decimal.MinValue;
        maximum = decimal.MaxValue;

        foreach (var rule in descriptor.Rules)
        {
            if (rule is not ISettingBounds bounds)
            {
                continue;
            }

            if (bounds.Minimum is IConvertible low)
            {
                minimum = low.ToDecimal(CultureInfo.InvariantCulture);
            }

            if (bounds.Maximum is IConvertible high)
            {
                maximum = high.ToDecimal(CultureInfo.InvariantCulture);
            }
        }
    }

    private static bool Same(object left, object right) =>
        left is string first && right is string second
            ? string.Equals(first, second, StringComparison.OrdinalIgnoreCase)
            : Equals(left, right);

    private static decimal AsNumber(object? value) =>
        value is IConvertible number ? number.ToDecimal(CultureInfo.InvariantCulture) : 0m;

    private static string AsText(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        bool flag => flag ? "true" : "false",
        Array items => string.Join(", ", items.Cast<object?>().Select(AsText)),
        IConvertible number => number.ToString(CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static IReadOnlyList<SettingsListEntry> AsLines(object? value) =>
        value is Array items
            ? [.. items.Cast<object?>().Select(item => new SettingsListEntry(AsText(item)))]
            : [];
}
