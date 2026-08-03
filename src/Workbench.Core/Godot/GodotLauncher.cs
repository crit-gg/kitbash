using Workbench.Core.IO;
using Workbench.Core.Platform;
using Workbench.Core.Settings;

namespace Workbench.Core.Godot;

/// <summary>
/// Opens a project, building and importing first when either is needed.
/// </summary>
/// <remarks>
/// <para>
/// The flags are read off <c>--help</c> on 4.7.1 rather than remembered.
/// <c>--build-solutions</c> is "Build the scripting solutions (e.g. for C# projects).
/// Implies --editor and requires a valid project to edit", and <c>--import</c> is "Starts
/// the editor, waits for any resources to be imported, and then quits". Both start the
/// editor, so both take <c>--headless</c> and neither puts a window on screen.
/// </para>
/// <para>
/// **The build and the import are waited for and the editor is not.** The first two have
/// to finish before the third is worth starting, and the editor is the thing a person is
/// waiting for, so it is handed to <see cref="IPlatformServices.StartDetached"/> and
/// outlives Workbench.
/// </para>
/// <para>
/// **A step that fails stops the whole thing, and the editor never opens.** That is the
/// point of building first. Opening anyway would put somebody in an editor whose
/// assemblies are stale or missing, which is the failure the build is there to catch, and
/// it would do it silently because the editor itself has no idea a build was attempted.
/// So this throws and nothing further runs. Turning the build off is a setting and a
/// deliberate act, not something a failure talks anybody into.
/// </para>
/// <para>
/// **The import runs before the editor rather than being left to it.** Godot imports on
/// open by itself, so this is not work the editor would skip. Doing it here means the
/// wait happens under a dialog that says what is happening and can be cancelled, instead
/// of the editor sitting on a splash screen for minutes on a fresh clone. It is skipped
/// outright when nothing needs importing, which is every open after the first.
/// </para>
/// </remarks>
internal sealed class GodotLauncher : IGodotLauncher
{
    private const string DotnetProgram = "dotnet";

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
        IProgress<GodotLaunchStep> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(progress);

        progress.Report(new GodotLaunchStep(GodotLaunchStage.Checking));

        if (BuildToolFor(engine, project) is { } tool)
        {
            await BuildAsync(tool, engine, project, progress, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (_imports.NeedsImport(project))
        {
            // The detail line is mono, so it carries a value and never a sentence. An
            // import has no count to report, so it carries the folder being read.
            progress.Report(new GodotLaunchStep(GodotLaunchStage.Importing, project.Directory));

            await RunAsync(
                GodotLaunchStage.Importing,
                "The import did not finish.",
                ProcessRequest.CommandIn(
                    project.Directory,
                    engine.Executable,
                    "--path", project.Directory, "--import", "--headless"),
                cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        progress.Report(new GodotLaunchStep(GodotLaunchStage.Starting));

        try
        {
            _platform.StartDetached(ProcessRequest.Command(
                engine.Executable, "--path", project.Directory, "--editor"));
        }
        catch (ProcessStartException exception)
        {
            throw new GodotLaunchException(
                GodotLaunchStage.Starting, "The editor would not start.", exception);
        }
    }

    /// <summary>
    /// Which program builds this project's C#, or null when nothing should.
    /// </summary>
    /// <remarks>
    /// **A plain engine never builds.** A project's C# only builds against the .NET build
    /// of the engine, so asking a plain one to do it fails and asking dotnet to do it
    /// produces assemblies that engine will not load. When the two disagree the strip has
    /// already said so, and opening anyway means opening what is there.
    /// </remarks>
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
        CancellationToken cancellationToken)
    {
        ProcessOutput output;

        try
        {
            output = await _processes.ReadAsync(request, cancellationToken).ConfigureAwait(false);
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
