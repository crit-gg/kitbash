namespace Kitbash.Core.Settings;

/// <summary>
/// What <c>window.nativeChrome</c> means on this desktop. Turning it on always gives the
/// desktop's own title bar. What it means to turn it off is the part that differs, which is
/// why the answer is registered per OS rather than decided here.
/// </summary>
/// <param name="Off">What this desktop draws when a person has not asked for the native frame.</param>
public sealed record WindowChromeRule(WindowChromeKind Off)
{
    public WindowChromeKind Resolve(bool preferNative) =>
        preferNative ? WindowChromeKind.Desktop : Off;
}
