namespace Kitbash.Core.IO;

/// <summary>What changed in a watched directory.</summary>
/// <param name="Name">
/// The file or folder that changed, without a path. Null when the watch could not say,
/// which happens when it lost notifications or lost the directory. A caller that filters on
/// this has to treat null as worth reading, since it means the opposite of nothing.
/// </param>
public sealed record DirectoryChangedEventArgs(string? Name);
