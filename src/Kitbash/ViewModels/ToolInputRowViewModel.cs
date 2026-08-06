using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Tools;
using Kitbash.Ui.Controls;

namespace Kitbash.ViewModels;

/// <summary>
/// One field of the form a script is run from. The value is held as text whatever the kind
/// is, since text is what reaches the command line.
/// </summary>
public sealed partial class ToolInputRowViewModel : ObservableObject
{
    private readonly ToolInput _input;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    public ToolInputRowViewModel(ToolInput input, string value)
    {
        ArgumentNullException.ThrowIfNull(input);

        _input = input;
        _text = value;

        Options = [.. input.Choices];
        Check();
    }

    public string Key => _input.Key;

    public string Name => _input.Name;

    public string Description => _input.Description;

    public bool HasDescription => Description.Length > 0;

    public bool IsText => _input.Kind is ToolInputKind.Text;

    public bool IsNumber => _input.Kind is ToolInputKind.Number or ToolInputKind.Integer;

    public bool IsBoolean => _input.Kind is ToolInputKind.Boolean;

    public bool IsChoice => _input.Kind is ToolInputKind.Choice;

    public bool IsPath => _input.Kind is ToolInputKind.File or ToolInputKind.Folder;

    public PathTarget PathTarget =>
        _input.Kind == ToolInputKind.Folder ? PathTarget.Folder : PathTarget.File;

    public IReadOnlyList<ToolInputChoice> Options { get; }

    public bool HasMessage => Message.Length > 0;

    public bool IsValid => Message.Length == 0;

    /// <summary>What the box says, for a boolean drawn as a toggle.</summary>
    public bool Flag
    {
        get => string.Equals(Text, ToolInput.True, StringComparison.OrdinalIgnoreCase);
        set => Text = value ? ToolInput.True : "false";
    }

    /// <summary>
    /// The spinner's value. It is null while the field is empty, so an optional number can
    /// be left out rather than being forced to zero.
    /// </summary>
    public decimal? Number
    {
        get => decimal.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
        set => Text = value is { } number
            ? number.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    /// <summary>Whole numbers step by one and anything else by a tenth.</summary>
    public decimal Step => _input.Kind == ToolInputKind.Integer ? 1 : 0.1m;

    public decimal Minimum => Bound(_input.Minimum, decimal.MinValue);

    public decimal Maximum => Bound(_input.Maximum, decimal.MaxValue);

    public ToolInputChoice? Chosen
    {
        get => Options.FirstOrDefault(option => option.Value == Text);
        set => Text = value?.Value ?? string.Empty;
    }

    /// <summary>What this field puts on the command line.</summary>
    public IReadOnlyList<string> Arguments() => _input.Arguments(Text);

    partial void OnTextChanged(string value)
    {
        Check();

        OnPropertyChanged(nameof(Flag));
        OnPropertyChanged(nameof(Number));
        OnPropertyChanged(nameof(Chosen));
    }

    partial void OnMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasMessage));
        OnPropertyChanged(nameof(IsValid));
    }

    private void Check() => Message = _input.Check(Text) ?? string.Empty;

    private static decimal Bound(double? value, decimal fallback) =>
        value is { } number && number > (double)decimal.MinValue && number < (double)decimal.MaxValue
            ? (decimal)number
            : fallback;
}
