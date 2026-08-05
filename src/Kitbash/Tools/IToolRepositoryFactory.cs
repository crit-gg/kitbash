namespace Kitbash.Tools;

/// <summary>
/// Turns a list entry into something that can be asked. Adding a repository type is a
/// class and a case here, which is the whole point of the two operation abstraction.
/// </summary>
public interface IToolRepositoryFactory
{
    /// <summary>Null when no type here can speak to that entry.</summary>
    IToolRepository? For(ToolRepositorySource source);
}
