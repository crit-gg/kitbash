namespace Kitbash.Core.Platform;

/// <summary>
/// What to overlay on a program that is not part of this application bundle, so it does
/// not inherit paths that point inside the bundle. Empty when there is no bundle.
/// </summary>
public interface IBundleEnvironment
{
    /// <summary>
    /// Variables to set on a child, in the form <see cref="ProcessRequest.Environment"/>
    /// takes, so an empty value means unset. Empty when nothing needs correcting.
    /// </summary>
    IReadOnlyDictionary<string, string> Outside { get; }
}
