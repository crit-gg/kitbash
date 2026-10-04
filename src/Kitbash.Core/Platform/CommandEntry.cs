namespace Kitbash.Core.Platform;

/// <summary>What a command folder holds under one name.</summary>
/// <param name="Exists">Whether anything at all is there.</param>
/// <param name="Program">
/// The program it runs, when it is shaped like an entry Kitbash writes. Null for anything
/// else, such as a plain file somebody put there.
/// </param>
public sealed record CommandEntry(bool Exists, string? Program)
{
    public static CommandEntry Absent { get; } = new(false, null);
}
