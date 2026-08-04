using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A well holding one path, with a browse button beside it. It holds exactly one path or
/// none, so there is no list, no add button and no multiple selection anywhere in it.
/// </summary>
public class PathField : TemplatedControl
{
    private const string FieldPart = "PART_Field";
    private const string BrowsePart = "PART_Browse";
    private const string ClearPart = "PART_Clear";

    /// <summary>The path, or blank for none. Clearing writes blank and never null.</summary>
    public static readonly StyledProperty<string?> PathProperty =
        AvaloniaProperty.Register<PathField, string?>(nameof(Path), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether this picks a file or a folder.</summary>
    public static readonly StyledProperty<PathTarget> TargetProperty =
        AvaloniaProperty.Register<PathField, PathTarget>(nameof(Target));

    /// <summary>
    /// Whether a path can be typed. Turn it off only where a hand written path would be
    /// meaningless. Browsing and clearing still work with it off.
    /// </summary>
    public static readonly StyledProperty<bool> AllowsTypingProperty =
        AvaloniaProperty.Register<PathField, bool>(nameof(AllowsTyping), defaultValue: true);

    /// <summary>
    /// The last segment of the path, when the host owns it rather than the person. Set it
    /// and browsing picks the folder that one goes inside: the dialog opens there, and a
    /// pick or a drop becomes the picked folder joined with this name. Folder targets only,
    /// and blank leaves browsing alone.
    /// </summary>
    public static readonly StyledProperty<string?> FolderNameProperty =
        AvaloniaProperty.Register<PathField, string?>(nameof(FolderName));

    /// <summary>The icon only form, for a column too narrow for the word Browse.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<PathField, bool>(nameof(IsCompact));

    /// <summary>What the empty field says. Worked out from the target when nothing is set.</summary>
    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<PathField, string?>(nameof(PlaceholderText));

    /// <summary>The dialog's title. Worked out from the target when nothing is set.</summary>
    public static readonly StyledProperty<string?> DialogTitleProperty =
        AvaloniaProperty.Register<PathField, string?>(nameof(DialogTitle));

    /// <summary>
    /// The quiet line under the field. Worked out from the filters when nothing is set, and
    /// a problem replaces it rather than adding a second line.
    /// </summary>
    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<PathField, string?>(nameof(Hint));

    /// <summary>
    /// What is wrong with the path. The control sets this itself when a typed path fails
    /// the filters, and a host sets it for anything else, such as a path that has gone.
    /// </summary>
    public static readonly StyledProperty<string?> ProblemProperty =
        AvaloniaProperty.Register<PathField, string?>(nameof(Problem));

    /// <summary>How bad <see cref="Problem"/> is. Ignored while there is no problem.</summary>
    public static readonly StyledProperty<PathProblem> ProblemTierProperty =
        AvaloniaProperty.Register<PathField, PathProblem>(nameof(ProblemTier), defaultValue: PathProblem.Error);

    public static readonly DirectProperty<PathField, string?> NoteProperty =
        AvaloniaProperty.RegisterDirect<PathField, string?>(nameof(Note), field => field.Note);

    public static readonly DirectProperty<PathField, bool> HasNoteProperty =
        AvaloniaProperty.RegisterDirect<PathField, bool>(nameof(HasNote), field => field.HasNote);

    public static readonly DirectProperty<PathField, bool> ShowsClearProperty =
        AvaloniaProperty.RegisterDirect<PathField, bool>(nameof(ShowsClear), field => field.ShowsClear);

    public static readonly DirectProperty<PathField, bool> IsDroppingProperty =
        AvaloniaProperty.RegisterDirect<PathField, bool>(nameof(IsDropping), field => field.IsDropping);

    public static readonly DirectProperty<PathField, string?> DropWordProperty =
        AvaloniaProperty.RegisterDirect<PathField, string?>(nameof(DropWord), field => field.DropWord);

    public static readonly DirectProperty<PathField, string?> PromptProperty =
        AvaloniaProperty.RegisterDirect<PathField, string?>(nameof(Prompt), field => field.Prompt);

    public static readonly DirectProperty<PathField, string?> TipProperty =
        AvaloniaProperty.RegisterDirect<PathField, string?>(nameof(Tip), field => field.Tip);

    public static readonly DirectProperty<PathField, bool> IsInvalidProperty =
        AvaloniaProperty.RegisterDirect<PathField, bool>(nameof(IsInvalid), field => field.IsInvalid);

    public static readonly DirectProperty<PathField, bool> IsWarnedProperty =
        AvaloniaProperty.RegisterDirect<PathField, bool>(nameof(IsWarned), field => field.IsWarned);

    public static readonly DirectProperty<PathField, bool> IsLockedProperty =
        AvaloniaProperty.RegisterDirect<PathField, bool>(nameof(IsLocked), field => field.IsLocked);

    private string? _note;
    private bool _hasNote;
    private bool _showsClear;
    private bool _isDropping;
    private bool _isInvalid;
    private bool _isWarned;
    private bool _isLocked;
    private string? _dropWord;
    private string? _tip;
    private string? _prompt;

    /// <summary>The problem on show is one this control worked out, so it may clear it.</summary>
    private bool _ownsProblem;
    private string? _mine;

    private TextBox? _field;

    public PathField()
    {
        AddHandler(Button.ClickEvent, OnClick);

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);

        Filters.CollectionChanged += (_, _) => Refresh();
        Refresh();
    }

