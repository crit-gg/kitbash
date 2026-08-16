using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Core.Tests.Platform;

/// <summary>
/// Starting something that outlives Kitbash. There is no setsid here, so a bundle goes
/// through LaunchServices and belongs to launchd instead.
/// </summary>
public sealed class MacDetachTests
{
    public static bool OnMac => OperatingSystem.IsMacOS();

    private const string MacOnly = "Only macOS starts a bundle through open.";

    private const string Bundle = "/Applications/Godot.app";

    private static readonly string Inner = $"{Bundle}/Contents/MacOS/Godot";

    /// <summary>
    /// The request that reached the runner. Built over a real temporary bundle, since the
    /// rewrite checks the program is there before it trusts the shape of the path.
    /// </summary>
    private static ProcessRequest Detached(Func<string, ProcessRequest> request)
    {
        var root = Path.Combine(Path.GetTempPath(), "kitbash-detach-" + Guid.NewGuid().ToString("n"));
        var executables = Path.Combine(root, "Godot.app", "Contents", "MacOS");

        Directory.CreateDirectory(executables);

        var program = Path.Combine(executables, "Godot");

        File.WriteAllText(program, string.Empty);

        // Through the seam, which already knows the mode means nothing on Windows.
        new FileSystem().MakeExecutableFile(program);

        try
        {
            var runner = new FakeRunner();
            var services = new ServiceCollection();

            services.AddSingleton<IProcessRunner>(runner);
            services.AddKitbashPlatform();

            using var provider = services.BuildServiceProvider();

            provider.GetRequiredService<IPlatformServices>().StartDetached(request(program));

            Assert.NotNull(runner.Started);

            return runner.Started;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void ABundleIsStartedThroughOpen()
    {
        var request = Detached(program => ProcessRequest.Command(program, "--path", "/work", "--editor"));

        Assert.Equal("/usr/bin/open", request.FileName);

        // -n or a second open would raise the running copy and drop everything after --args.
        Assert.Equal(["-n", "-a"], request.Arguments.Take(2));
        Assert.EndsWith("Godot.app", request.Arguments[2], StringComparison.Ordinal);
        Assert.Equal(["--args", "--path", "/work", "--editor"], request.Arguments.Skip(3));
    }

    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void ABundleWithNoArgumentsCarriesNoArgsSeparator()
    {
        var request = Detached(program => ProcessRequest.Command(program));

        Assert.DoesNotContain("--args", request.Arguments);
    }

    /// <summary>open cannot set one, so a request naming it is left alone rather than losing it.</summary>
    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void AWorkingDirectoryStopsTheRewrite()
    {
        var request = Detached(program => ProcessRequest.CommandIn("/work", program));

        Assert.NotEqual("/usr/bin/open", request.FileName);
    }

    /// <summary>The overlay would land on open rather than on the program it starts.</summary>
    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void AnEnvironmentStopsTheRewrite()
    {
        var request = Detached(program =>
            ProcessRequest.Command(program).With(new Dictionary<string, string> { ["A"] = "b" }));

        Assert.NotEqual("/usr/bin/open", request.FileName);
    }

    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void APlainProgramRunsAsItIs()
    {
        var services = new ServiceCollection();
        var runner = new FakeRunner();

        services.AddSingleton<IProcessRunner>(runner);
        services.AddKitbashPlatform();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IPlatformServices>()
            .StartDetached(ProcessRequest.Command("/bin/echo", "hello"));

        Assert.Equal("/bin/echo", runner.Started?.FileName);
    }

    /// <summary>
    /// A path shaped like a bundle whose program is not there is not rewritten, because open
    /// reports that in an exit code nothing reads where starting it directly throws.
    /// </summary>
    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void ABundleThatIsNotThereIsNotRewritten()
    {
        var services = new ServiceCollection();
        var runner = new FakeRunner();

        services.AddSingleton<IProcessRunner>(runner);
        services.AddKitbashPlatform();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IPlatformServices>().StartDetached(ProcessRequest.Command(Inner));

        Assert.Equal(Inner, runner.Started?.FileName);
    }

    private sealed class FakeRunner : IProcessRunner
    {
        public ProcessRequest? Started { get; private set; }

        public void Run(ProcessRequest request) => Started = request;

        public Task<ProcessOutput> ReadAsync(
            ProcessRequest request,
            CancellationToken cancellation = default) => throw new NotSupportedException();

        public Task<ProcessOutput> ReadLinesAsync(
            ProcessRequest request,
            Action<string> onLine,
            CancellationToken cancellation = default) => throw new NotSupportedException();
    }
}
