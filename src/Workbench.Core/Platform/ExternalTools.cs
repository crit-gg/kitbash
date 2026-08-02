using Workbench.Core.IO;
using Workbench.Core.Settings;
using Workbench.Core.Settings.Schema;

namespace Workbench.Core.Platform;

internal sealed class ExternalTools : IExternalTools
{
    private readonly ExternalToolsSettingsSchema _schema;
    private readonly IApplicationSettings _settings;
    private readonly IExecutableFinder _executables;
    private readonly IFileSystem _fileSystem;

    private readonly Lock _gate = new();
    private ExternalTool? _git;
    private ExternalTool? _dotnet;

    public ExternalTools(
        ExternalToolsSettingsSchema schema,
        IApplicationSettings settings,
        IExecutableFinder executables,
        IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(executables);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _schema = schema;
        _settings = settings;
        _executables = executables;
        _fileSystem = fileSystem;
    }

    public ExternalTool Git => Resolve(ref _git, "git", _schema.GitPath);

    public ExternalTool Dotnet => Resolve(ref _dotnet, "dotnet", _schema.DotnetPath);

    private ExternalTool Resolve(ref ExternalTool? held, string name, SettingDescriptor<string> descriptor)
    {
        lock (_gate)
        {
            return held ??= Look(name, descriptor);
        }
    }

    /// <summary>
    /// An override that points at nothing runnable falls back to PATH rather than taking
    /// the program away, since a bad override should not be worse than no override. It is
    /// reported rather than swallowed, so something can say the choice is being ignored.
    /// </summary>
    private ExternalTool Look(string name, SettingDescriptor<string> descriptor)
    {
        var stored = descriptor.Read(_settings.Global);
        var configured = string.IsNullOrWhiteSpace(stored) ? null : stored;

        if (configured is not null && _fileSystem.IsExecutableFile(configured))
        {
            return new ExternalTool(name, configured, configured, ConfiguredIsMissing: false);
        }

        return new ExternalTool(name, _executables.Find(name), configured, configured is not null);
    }
}
