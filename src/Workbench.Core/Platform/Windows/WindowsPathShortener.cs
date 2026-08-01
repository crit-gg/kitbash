using Workbench.Core.IO;

namespace Workbench.Core.Platform.Windows;

/// <summary>
/// Writes a path the way Windows writes one. There is no home shorthand here, so the
/// drive or the share stays and the middle collapses, which is what the shell does.
/// </summary>
internal sealed class WindowsPathShortener : PathShortener
{
    protected override char Separator => '\\';

    protected override string ToDisplay(string path)
    {
        // A forward slash is an accepted separator here and never part of a name.
        var display = path.Replace('/', '\\').TrimEnd('\\');

        // "C:" on its own names the current directory on that drive, not its root.
        return display.Length == 2 && display[1] == ':' ? display + '\\' : display;
    }

    protected override string RootOf(string display)
    {
        // A drive falls out as the first segment, so it needs no root of its own.
        if (!display.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        // A share is one place, so the server and the share name stay together.
        var share = display.IndexOf('\\', 2);

        if (share < 0)
        {
            return display;
        }

        var rest = display.IndexOf('\\', share + 1);

        return rest < 0 ? display : display[..(rest + 1)];
    }
}
