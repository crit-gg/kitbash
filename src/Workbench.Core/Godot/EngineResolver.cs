namespace Workbench.Core.Godot;

/// <summary>
/// Matches a requirement against the engines on this machine.
/// </summary>
/// <remarks>
/// <para>
/// The order is: the right version with the right runtime, then the right version with
/// the wrong runtime, then this machine's default, then nothing. Each step down is a
/// worse answer and the first three all name an engine, so the strip can offer to open
/// something in every case where opening something is possible.
/// </para>
/// <para>
/// **A .NET project will not open in a plain engine**, which is why the runtime flag is
/// the first thing filtered on rather than a detail checked afterwards. The version being
/// right is not enough, and reporting a match on version alone would send somebody into
/// an editor that cannot build their code.
/// </para>
/// <para>
/// An engine whose folder has gone is left out of all of it. It is still listed on the
/// engines page, since forgetting it has to be deliberate, but it cannot answer anything.
/// </para>
/// </remarks>
internal sealed class EngineResolver : IEngineResolver
{
    public EngineResolution Resolve(
        EngineRequirement requirement,
        IReadOnlyList<InstalledEngine> installed,
        EngineId? theDefault)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(installed);

        if (requirement.Project is null)
        {
            return EngineResolution.None;
        }

        var usable = installed.Where(engine => !engine.IsMissing).ToList();
        var fallback = theDefault is { } id
            ? usable.FirstOrDefault(engine => engine.Id == id)
            : null;

        if (requirement.Version is not { } wanted)
        {
            // Nothing was asked for, so the default is the answer and the only thing that
            // can be wrong with it is the runtime.
            return fallback is null
                ? new EngineResolution(requirement, null, EngineMatch.Missing, IsDefault: false)
                : new EngineResolution(
                    requirement,
                    fallback,
                    Fits(fallback, requirement) ? EngineMatch.Matched : EngineMatch.Mismatch,
                    IsDefault: true);
        }

        if (Best(usable.Where(engine => Fits(engine, requirement)), wanted) is { } exact)
        {
            return new EngineResolution(requirement, exact, EngineMatch.Matched, IsDefault: false);
        }

        if (Best(usable.Where(engine => !Fits(engine, requirement)), wanted) is { } wrongRuntime)
        {
            return new EngineResolution(requirement, wrongRuntime, EngineMatch.Mismatch, IsDefault: false);
        }

        return fallback is null
            ? new EngineResolution(requirement, null, EngineMatch.Missing, IsDefault: false)
            : new EngineResolution(requirement, fallback, EngineMatch.Mismatch, IsDefault: true);
    }

    private static bool Fits(InstalledEngine engine, EngineRequirement requirement) =>
        engine.IsMono == requirement.NeedsDotnet;

    /// <summary>
    /// The best engine in a set for one pattern. Two engines can share a tag, since a
    /// version and its .NET build are separate installs, so the set handed in is already
    /// filtered by the runtime flag and a tag inside it names one engine.
    /// </summary>
    private static InstalledEngine? Best(IEnumerable<InstalledEngine> engines, EngineVersionPattern wanted)
    {
        var list = engines.ToList();

        return wanted.BestMatch(list.Select(engine => engine.Tag)) is { } tag
            ? list.Find(engine => engine.Tag == tag)
            : null;
    }
}
