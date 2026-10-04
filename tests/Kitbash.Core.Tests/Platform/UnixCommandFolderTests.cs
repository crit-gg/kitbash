using Microsoft.Extensions.DependencyInjection;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;

namespace Kitbash.Core.Tests.Platform;

/// <summary>
/// The per user bin folder, over a real home in a temporary folder. The login shell is a
/// fake runner, so no profile on this machine is read.
/// </summary>
public sealed class UnixCommandFolderTests : IDisposable
{
    public static bool OnUnix => !OperatingSystem.IsWindows();

    public static bool OnLinux => OperatingSystem.IsLinux();

    private const string UnixOnly = "Windows writes a shim rather than a link.";

    private const string LinuxOnly = "macOS asks for an administrator when the folder is missing.";

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "kitbash-commands-" + Guid.NewGuid().ToString("n"));

    private readonly List<ServiceProvider> _providers = [];

    private string Home => Path.Combine(_root, "home");

    private string Bin => Path.Combine(Home, ".local", "bin");

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact(Skip = UnixOnly, SkipUnless = nameof(OnUnix))]
    public void TheFolderIsTheBinUnderHome()
    {
        Assert.Equal(Bin, Folder().Directory);
    }

    [Fact(Skip = UnixOnly, SkipUnless = nameof(OnUnix))]
    public void AnAbsoluteXdgBinHomeWins()
    {
        var elsewhere = Path.Combine(_root, "elsewhere");

        Assert.Equal(elsewhere, Folder(new() { ["XDG_BIN_HOME"] = elsewhere }).Directory);
    }

    [Fact(Skip = UnixOnly, SkipUnless = nameof(OnUnix))]
    public void ARelativeXdgBinHomeIsIgnored()
    {
        Assert.Equal(Bin, Folder(new() { ["XDG_BIN_HOME"] = "relative/bin" }).Directory);
    }

    [Fact(Skip = UnixOnly, SkipUnless = nameof(OnUnix))]
    public void NoHomeMeansNowhereToWrite()
    {
        var folder = Folder(home: string.Empty);

        Assert.Null(folder.Directory);
        Assert.False(folder.CanWrite);
        Assert.Equal(CommandEntry.Absent, folder.Read("godot"));
    }

    [Fact(Skip = UnixOnly, SkipUnless = nameof(OnUnix))]
    public void WritingMakesTheFolderAndALinkToTheProgram()
    {
        var folder = Folder();
        var program = Program("one");

        folder.Write("godot", program);

        Assert.Equal(program, File.ResolveLinkTarget(Path.Combine(Bin, "godot"), returnFinalTarget: false)!.FullName);
        Assert.Equal(new CommandEntry(true, program), folder.Read("godot"));
        Assert.Single(Directory.EnumerateFileSystemEntries(Bin));
    }

    [Fact(Skip = UnixOnly, SkipUnless = nameof(OnUnix))]
    public void WritingAgainReplacesTheLink()
    {
        var folder = Folder();

        folder.Write("godot", Program("one"));
        folder.Write("godot", Program("two"));

        Assert.Equal(Program("two"), folder.Read("godot").Program);
    }

    [Fact(Skip = UnixOnly, SkipUnless = nameof(OnUnix))]
    public void ALinkWhoseProgramHasGoneIsStillReadAndRemoved()
    {
        var folder = Folder();
        var program = Program("one");

        folder.Write("godot", program);
        File.Delete(program);

        Assert.Equal(new CommandEntry(true, program), folder.Read("godot"));

        folder.Remove("godot");

        Assert.Equal(CommandEntry.Absent, folder.Read("godot"));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Bin));
    }

    [Fact(Skip = UnixOnly, SkipUnless = nameof(OnUnix))]
    public void APlainFileIsThereButNotShapedLikeOurs()
    {
        Directory.CreateDirectory(Bin);
        File.WriteAllText(Path.Combine(Bin, "godot"), "#!/bin/sh\n");

        Assert.Equal(new CommandEntry(true, null), Folder().Read("godot"));
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task TheLoginShellSaysTheFolderIsOnPath()
    {
        var shell = new FakeShell($"profile noise\n\nKITBASH_PATH={Bin}:/usr/bin\n");
        var reach = await Folder(shell: shell).ReachAsync("godot", mayAsk: true, CancellationToken.None);

        Assert.Equal(new CommandReach(PathReach.OnPath, null), reach);
        Assert.Equal(["-l", "-i", "-c"], shell.Asked!.Arguments.Take(3));
        Assert.Equal(string.Empty, shell.Asked.StandardInput);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task AFolderTheShellDoesNotSearchIsReportedAndNothingIsEdited()
    {
        var shell = new FakeShell("KITBASH_PATH=/usr/bin:/bin\n");
        var reach = await Folder(shell: shell).ReachAsync("godot", mayAsk: true, CancellationToken.None);

        Assert.Equal(PathReach.NotOnPath, reach.Path);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task AProgramOfTheSameNameEarlierOnPathIsNamed()
    {
        var earlier = Path.Combine(_root, "earlier");
        Directory.CreateDirectory(earlier);
        var other = Executable(Path.Combine(earlier, "godot"));

        var shell = new FakeShell($"KITBASH_PATH={earlier}:{Bin}\n");
        var reach = await Folder(shell: shell).ReachAsync("godot", mayAsk: false, CancellationToken.None);

        Assert.Equal(new CommandReach(PathReach.OnPath, other), reach);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task AProgramOfTheSameNameLaterOnPathIsNotAShadow()
    {
        var later = Path.Combine(_root, "later");
        Directory.CreateDirectory(later);
        Executable(Path.Combine(later, "godot"));

        var shell = new FakeShell($"KITBASH_PATH={Bin}:{later}\n");
        var reach = await Folder(shell: shell).ReachAsync("godot", mayAsk: false, CancellationToken.None);

        Assert.Null(reach.Shadow);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task AShellThatWillNotStartFallsBackToTheInheritedPath()
    {
        var shell = new FakeShell(fails: true);
        var folder = Folder(new() { ["PATH"] = $"/usr/bin:{Bin}" }, shell: shell);

        var reach = await folder.ReachAsync("godot", mayAsk: true, CancellationToken.None);

        Assert.Equal(PathReach.OnPath, reach.Path);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task AShellThatPrintsNoPathFallsBackToTheInheritedPath()
    {
        var shell = new FakeShell("nothing useful\n");
        var folder = Folder(new() { ["PATH"] = "/usr/bin" }, shell: shell);

        var reach = await folder.ReachAsync("godot", mayAsk: true, CancellationToken.None);

        Assert.Equal(PathReach.NotOnPath, reach.Path);
    }

    private string Program(string name)
    {
        var directory = Path.Combine(_root, "engines", name);
        Directory.CreateDirectory(directory);

        return Executable(Path.Combine(directory, "Godot_v4.7.1-stable_linux.x86_64"));
    }

    private static string Executable(string path)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "#!/bin/sh\n");
            new FileSystem().MakeExecutableFile(path);
        }

        return path;
    }

    private ICommandFolder Folder(
        Dictionary<string, string>? variables = null, string? home = null, FakeShell? shell = null)
    {
        variables ??= [];

        if (shell is not null)
        {
            variables["SHELL"] = Executable(Path.Combine(Directory.CreateDirectory(_root).FullName, "shell"));
        }

        var services = new ServiceCollection();
        services.AddSingleton<IEnvironment>(new FakeEnvironment(variables, home ?? Home));
        services.AddSingleton<IProcessRunner>(shell ?? new FakeShell(fails: true));
        services.AddKitbashPlatform();

        var provider = services.BuildServiceProvider();
        _providers.Add(provider);

        return provider.GetRequiredService<ICommandFolder>();
    }

    private sealed class FakeEnvironment(Dictionary<string, string> variables, string home) : IEnvironment
    {
        public string? GetVariable(string name) => variables.TryGetValue(name, out var value) ? value : null;

        public string GetHomeDirectory() => home;

        public IReadOnlyList<string> GetProcessCommand() => [];
    }

    /// <summary>Answers for a login shell, or refuses to start like a shell that is not there.</summary>
    private sealed class FakeShell(string output = "", bool fails = false) : IProcessRunner
    {
        public ProcessRequest? Asked { get; private set; }

        public void Run(ProcessRequest request) => throw new NotSupportedException();

        public Task<ProcessOutput> ReadAsync(ProcessRequest request, CancellationToken cancellation = default)
        {
            Asked = request;

            return fails
                ? throw new ProcessStartException(request.FileName, new InvalidOperationException("No shell."))
                : Task.FromResult(new ProcessOutput(0, output, string.Empty));
        }

        public Task<ProcessOutput> ReadLinesAsync(
            ProcessRequest request, Action<string> onLine, CancellationToken cancellation = default) =>
            throw new NotSupportedException();
    }
}
