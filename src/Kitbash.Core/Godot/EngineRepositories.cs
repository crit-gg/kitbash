using System.Collections.Concurrent;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;

namespace Kitbash.Core.Godot;

internal sealed class EngineRepositories : IEngineRepositories
{
    private readonly EngineCatalogue _official;
    private readonly IWebContent _web;
    private readonly IFileSystem _files;
    private readonly ApplicationPaths _paths;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<EngineRepositoryAddress, IEngineRepository> _made = new();

    public EngineRepositories(
        EngineCatalogue official,
        IWebContent web,
        IFileSystem files,
        ApplicationPaths paths,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(official);
        ArgumentNullException.ThrowIfNull(web);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(time);

        _official = official;
        _web = web;
        _files = files;
        _paths = paths;
        _time = time;
    }

    public IEngineRepository For(EngineRepositoryAddress? address) =>
        address is null
            ? _official
            : _made.GetOrAdd(address, made => new GitHubEngineRepository(made, _web, _files, _paths, _time));
}
