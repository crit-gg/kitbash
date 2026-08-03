namespace Workbench.Core.Settings.Schema;

/// <summary>
/// What a value has to be, as data rather than as a predicate, so a rule can also bound
/// an editor and write its own summary. The set of implementations is closed.
/// </summary>
public interface ISettingRule
{
    /// <summary>Plain language, said as what the value has to be.</summary>
    string Summary { get; }
}

/// <summary>A rule over one setting's type.</summary>
public interface ISettingRule<in T> : ISettingRule
{
    /// <summary>
    /// Whether the value is allowed. <paramref name="reason"/> is null when it is, and
    /// says what is wrong with this value when it is not.
    /// </summary>
    bool Allows(T value, out string? reason);
}
