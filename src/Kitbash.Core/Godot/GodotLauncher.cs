using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;

namespace Kitbash.Core.Godot;

/// <summary>
/// Opens a project, building and importing first when either is needed.
/// </summary>
internal sealed class GodotLauncher : IGodotLauncher
{
    private const string DotnetProgram = "dotnet";

    /// <summary>Everything Godot generates for a project, and everything a rebuild drops.</summary>
    private const string CacheDirectoryName = ".godot";

    /// <summary>
    /// The files under the cache a person arranged rather than Godot generated. Relative
    /// to the cache folder, and Godot writes both when the editor closes.
    /// </summary>
    private static readonly string[] KeptFiles =
    [
        "editor/editor_layout.cfg",
        "editor/project_metadata.cfg",
    ];

    private readonly IProcessRunner _processes;
    private readonly IPlatformServices _platform;
    private readonly IGodotImports _imports;
    private readonly IGodotSettings _settings;
    private readonly IExternalTools _tools;
    private readonly IFileSystem _fileSystem;

    public GodotLauncher(
        IProcessRunner processes,
        IPlatformServices platform,
        IGodotImports imports,
        IGodotSettings settings,
        IExternalTools tools,
        IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(imports);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _processes = processes;
        _platform = platform;
        _imports = imports;
        _settings = settings;
        _tools = tools;
        _fileSystem = fileSystem;
    }

