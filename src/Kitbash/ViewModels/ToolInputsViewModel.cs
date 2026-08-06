using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Tools;

namespace Kitbash.ViewModels;

/// <summary>
/// The form a script is run from. One row per input the manifest declares, filled from what
/// was remembered and from the defaults behind that.
/// </summary>
public sealed partial class ToolInputsViewModel : ObservableObject
{
    private readonly IReadOnlyList<ToolInput> _inputs;

    [ObservableProperty]
    private bool _canRun;

    /// <param name="remembered">What the form was last accepted with, keyed by input key.</param>
    /// <param name="workspaceRoot">Fills the workspace token in a default, or nothing.</param>
    public ToolInputsViewModel(
        InstalledTool tool,
        IReadOnlyDictionary<string, string> remembered,
        string? workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(remembered);

        _inputs = tool.Manifest.Inputs;

        ToolName = tool.Name;

        foreach (var input in _inputs)
        {
            var value = remembered.TryGetValue(input.Key, out var kept) && kept.Length > 0
                ? kept
                : input.DefaultFor(workspaceRoot);

            var row = new ToolInputRowViewModel(input, value);

            row.PropertyChanged += OnRowChanged;
            Rows.Add(row);
        }

        Recheck();
    }

    public string ToolName { get; }

    public string Title => $"Run {ToolName}";

    public IList<ToolInputRowViewModel> Rows { get; } = [];

    /// <summary>The answers, keyed by input key, for what is remembered afterwards.</summary>
    public IReadOnlyDictionary<string, string> Values =>
        Rows.ToDictionary(row => row.Key, row => row.Text, StringComparer.Ordinal);

    /// <summary>Every answer as command line arguments, in the order the manifest wrote them.</summary>
    public IReadOnlyList<string> Arguments() => [.. Rows.SelectMany(row => row.Arguments())];

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ToolInputRowViewModel.IsValid))
        {
            Recheck();
        }
    }

    private void Recheck() => CanRun = Rows.All(row => row.IsValid);
}
