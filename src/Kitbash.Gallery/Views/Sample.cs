using System.Collections;
using System.ComponentModel;

namespace Kitbash.Gallery.Views;

/// <summary>
/// What the validation grid is made of. It reports its own errors, which is all the grid
/// reads, and the rule spanning the low and the high is what a row transaction is for.
/// </summary>
public sealed class Sample(string code, string name, double low, double high)
    : INotifyPropertyChanged, INotifyDataErrorInfo, IEditableObject
{
    /// <summary>What a code has to start with, which is the one field rule on this row.</summary>
    private const string Prefix = "MTL_";

    private string identifier = code;
    private string title = name;
    private double bottom = low;
    private double top = high;

    private (string Code, string Name, double Low, double High)? held;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public string Code
    {
        get => identifier;
        set
        {
            if (Set(ref identifier, value, nameof(Code)))
            {
                Wrong(nameof(Code));
            }
        }
    }

    public string Name
    {
        get => title;
        set
        {
            if (Set(ref title, value, nameof(Name)))
            {
                Wrong(nameof(Name));
            }
        }
    }

    public double Low
    {
        get => bottom;
        set
        {
            if (Set(ref bottom, value, nameof(Low)))
            {
                Wrong(nameof(Low));
                Wrong(nameof(High));
            }
        }
    }

    public double High
    {
        get => top;
        set
        {
            if (Set(ref top, value, nameof(High)))
            {
                Wrong(nameof(Low));
                Wrong(nameof(High));
            }
        }
    }

    public bool HasErrors =>
        Problems(nameof(Code)).Count > 0
        || Problems(nameof(Name)).Count > 0
        || Problems(nameof(Low)).Count > 0;

    public IEnumerable GetErrors(string? propertyName) => Problems(propertyName);

    public override string ToString() => Code;

    /// <summary>What is wrong with one field, or nothing.</summary>
    private List<string> Problems(string? field) => field switch
    {
        nameof(Code) when !identifier.StartsWith(Prefix, StringComparison.Ordinal) =>
            ["A code starts with MTL_."],
        nameof(Name) when string.IsNullOrWhiteSpace(title) => ["A name is needed."],
        nameof(Low) or nameof(High) when top <= bottom => ["The high has to be over the low."],
        _ => [],
    };

    private bool Set<T>(ref T field, T value, string property)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

        return true;
    }

    private void Wrong(string field) =>
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(field));

    // Under a row transaction the row opens on the first cell touched and closes when the
    // edit leaves the row, so a rule over two fields is checked once both hold values.

    void IEditableObject.BeginEdit() => held ??= (identifier, title, bottom, top);

    void IEditableObject.CancelEdit()
    {
        if (held is not { } was)
        {
            return;
        }

        held = null;

        Code = was.Code;
        Name = was.Name;
        Low = was.Low;
        High = was.High;
    }

    void IEditableObject.EndEdit() => held = null;
}
