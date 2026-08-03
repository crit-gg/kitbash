namespace Workbench.Core.Platform;

/// <summary>
/// No bundle, so nothing to correct. Windows, and Linux outside an AppImage.
/// </summary>
internal sealed class PlainEnvironment : IBundleEnvironment
{
    public IReadOnlyDictionary<string, string> Outside { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