    /// <inheritdoc cref="PathProperty"/>
    public string? Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    /// <inheritdoc cref="TargetProperty"/>
    public PathTarget Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <inheritdoc cref="AllowsTypingProperty"/>
    public bool AllowsTyping
    {
        get => GetValue(AllowsTypingProperty);
        set => SetValue(AllowsTypingProperty, value);
    }

    /// <inheritdoc cref="FolderNameProperty"/>
    public string? FolderName
    {
        get => GetValue(FolderNameProperty);
        set => SetValue(FolderNameProperty, value);
    }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    /// <inheritdoc cref="PlaceholderTextProperty"/>
    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <inheritdoc cref="DialogTitleProperty"/>
    public string? DialogTitle
    {
        get => GetValue(DialogTitleProperty);
        set => SetValue(DialogTitleProperty, value);
    }

    /// <inheritdoc cref="HintProperty"/>
    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    /// <inheritdoc cref="ProblemProperty"/>
    public string? Problem
    {
        get => GetValue(ProblemProperty);
        set => SetValue(ProblemProperty, value);
    }

    /// <inheritdoc cref="ProblemTierProperty"/>
    public PathProblem ProblemTier
    {
        get => GetValue(ProblemTierProperty);
        set => SetValue(ProblemTierProperty, value);
    }

    /// <summary>
    /// What the dialog offers and what a typed path is held to. They belong to the control
    /// rather than to the person using it, and a folder field ignores them.
    /// </summary>
    public AvaloniaList<PathFilter> Filters { get; } = [];

    /// <summary>The line under the field, which is the problem when there is one.</summary>
    public string? Note
    {
        get => _note;
        private set => SetAndRaise(NoteProperty, ref _note, value);
    }

    public bool HasNote
    {
        get => _hasNote;
        private set => SetAndRaise(HasNoteProperty, ref _hasNote, value);
    }

    /// <summary>There is a path to clear and the field is not being typed in.</summary>
    public bool ShowsClear
    {
        get => _showsClear;
        private set => SetAndRaise(ShowsClearProperty, ref _showsClear, value);
    }

    /// <summary>Something the field would take is being dragged over it.</summary>
    public bool IsDropping
    {
        get => _isDropping;
        private set => SetAndRaise(IsDroppingProperty, ref _isDropping, value);
    }

    public string? DropWord
    {
        get => _dropWord;
        private set => SetAndRaise(DropWordProperty, ref _dropWord, value);
    }

    /// <summary>What the empty field says, which names what will be picked.</summary>
    public string? Prompt
    {
        get => _prompt;
        private set => SetAndRaise(PromptProperty, ref _prompt, value);
    }

    /// <summary>The whole path, for the tooltip, since a long one does not fit the well.</summary>
    public string? Tip
    {
        get => _tip;
        private set => SetAndRaise(TipProperty, ref _tip, value);
    }

    /// <summary>The value will not do.</summary>
    public bool IsInvalid
    {
        get => _isInvalid;
        private set => SetAndRaise(IsInvalidProperty, ref _isInvalid, value);
    }

    /// <summary>The value is allowed and something about the world is wrong.</summary>
    public bool IsWarned
    {
        get => _isWarned;
        private set => SetAndRaise(IsWarnedProperty, ref _isWarned, value);
    }

    /// <summary>Nothing can be typed in. The inverse of <see cref="AllowsTyping"/>.</summary>
    public bool IsLocked
    {
        get => _isLocked;
        private set => SetAndRaise(IsLockedProperty, ref _isLocked, value);
    }

    /// <summary>There is a path.</summary>
    public bool HasPath => !string.IsNullOrWhiteSpace(Path);

