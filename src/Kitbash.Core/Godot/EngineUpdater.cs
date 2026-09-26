using Kitbash.Core.Platform;

namespace Kitbash.Core.Godot;

internal sealed class EngineUpdater : IEngineUpdater, IDisposable
{
    private readonly IEngineRepositories _repositories;
    private readonly IEngineStore _store;
    private readonly IEngineInstaller _installer;
    private readonly IEngineTemplates _templates;
    private readonly IEngineFiles _engineFiles;
    private readonly IRunningPrograms _running;

    /// <summary>One check at a time, so two triggers never stage the same build twice.</summary>
    private readonly SemaphoreSlim _one = new(1, 1);

    public EngineUpdater(
        IEngineRepositories repositories,
        IEngineStore store,
        IEngineInstaller installer,
        IEngineTemplates templates,
        IEngineFiles engineFiles,
        IRunningPrograms running)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(engineFiles);
        ArgumentNullException.ThrowIfNull(running);

        _repositories = repositories;
        _store = store;
        _installer = installer;
        _templates = templates;
        _engineFiles = engineFiles;
        _running = running;
    }

    public async Task<EngineBuild?> FindAsync(
        EngineRequirement requirement,
        bool refresh,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        if (requirement.Repository is not { } source || requirement.Version is not { } pinned)
        {
            return null;
        }

        var repository = _repositories.For(source.Address);
        var releases = await repository.ReadReleasesAsync(refresh, cancellationToken).ConfigureAwait(false);

        var candidates = releases.Releases
            .Where(release => pinned.Matches(release.IdFor(requirement.NeedsDotnet)))
            .Where(release => pinned.IsExact || !release.IsPrerelease)
            .OrderByDescending(release => release.PublishedAt);

        foreach (var release in candidates)
        {
            var manifest = await repository.ReadManifestAsync(release.Name, cancellationToken).ConfigureAwait(false);

            var build = manifest.Builds.FirstOrDefault(candidate =>
                candidate.Platform == _engineFiles.Platform
                && candidate.Architecture == _engineFiles.Architecture
                && candidate.IsMono == requirement.NeedsDotnet);

            // A newest release with nothing for this machine is passed over for an older
            // one that has, since the pin asks for the newest build that runs here.
            if (build is not null)
            {
                return build;
            }
        }

        return null;
    }

    public async Task<InstalledEngine> InstallAsync(
        EngineRequirement requirement,
        EngineBuild build,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(build);

        if (requirement.Slot is not { } slot || build.Id.Repository is not { } address)
        {
            return await _installer.InstallAsync(build, progress, cancellationToken).ConfigureAwait(false);
        }

        await _one.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _installer.StageAsync(build, slot, progress, cancellationToken).ConfigureAwait(false);

            return _installer.Place(address, slot)
                ?? throw new EngineInstallException($"{build.FileName} was unpacked and then could not be found.");
        }
        finally
        {
            _one.Release();
        }
    }

    public async Task<EngineUpdate> UpdateAsync(
        EngineRequirement requirement,
        bool refresh,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        if (requirement.Slot is not { } slot || requirement.Repository is not { } source)
        {
            return EngineUpdate.NotFollowing;
        }

        await _one.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var installed = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
            var current = installed.FirstOrDefault(engine =>
                engine.Repository == source.Address && engine.Record.Slot == slot && !engine.IsMissing);

            if (current is null)
            {
                return new EngineUpdate(EngineUpdateOutcome.NotInstalled);
            }

            var staged = _installer.Staged(source.Address, slot);
            EngineBuild? newest;

            try
            {
                newest = await FindAsync(requirement, refresh, cancellationToken).ConfigureAwait(false);
            }
            catch (EngineCatalogueException) when (staged is not null && staged.Id.Build != current.Id.Build)
            {
                // Offline with a build already waiting, which was the newest when it was
                // fetched, so it is still placed.
                newest = null;
            }

            if (newest is not null && newest.Id.Build == current.Id.Build)
            {
                // A build staged for a release since withdrawn is not placed.
                return new EngineUpdate(EngineUpdateOutcome.UpToDate, current);
            }

            if (newest is null && (staged is null || staged.Id.Build == current.Id.Build))
            {
                return new EngineUpdate(EngineUpdateOutcome.UpToDate, current);
            }

            if (newest is not null && staged?.Id.Build != newest.Id.Build)
            {
                await _installer.StageAsync(newest, slot, progress, cancellationToken).ConfigureAwait(false);
            }

            if (_running.IsRunning(current.Executable))
            {
                return new EngineUpdate(EngineUpdateOutcome.Waiting, current);
            }

            InstalledEngine? placed;

            try
            {
                placed = _installer.Place(source.Address, slot);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Something still holds the old one open. The next check tries again.
                return new EngineUpdate(EngineUpdateOutcome.Waiting, current);
            }

            if (placed is null)
            {
                return new EngineUpdate(EngineUpdateOutcome.UpToDate, current);
            }

            return await FollowTemplatesAsync(current, placed, installed, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _one.Release();
        }
    }

    public void Dispose() => _one.Dispose();

    /// <summary>
    /// Installs templates for the new build when the old one had them, and removes the old
    /// folder when the version moved and nothing else reads it.
    /// </summary>
    private async Task<EngineUpdate> FollowTemplatesAsync(
        InstalledEngine old,
        InstalledEngine placed,
        IReadOnlyList<InstalledEngine> installed,
        CancellationToken cancellationToken)
    {
        if (old.Record.Templates.Length == 0)
        {
            return new EngineUpdate(EngineUpdateOutcome.Updated, placed);
        }

        try
        {
            placed = await _templates.InstallAsync(placed, progress: null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is EngineInstallException or EngineCatalogueException or IOException)
        {
            return new EngineUpdate(EngineUpdateOutcome.Updated, placed, error.Message);
        }

        var others = installed.Where(engine => engine.Directory != old.Directory).Append(placed).ToList();

        if (old.Record.Templates != placed.Record.Templates
            && _templates.OwnedBy(old, others) is { } orphan)
        {
            try
            {
                _templates.Remove(orphan);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Left behind is a folder of disk, never a broken engine.
            }
        }

        return new EngineUpdate(EngineUpdateOutcome.Updated, placed);
    }
}
