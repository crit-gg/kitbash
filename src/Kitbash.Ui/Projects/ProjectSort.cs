namespace Kitbash.Ui.Projects;

/// <summary>
/// How the list is ordered. Searching never changes it, so typing three letters leaves
/// what is left in the order it was already in.
/// </summary>
public enum ProjectSort
{
    /// <summary>The order the store keeps, which is newest first.</summary>
    Recent,

    Name,

    Path,
}