    /// <summary>Empties the field.</summary>
    public void Clear()
    {
        SetCurrentValue(PathProperty, string.Empty);
        Judge();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_field is not null)
        {
            _field.PropertyChanged -= OnFieldChanged;
        }

        _field = e.NameScope.Find<TextBox>(FieldPart);

        if (_field is not null)
        {
            _field.PropertyChanged += OnFieldChanged;
        }

        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PathProperty
            || change.Property == TargetProperty
            || change.Property == HintProperty
            || change.Property == ProblemProperty
            || change.Property == ProblemTierProperty
            || change.Property == PlaceholderTextProperty
            || change.Property == AllowsTypingProperty
            || change.Property == IsCompactProperty
            || change.Property == IsDroppingProperty
            || change.Property == IsEffectivelyEnabledProperty)
        {
            Refresh();
        }

        // Typing over a path this control refused takes the refusal away until the field is
        // left again, so a path being fixed is not red while it is half fixed.
        if (change.Property == PathProperty && _ownsProblem && (_field?.IsKeyboardFocusWithin ?? false))
        {
            Forget();
        }

        // A host writing its own problem over ours takes it back.
        if (change.Property == ProblemProperty && _ownsProblem && change.GetNewValue<string?>() != _mine)
        {
            _ownsProblem = false;
        }
    }

    // Typing is judged when the field is left, so a half typed path never flashes red.
    private void OnFieldChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsKeyboardFocusWithinProperty)
        {
            if (!e.GetNewValue<bool>())
            {
                Judge();
            }

            Refresh();
        }
    }

    private void OnClick(object? sender, RoutedEventArgs e)
    {
        switch (e.Source)
        {
            case Button { Name: ClearPart }:
                Clear();

                // The button pressed is the button that just went, so the focus it took
                // goes to the field rather than nowhere.
                _field?.Focus();
                e.Handled = true;
                break;

            case Button { Name: BrowsePart }:
                e.Handled = true;
                Browse();
                break;
        }
    }

    private async void Browse()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        try
        {
            var picked = Target is PathTarget.Folder
                ? await PickFolder(storage).ConfigureAwait(true)
                : await PickFile(storage).ConfigureAwait(true);

            // A picker can answer something with no path of its own, such as a document a
            // portal hands back by handle. There is nothing to put in the field for that.
            if (picked?.TryGetLocalPath() is { } path)
            {
                Take(path);
            }
        }
        catch (Exception exception) when (exception is IOException or ArgumentException
            or UnauthorizedAccessException or NotSupportedException)
        {
            // A dialog that could not open leaves the field as it was.
        }
    }

    private async Task<IStorageItem?> PickFile(IStorageProvider storage)
    {
        if (!storage.CanOpen)
        {
            return null;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = DialogTitle ?? "Choose a file",
            AllowMultiple = false,
            FileTypeFilter = Filters.Count == 0 ? null : [.. Filters.Select(filter => filter.ForDialog())],
            SuggestedStartLocation = await StartingAt(storage).ConfigureAwait(true),
            SuggestedFileName = HasPath ? System.IO.Path.GetFileName(Path!) : null,
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0] : null;
    }

    private async Task<IStorageItem?> PickFolder(IStorageProvider storage)
    {
        if (!storage.CanPickFolder)
        {
            return null;
        }

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = DialogTitle ?? "Choose a folder",
            AllowMultiple = false,
            SuggestedStartLocation = await StartingAt(storage).ConfigureAwait(true),
        }).ConfigureAwait(true);

        return folders.Count > 0 ? folders[0] : null;
    }

    /// <summary>Where the dialog opens, which is the path already set, or the platform's own.</summary>
    private async Task<IStorageFolder?> StartingAt(IStorageProvider storage)
    {
        if (!HasPath)
        {
            return null;
        }

        // A name of the host's means the folder being picked is the one above it, which is
        // also the folder that has to exist while the named one does not yet.
        var folder = Target is PathTarget.Folder && !OwnsName
            ? Path!.Trim()
            : System.IO.Path.GetDirectoryName(
                System.IO.Path.TrimEndingDirectorySeparator(Path!.Trim()));

        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        try
        {
            return await storage.TryGetFolderFromPathAsync(folder).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException
            or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var offered = Offered(e);

        IsDropping = offered is not null && IsEffectivelyEnabled;
        e.DragEffects = IsDropping ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => IsDropping = false;

    private void OnDrop(object? sender, DragEventArgs e)
    {
        IsDropping = false;

        if (!IsEffectivelyEnabled || Offered(e) is not { } path)
        {
            return;
        }

        Take(path);
        e.Handled = true;
    }

    /// <summary>
    /// The path a drag is carrying, or null when this field would not take it. A drag of
    /// several items is judged by its first, which is the one a drop would take.
    /// </summary>
    private string? Offered(DragEventArgs e)
    {
        if (e.DataTransfer.TryGetValues(DataFormat.File) is not { Length: > 0 } items)
        {
            return null;
        }

        var wantsFolder = Target is PathTarget.Folder;
        var first = items[0];

        if ((first is IStorageFolder) != wantsFolder || first.TryGetLocalPath() is not { } path)
        {
            return null;
        }

        return wantsFolder || Accepts(path) ? path : null;
    }

    /// <summary>Takes a path from a dialog or a drop, which replaces whatever was there.</summary>
    private void Take(string path)
    {
        // A folder dialog on Windows can answer a drive root, whose trailing separator is
        // part of the name, so only a longer path is trimmed.
        var trimmed = Target is PathTarget.Folder && System.IO.Path.EndsInDirectorySeparator(path)
            ? System.IO.Path.TrimEndingDirectorySeparator(path)
            : path;

        SetCurrentValue(PathProperty, OwnsName ? System.IO.Path.Combine(trimmed, FolderName!.Trim()) : trimmed);
        Judge();
    }

    /// <summary>The host named the last segment, so a pick is the folder above it.</summary>
    private bool OwnsName => Target is PathTarget.Folder && !string.IsNullOrWhiteSpace(FolderName);

    /// <summary>Holds a typed path to the filters, and says so in place of the hint.</summary>
    private void Judge()
    {
        if (Restricts && HasPath && !Accepts(Path!.Trim()))
        {
            _mine = $"Only {Say(Extensions)} files are accepted";
            _ownsProblem = true;
            SetCurrentValue(ProblemTierProperty, PathProblem.Error);
            SetCurrentValue(ProblemProperty, _mine);
            return;
        }

        Forget();
    }

    /// <summary>Takes back a problem this control put up, leaving a host's alone.</summary>
    private void Forget()
    {
        if (!_ownsProblem)
        {
            return;
        }

        _ownsProblem = false;
        _mine = null;
        SetCurrentValue(ProblemProperty, null);
    }

    private bool Accepts(string path) => Filters.Any(filter => filter.Accepts(path));

    /// <summary>The filters bind a typed path only when every one of them names extensions.</summary>
    private bool Restricts =>
        Target is PathTarget.File
        && Filters.Count > 0
        && Filters.All(filter => filter.Takes.Count > 0);

    private IReadOnlyList<string> Extensions =>
        [.. Filters.SelectMany(filter => filter.Takes).Distinct(StringComparer.OrdinalIgnoreCase)];

    private void Refresh()
    {
        var problem = string.IsNullOrWhiteSpace(Problem) ? null : Problem;
        var tier = problem is null ? PathProblem.None : ProblemTier;

        Note = problem ?? Quiet();
        HasNote = !string.IsNullOrWhiteSpace(Note);
        // A press inside the well focuses the field, so a button that went on focus would
        // go under its own press and never be clicked. Gone rather than greyed when the
        // whole control is off.
        ShowsClear = HasPath && IsEffectivelyEnabled;
        DropWord = Target is PathTarget.Folder ? "Drop to use this folder" : "Drop to use this file";
        Tip = HasPath ? Path : null;
        Prompt = PlaceholderText
            ?? (Target is PathTarget.Folder ? "No folder selected" : "No file selected");
        IsInvalid = tier is PathProblem.Error;
        IsWarned = tier is PathProblem.Warn;
        IsLocked = !AllowsTyping;

        Classes.Set("empty", !HasPath);
        Classes.Set("clearable", ShowsClear);
        Classes.Set("compact", IsCompact);
        Classes.Set("dropping", IsDropping);
        Classes.Set("error", IsInvalid);
        Classes.Set("warn", IsWarned);
    }

    /// <summary>The hint, which is the filters said in words when nobody wrote one.</summary>
    private string? Quiet()
    {
        if (Hint is not null)
        {
            return Hint;
        }

        return Restricts ? $"Accepts {Say(Extensions)}" : null;
    }

    private static string Say(IReadOnlyList<string> words) => words.Count switch
    {
        0 => string.Empty,
        1 => words[0],
        _ => $"{string.Join(", ", words.Take(words.Count - 1))} and {words[^1]}",
    };
}
