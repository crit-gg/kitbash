namespace Kitbash.Core.Settings;

/// <summary>
/// Whether settings belong to everything or to one tool. These are separate
/// namespaces, so a missing tool setting does not fall back to a global value of the
/// same name.
/// </summary>
public readonly record struct SettingsScope
{
    private SettingsScope(string? toolId)
    {
        ToolId = toolId;
    }

    /// <summary>Settings the launcher and every tool can read.</summary>
    public static SettingsScope Global { get; } = new(null);

    /// <summary>Settings for one tool, keyed by the id the launcher installed it under.</summary>
    public static SettingsScope ForTool(string toolId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);

        // The id becomes a file name.
        if (toolId.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                $"Tool id '{toolId}' cannot be used as a file name.",
                nameof(toolId));
        }

        return new SettingsScope(toolId);
    }

    /// <summary>Null when this is <see cref="Global"/>.</summary>
    public string? ToolId { get; }

    public bool IsGlobal => ToolId is null;

    public override string ToString() => ToolId ?? "global";
}
