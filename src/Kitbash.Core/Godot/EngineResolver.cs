namespace Kitbash.Core.Godot;

/// <summary>
/// Matches a requirement against the engines on this machine.
/// </summary>
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

        if (requirement.RepositoryName is not null && requirement.Version is { } pinned)
        {
            return ResolveRepository(requirement, pinned, usable, fallback);
        }

        // A repository build is custom and would answer any official pattern naming no
        // channel, so an official pin only ever looks at official installs.
        usable = usable.Where(engine => engine.Id.IsOfficial).ToList();

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

    /// <summary>
    /// The repository is matched before anything else, so a custom build never answers an
    /// official pin and the other way round. A newest pin takes only its own slot and an
    /// exact pin takes only an install that never moves.
    /// </summary>
    private static EngineResolution ResolveRepository(
        EngineRequirement requirement,
        EngineVersionPattern pinned,
        List<InstalledEngine> usable,
        InstalledEngine? fallback)
    {
        var address = requirement.Repository?.Address;

        var candidates = usable
            .Where(engine => address is not null
                && engine.Repository == address
                && pinned.Matches(engine.Id)
                && engine.IsSlot == !pinned.IsExact)
            .ToList();

        // A slot is keyed by the pin as written, so 4.7 and 4.7.2 are never each other's.
        if (requirement.Slot is { } slot)
        {
            var slotted = candidates.Find(engine => engine.Record.Slot == slot);

            if (slotted is not null)
            {
                return new EngineResolution(requirement, slotted, EngineMatch.Matched, IsDefault: false);
            }

            candidates = candidates.Where(engine => SlotVersion(engine.Record.Slot) == SlotVersion(slot)).ToList();
        }

        if (candidates.Find(engine => Fits(engine, requirement)) is { } exact)
        {
            return new EngineResolution(requirement, exact, EngineMatch.Matched, IsDefault: false);
        }

        if (candidates.FirstOrDefault() is { } wrongRuntime)
        {
            return new EngineResolution(requirement, wrongRuntime, EngineMatch.Mismatch, IsDefault: false);
        }

        return fallback is null
            ? new EngineResolution(requirement, null, EngineMatch.Missing, IsDefault: false)
            : new EngineResolution(requirement, fallback, EngineMatch.Mismatch, IsDefault: true);
    }

    private static string SlotVersion(string slot) =>
        slot.EndsWith("-mono", StringComparison.Ordinal) ? slot[..^"-mono".Length] : slot;

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
