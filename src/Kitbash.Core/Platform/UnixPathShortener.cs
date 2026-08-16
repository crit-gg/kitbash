using Kitbash.Core.IO;

namespace Kitbash.Core.Platform;

/// <summary>
/// Writes a path the way a shell does, on Linux and on macOS. The home directory collapses
/// to a tilde, which is where most of the saving comes from, since projects live under it.
/// </summary>
internal sealed class UnixPathShortener : PathShortener
{
    private readonly IEnvironment _environment;

    public UnixPathShortener(IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    protected override char Separator => '/';

    protected override string ToDisplay(string path)
    {
        // A backslash is a legal character in a name here, so it is left alone.
        var display = path.TrimEnd('/');

        return display.Length == 0 ? "/" : UnderHome(display);
    }

    protected override string RootOf(string display) =>
        display.StartsWith('/') ? "/" : string.Empty;

    private string UnderHome(string path)
    {
        var home = _environment.GetHomeDirectory().TrimEnd('/');

        if (home.Length == 0)
        {
            return path;
        }

        if (string.Equals(path, home, StringComparison.Ordinal))
        {
            return "~";
        }

        return path.StartsWith(home + "/", StringComparison.Ordinal)
            ? "~" + path[home.Length..]
            : path;
    }
}
