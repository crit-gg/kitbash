namespace Kitbash.Core.Godot;

/// <summary>
/// Where each kind of install sits under the engine directory. An official install is
/// <c>&lt;root&gt;/4.7.1-stable-mono</c>. A repository's installs sit under its address:
/// <c>&lt;root&gt;/github/crit-gg/godot-slopworks/4.7.2-slopworks-18d5d19-mono</c> for an
/// exact pin and <c>.../newest/4.7.2-mono</c> for a slot.
/// </summary>
internal sealed class EngineLayout
{
    /// <summary>The folder a repository keeps its slots in.</summary>
    public const string Newest = "newest";

    /// <summary>Where a newer build waits while the one in its slot is running.</summary>
    private const string Staging = ".staging";

    /// <summary>Where a replaced build goes on its way out, so a swap is two renames.</summary>
    private const string Retired = ".retired";

    private const string MonoSuffix = "-mono";

    public EngineLayout(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        Root = root;
    }

    public string Root { get; }

    /// <summary>The first folder under the root for every repository kind this app reads.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [EngineRepositoryAddress.GitHub];

    public string RepositoryDirectory(EngineRepositoryAddress address) =>
        Path.Combine([Root, .. address.Segments]);

    /// <summary>Where a build that never moves is unpacked.</summary>
    public string DirectoryFor(EngineBuild build) =>
        build.Id.Repository is { } address
            ? Path.Combine(RepositoryDirectory(address), build.Release + (build.IsMono ? MonoSuffix : string.Empty))
            : Path.Combine(Root, build.Id.DirectoryName);

    public string SlotDirectory(EngineRepositoryAddress address, string slot) =>
        Path.Combine(RepositoryDirectory(address), Newest, slot);

    public string StagingDirectory(EngineRepositoryAddress address, string slot) =>
        Path.Combine(RepositoryDirectory(address), Staging, slot);

    public string RetiredDirectory(EngineRepositoryAddress address, string slot) =>
        Path.Combine(RepositoryDirectory(address), Retired, slot);

    /// <summary>A name the layout keeps for itself, which is never an install.</summary>
    public static bool IsReserved(string name) => name is Newest or Staging or Retired;
}
