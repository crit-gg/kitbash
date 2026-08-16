using Kitbash.Core.IO;

namespace Kitbash.Core.Platform.MacOS;

/// <summary>
/// Case does not count. APFS is case insensitive and case preserving as it ships, and a
/// volume formatted case sensitive is the case this answer is wrong for, which is safer
/// than reading two spellings of one folder as two places.
/// </summary>
internal sealed class MacPathRules : PathRules
{
    protected override StringComparison Comparison => StringComparison.OrdinalIgnoreCase;
}
