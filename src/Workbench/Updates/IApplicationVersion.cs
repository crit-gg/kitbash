namespace Workbench.Updates;

/// <summary>
/// Which version of Workbench is running. Separate from updating, so anything that only
/// wants to say what this copy is does not depend on where newer ones come from.
/// </summary>
public interface IApplicationVersion
{
    /// <summary>
    /// The version, in the semantic form used everywhere. Blank when it cannot be
    /// established at all, which nothing built here should produce.
    /// </summary>
    string Current { get; }

    /// <summary>Whether this copy was installed, rather than run from a build directory.</summary>
    bool IsInstalled { get; }
}
