using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;

namespace Kitbash.Tools;

public sealed class ToolRepositoryFactory : IToolRepositoryFactory
{
    private readonly IWebContent _web;
    private readonly IFileSystem _files;
    private readonly ApplicationPaths _paths;
    private readonly TimeProvider _time;

    public ToolRepositoryFactory(
        IWebContent web,
        IFileSystem files,
        ApplicationPaths paths,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(web);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(time);

        _web = web;
        _files = files;
        _paths = paths;
        _time = time;
    }

    public IToolRepository? For(ToolRepositorySource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Type switch
        {
            ToolRepositorySource.GitHub => GitHubToolRepository.For(source, _web, _files, _paths, _time),
            _ => null,
        };
    }
}
