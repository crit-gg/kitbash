namespace Workbench.Core.Settings.Schema;

/// <summary>
/// One thing a <see cref="SettingsHome"/> keeps files for. A home over this machine has a
/// single place and no name for it. A home over workspaces has one per workspace, each
/// named, and a settings tree draws a level for them.
/// </summary>
/// <param name="Id">
/// Opaque and stable, and compared ordinally, since it only ever names a place the home
/// itself listed. A workspace's root path is what fills it.
/// </param>
/// <param name="Name">What a tree draws. Blank means there is no level to draw.</param>
public sealed record SettingsPlace(string Id, string Name)
{
    /// <summary>The one place of a home that has only one, drawn as no level at all.</summary>
    public static SettingsPlace Only { get; } = new(string.Empty, string.Empty);

    public bool IsNamed => Name.Length > 0;

    public override string ToString() => IsNamed ? Name : Id;
}
