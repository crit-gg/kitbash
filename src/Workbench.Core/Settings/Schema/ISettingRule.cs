namespace Workbench.Core.Settings.Schema;

/// <summary>
/// What a value has to be, as data rather than as a predicate. The set of rules is
/// closed on purpose. A <c>Func&lt;T, bool&gt;</c> can validate and nothing else, so it
/// cannot bound a spinner, cannot write its own summary and cannot be reasoned about.
/// Adding a kind of rule is a deliberate change here.
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
