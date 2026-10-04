using System.Security;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;

namespace Kitbash.Core.Godot;

internal sealed class EngineCommand : IEngineCommand
{
    private const string Name = "godot";

    // The program Kitbash last pointed the command at. An entry is ours only while it
    // still points there, so a link somebody made themselves is never replaced.
    private const string ProgramKey = "godot.command.program";

    // Whether the person has been asked to put the folder on PATH, so a refusal stays one.
    private const string AskedKey = "godot.command.asked";

    private readonly IGodotSettings _settings;
    private readonly IEngineStore _store;
    private readonly IEngineFiles _engineFiles;
    private readonly ICommandFolder _folder;
    private readonly IApplicationState _state;
    private readonly IPathRules _paths;
    private readonly SemaphoreSlim _turn = new(1, 1);

    public EngineCommand(
        IGodotSettings settings,
        IEngineStore store,
        IEngineFiles engineFiles,
        ICommandFolder folder,
        IApplicationState state,
        IPathRules paths)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(engineFiles);
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(paths);

        _settings = settings;
        _store = store;
        _engineFiles = engineFiles;
        _folder = folder;
        _state = state;
        _paths = paths;
    }

    public async Task<EngineCommandState> SyncAsync(CancellationToken cancellation)
    {
        await _turn.WaitAsync(cancellation).ConfigureAwait(false);

        try
        {
            return await SyncCoreAsync(cancellation).ConfigureAwait(false);
        }
        catch (Exception exception) when (Survivable(exception))
        {
            return new EngineCommandState(EngineCommandKind.Failed, _folder.PathOf(Name), Failure: exception.Message);
        }
        finally
        {
            _turn.Release();
        }
    }

    private async Task<EngineCommandState> SyncCoreAsync(CancellationToken cancellation)
    {
        var command = _folder.PathOf(Name);

        if (!_settings.DefaultOnPath)
        {
            TakeBack();
            _folder.Unreach();
            Forget(AskedKey);

            return new EngineCommandState(EngineCommandKind.Off, command);
        }

        if (!_folder.CanWrite || command is null)
        {
            return new EngineCommandState(EngineCommandKind.Unavailable, command);
        }

        var engine = await DefaultAsync(cancellation).ConfigureAwait(false);

        if (engine is null)
        {
            TakeBack();

            return new EngineCommandState(EngineCommandKind.NoDefault, command);
        }

        var program = _engineFiles.CommandFor(engine.Executable);
        var entry = _folder.Read(Name);

        if (entry.Exists && !IsOurs(entry))
        {
            return new EngineCommandState(EngineCommandKind.Conflict, command, engine);
        }

        if (entry.Program is null || !_paths.AreSame(entry.Program, program))
        {
            _folder.Write(Name, program);
        }

        _state.Set(SettingsScope.Global, ProgramKey, program);

        var asked = _state.Global.Get(AskedKey, false);
        var reach = await _folder.ReachAsync(Name, mayAsk: !asked, cancellation).ConfigureAwait(false);

        if (reach.Path is PathReach.Added or PathReach.Declined)
        {
            _state.Set(SettingsScope.Global, AskedKey, true);
        }

        return new EngineCommandState(EngineCommandKind.Placed, command, engine, reach);
    }

    /// <summary>
    /// The default install, read from where Kitbash puts an official build before the whole
    /// list is walked, since the list measures every engine's size.
    /// </summary>
    private async Task<InstalledEngine?> DefaultAsync(CancellationToken cancellation)
    {
        if (_settings.DefaultEngine is not { } id)
        {
            return null;
        }

        var engine = _store.ReadAt(Path.Combine(_settings.EngineDirectory, id.DirectoryName)) is { } local && local.Id == id
            ? local
            : (await _store.ReadAsync(cancellation).ConfigureAwait(false)).FirstOrDefault(found => found.Id == id);

        return engine is { IsMissing: false } ? engine : null;
    }

    private bool IsOurs(CommandEntry entry) =>
        entry.Program is { } program
        && _state.Global.Get(ProgramKey, string.Empty) is { Length: > 0 } recorded
        && _paths.AreSame(program, recorded);

    /// <summary>Removes the command when it is still the one Kitbash wrote.</summary>
    private void TakeBack()
    {
        var entry = _folder.Read(Name);

        if (entry.Exists && IsOurs(entry))
        {
            _folder.Remove(Name);
        }

        Forget(ProgramKey);
    }

    private void Forget(string key)
    {
        if (_state.Global.Contains(key))
        {
            _state.Apply(SettingsScope.Global, [SettingsEdit.Remove(key)]);
        }
    }

    private static bool Survivable(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or SecurityException
            or SettingsFileUnreadableException
            or InvalidOperationException
            or ProcessStartException;
}
