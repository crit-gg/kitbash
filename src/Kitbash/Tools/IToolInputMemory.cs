namespace Kitbash.Tools;

/// <summary>
/// What a form was last answered with, for this person on this machine. Only an input the
/// manifest marks is kept, so a value nobody asked to remember is typed again.
/// </summary>
public interface IToolInputMemory
{
    /// <summary>The value last accepted for each remembered input, keyed by its key.</summary>
    IReadOnlyDictionary<string, string> Read(ToolId id, IReadOnlyList<ToolInput> inputs);

    /// <summary>Keeps what the form was accepted with. A blank value forgets the key.</summary>
    void Write(ToolId id, IReadOnlyList<ToolInput> inputs, IReadOnlyDictionary<string, string> values);
}
