using Kitbash.Core.IO;

namespace Kitbash.Core.Platform.MacOS;

internal sealed class MacPlatform : DesktopPlatform
{
    /// <summary>LaunchServices. Part of the OS, and named absolutely so PATH cannot shadow it.</summary>
    private const string Opener = "/usr/bin/open";

    private const string BundleSuffix = ".app";

    private readonly IFileSystem _fileSystem;

    public MacPlatform(IFileSystem fileSystem, IProcessRunner processes)
        : base(fileSystem, processes)
    {
        _fileSystem = fileSystem;
    }

    public override PlatformKind Kind => PlatformKind.MacOS;

    // WebAddress and DirectoryLocation both refuse anything that is not absolute, so a
    // target can never begin with a dash and be read as a flag.
    protected override void Open(string target) =>
        Processes.Run(ProcessRequest.Command(Opener, target));

    /// <summary>
    /// There is no setsid here, so a bundle is started through LaunchServices instead and
    /// belongs to launchd rather than to us. Anything else runs as it is, which still
    /// outlives a launcher that exits.
    /// </summary>
    protected override ProcessRequest Detach(ProcessRequest request)
    {
        // open cannot set a working directory and would take the variables itself, so a
        // request naming either is left alone rather than quietly losing it.
        if (request.WorkingDirectory is not null
            || request.Environment is { Count: > 0 }
            || BundleOf(request.FileName) is not { } bundle)
        {
            return request;
        }

        // Without -n a second open of a running app raises its window and drops --args.
        string[] arguments = request.Arguments.Count == 0
            ? ["-n", "-a", bundle]
            : ["-n", "-a", bundle, "--args", .. request.Arguments];

        return request with { FileName = Opener, Arguments = arguments, UseShellExecute = false };
    }

    /// <summary>
    /// The bundle holding an executable, from <c>X.app/Contents/MacOS/program</c>, or null
    /// when the program does not sit in one.
    /// </summary>
    private string? BundleOf(string fileName)
    {
        // Checked before rewriting, because open reports a broken bundle in an exit code
        // nothing reads, where starting the program directly throws.
        if (!_fileSystem.IsExecutableFile(fileName))
        {
            return null;
        }

        var executables = Path.GetDirectoryName(fileName);
        var contents = Path.GetDirectoryName(executables);
        var bundle = Path.GetDirectoryName(contents);

        if (bundle is null
            || !Named(executables, "MacOS")
            || !Named(contents, "Contents")
            || !bundle.EndsWith(BundleSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return bundle;
    }

    // Case folded, for the same reason MacPathRules folds: the default filesystem here does.
    private static bool Named(string? directory, string name) =>
        string.Equals(Path.GetFileName(directory), name, StringComparison.OrdinalIgnoreCase);
}
