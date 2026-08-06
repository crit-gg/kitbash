namespace Kitbash.Settings;

/// <summary>
/// The per tool answers behind <c>launcher.after.tool</c>. An array of tables, which no
/// descriptor can describe, so it is read and written directly the way the custom tool
/// list is.
/// </summary>
public interface IAfterLaunchOverrides
{
    /// <summary>
    /// In the order the file holds them. A row naming a tool that is not installed is
    /// still returned, since it belongs to a tool that may come back.
    /// </summary>
    IReadOnlyList<AfterLaunchOverride> Read();

    /// <summary>Replaces the whole list. An empty one takes every block out of the file.</summary>
    void Write(IReadOnlyList<AfterLaunchOverride> overrides);
}
