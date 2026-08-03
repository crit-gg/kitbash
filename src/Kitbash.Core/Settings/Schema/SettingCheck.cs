namespace Kitbash.Core.Settings.Schema;

/// <summary>How a stored value fared against the descriptor that declares it.</summary>
public enum SettingCheckResult
{
    /// <summary>The file does not hold this key at all.</summary>
    Absent = 0,

    /// <summary>Converted and passed every rule.</summary>
    Ok = 1,

    /// <summary>Present, but not the type the setting is declared as.</summary>
    WrongType = 2,

    /// <summary>The right type, and a rule refused it.</summary>
    Rejected = 3,
}

/// <summary>
/// One stored value judged by one descriptor. A value that is not
/// <see cref="IsUsable"/> falls through, so the layer below it decides instead.
/// </summary>
public sealed record SettingCheck
{
    private SettingCheck(SettingCheckResult result, string? message)
    {
        Result = result;
        Message = message;
    }

    /// <summary>Nothing was stored, which is normal and is never a problem.</summary>
    public static SettingCheck Absent { get; } = new(SettingCheckResult.Absent, null);

    public static SettingCheck Ok { get; } = new(SettingCheckResult.Ok, null);

    public static SettingCheck WrongType(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new SettingCheck(SettingCheckResult.WrongType, message);
    }

    public static SettingCheck Rejected(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new SettingCheck(SettingCheckResult.Rejected, message);
    }

    public SettingCheckResult Result { get; }

    /// <summary>Plain language, and null unless something went wrong.</summary>
    public string? Message { get; }

    public bool IsUsable => Result is SettingCheckResult.Ok;

    /// <summary>A value was stored and could not be used, which is worth reporting.</summary>
    public bool IsProblem => Result is SettingCheckResult.WrongType or SettingCheckResult.Rejected;
}