    public async Task OpenAsync(
        InstalledEngine engine,
        GodotProject project,
        GodotLaunchMode mode,
        IProgress<GodotLaunchStep> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(progress);

        progress.Report(new GodotLaunchStep(GodotLaunchStage.Checking));

        // Taken before the delete and put back after everything, so a rebuild costs a
        // person the cache and never the way they had the editor arranged.
        var kept = mode == GodotLaunchMode.Rebuild ? Clean(project, progress) : [];

        try
        {
            await RebuildAsync(engine, project, mode, progress, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            // In a finally, so a rebuild that failed or was cancelled does not take these
            // with it as well. There is nothing to put back when there was none.
            Restore(project, kept);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // A rebuild is the work and not a way of getting somewhere, so it stops here.
        // Whoever asked for it can open the editor next, and that is their press to make.
        if (mode == GodotLaunchMode.Rebuild)
        {
            return;
        }

        progress.Report(new GodotLaunchStep(GodotLaunchStage.Starting));

        // Running is no flag at all, so editing is the one that adds an argument.
        string[] arguments = mode == GodotLaunchMode.Editor
            ? ["--path", project.Directory, "--editor"]
            : ["--path", project.Directory];

        try
        {
            _platform.StartDetached(ProcessRequest.Command(engine.Executable, arguments));
        }
        catch (ProcessStartException exception)
        {
            throw new GodotLaunchException(
                GodotLaunchStage.Starting,
                mode == GodotLaunchMode.Editor
                    ? "The editor would not start."
                    : "The project would not start.",
                exception);
        }
    }

    /// <summary>Everything that has to finish before anything is started.</summary>
    private async Task RebuildAsync(
        InstalledEngine engine,
        GodotProject project,
        GodotLaunchMode mode,
        IProgress<GodotLaunchStep> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (BuildToolFor(engine, project) is { } tool)
        {
            await BuildAsync(tool, engine, project, progress, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // A rebuild imports whatever it finds, since it just threw the cache away and
        // asking whether one is needed would be asking a question with a known answer.
        if (mode == GodotLaunchMode.Rebuild || _imports.NeedsImport(project))
        {
            progress.Report(new GodotLaunchStep(GodotLaunchStage.Importing));

            // Godot reports its own progress while it works, so the bar is real rather
            // than a spinner. A line that cannot be read reports nothing and the bar goes
            // back to indeterminate, which is what an older engine would give throughout.
            var reader = new GodotProgressReader();

            await RunAsync(
                GodotLaunchStage.Importing,
                "The import did not finish.",
                ProcessRequest.CommandIn(
                    project.Directory,
                    engine.Executable,
                    "--path", project.Directory, "--import", "--headless"),
                cancellationToken,
                line =>
                {
                    if (reader.Read(line) is { } step)
                    {
                        progress.Report(step);
                    }
                }).ConfigureAwait(false);

            progress.Report(reader.Finished());
        }
    }

    /// <summary>
    /// Deletes the import cache, so the import that follows makes all of it again.
    /// </summary>
    /// <returns>The kept files that were there, to be put back afterwards.</returns>
    private IReadOnlyList<KeptFile> Clean(GodotProject project, IProgress<GodotLaunchStep> progress)
    {
        var cache = Path.Combine(project.Directory, CacheDirectoryName);

        if (!_fileSystem.DirectoryExists(cache))
        {
            return [];
        }

        progress.Report(new GodotLaunchStep(GodotLaunchStage.Cleaning, cache));

        List<KeptFile> kept = [];

        foreach (var name in KeptFiles)
        {
            var file = CacheFile(project, name);

            if (Read(file) is { } text)
            {
                kept.Add(new KeptFile(file, text));
            }
        }

        try
        {
            _fileSystem.DeleteDirectory(cache);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new GodotLaunchException(
                GodotLaunchStage.Cleaning, "The import cache could not be deleted.", exception);
        }

        return kept;
    }

    /// <summary>
    /// Puts the kept files back where the cache used to be.
    /// </summary>
    private void Restore(GodotProject project, IReadOnlyList<KeptFile> kept)
    {
        foreach (var file in kept)
        {
            try
            {
                _fileSystem.CreateDirectory(Path.GetDirectoryName(file.Path)!);
                _fileSystem.WriteAllText(file.Path, file.Text);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>A file under the cache, named with forward slashes.</summary>
    private static string CacheFile(GodotProject project, string name) =>
        Path.Combine([project.Directory, CacheDirectoryName, .. name.Split('/')]);

    /// <summary>One file taken out of the cache before the delete.</summary>
    private readonly record struct KeptFile(string Path, string Text);

    /// <summary>
    /// The file's text, or null when there is nothing to keep.
    /// </summary>
    private string? Read(string file)
    {
        try
        {
            return _fileSystem.FileExists(file) ? _fileSystem.ReadAllText(file) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Which program builds this project's C#, or null when nothing should.
    /// </summary>
    private GodotBuildTool? BuildToolFor(InstalledEngine engine, GodotProject project)
    {
        if (!project.UsesDotnet || !engine.IsMono)
        {
            return null;
        }

        return _settings.BuildTool switch
        {
            GodotBuildTool.None => null,
            GodotBuildTool.Dotnet => GodotBuildTool.Dotnet,
            GodotBuildTool.Editor => GodotBuildTool.Editor,

            // Auto. dotnet is preferred when this machine has one, since it is faster and
            // its failures read better. The editor needs nothing else installed.
            _ => _tools.Dotnet.IsAvailable ? GodotBuildTool.Dotnet : GodotBuildTool.Editor,
        };
    }

    private async Task BuildAsync(
        GodotBuildTool tool,
        InstalledEngine engine,
        GodotProject project,
        IProgress<GodotLaunchStep> progress,
        CancellationToken cancellationToken)
    {
        if (tool == GodotBuildTool.Dotnet)
        {
            // Nothing to build. A project can carry the C# feature and a [dotnet] section
            // with no solution written yet, and that is not a failure.
            if (BuildTarget(project) is not { } target)
            {
                return;
            }

            progress.Report(new GodotLaunchStep(
                GodotLaunchStage.Building, $"dotnet build {Path.GetFileName(target)}"));

            await RunAsync(
                GodotLaunchStage.Building,
                "The C# build failed.",
                ProcessRequest.CommandIn(
                    project.Directory,
                    _tools.Dotnet.Path ?? DotnetProgram,
                    "build", target, "--property", "WarningLevel=0"),
                cancellationToken).ConfigureAwait(false);

            return;
        }

        progress.Report(new GodotLaunchStep(
            GodotLaunchStage.Building, Path.GetFileName(engine.Executable)));

        // --build-solutions implies --editor and needs a project, so --quit is what makes
        // it exit once the build is done.
        await RunAsync(
            GodotLaunchStage.Building,
            "The C# build failed.",
            ProcessRequest.CommandIn(
                project.Directory,
                engine.Executable,
                "--path", project.Directory,
                "--build-solutions", "--quit", "--quiet", "--no-header", "--headless"),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The solution, or the project file when there is no solution.</summary>
    private string? BuildTarget(GodotProject project)
    {
        string? csproj = null;

        try
        {
            foreach (var file in _fileSystem.EnumerateFiles(project.Directory, recursive: false).Order(StringComparer.Ordinal))
            {
                if (file.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }

                csproj ??= file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ? file : null;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return csproj;
    }

    private async Task RunAsync(
        GodotLaunchStage stage,
        string message,
        ProcessRequest request,
        CancellationToken cancellationToken,
        Action<string>? onLine = null)
    {
        ProcessOutput output;

        try
        {
            output = onLine is null
                ? await _processes.ReadAsync(request, cancellationToken).ConfigureAwait(false)
                : await _processes.ReadLinesAsync(request, onLine, cancellationToken).ConfigureAwait(false);
        }
        catch (ProcessStartException exception)
        {
            throw new GodotLaunchException(stage, $"{request.FileName} could not be run.", exception);
        }

        if (output.ExitCode != 0)
        {
            throw new GodotLaunchException(stage, message, Combined(output));
        }
    }

    private static string Combined(ProcessOutput output)
    {
        var error = output.StandardError.TrimEnd();
        var standard = output.StandardOutput.TrimEnd();

        // The error stream first, since that is where the reason is and a build writes a
        // great deal to the other one.
        return string.Join(
            Environment.NewLine,
            new[] { error, standard }.Where(part => part.Length > 0));
    }
}
