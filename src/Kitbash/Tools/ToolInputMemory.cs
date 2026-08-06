using Kitbash.Core.Settings;

namespace Kitbash.Tools;

/// <summary>
/// Keeps remembered answers in the tool's own state file, beside the version it runs. They
/// are the app's record rather than anyone's setting, which is what state is for.
/// </summary>
public sealed class ToolInputMemory : IToolInputMemory
{
    /// <summary>The table remembered answers sit in, under the tool's state.</summary>
    private const string Table = "inputs";

    private readonly IApplicationState _state;
    private readonly ToolLog _log;

    public ToolInputMemory(IApplicationState state, ToolLog log)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(log);

        _state = state;
        _log = log;
    }

    public IReadOnlyDictionary<string, string> Read(ToolId id, IReadOnlyList<ToolInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var stored = _state.ForTool(id.Value);
        Dictionary<string, string> found = new(StringComparer.Ordinal);

        foreach (var input in inputs.Where(input => input.Remember))
        {
            var value = stored.Get(KeyFor(input), string.Empty);

            if (value.Length > 0)
            {
                found[input.Key] = value;
            }
        }

        return found;
    }

    public void Write(
        ToolId id, IReadOnlyList<ToolInput> inputs, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(values);

        List<SettingsEdit> edits = [];

        foreach (var input in inputs.Where(input => input.Remember))
        {
            var value = values.TryGetValue(input.Key, out var answered) ? answered.Trim() : string.Empty;

            edits.Add(value.Length > 0
                ? SettingsEdit.Set(KeyFor(input), value)
                : SettingsEdit.Remove(KeyFor(input)));
        }

        if (edits.Count == 0)
        {
            return;
        }

        try
        {
            _state.Apply(SettingsScope.ForTool(id.Value), edits);
        }
        catch (SettingsFileUnreadableException exception)
        {
            // Losing what was typed is not worth failing a run that is about to start.
            _log.Say($"{id} could not remember what its form was answered with", exception);
        }
    }

    private static string KeyFor(ToolInput input) => $"{Table}.{input.Key}";
}
