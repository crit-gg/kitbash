using System.Reflection;
using Velopack.Locators;

namespace Kitbash.Updates;

/// <summary>
/// What Velopack installed, and the build's own version when nothing installed it.
/// </summary>
public sealed class ApplicationVersion : IApplicationVersion
{
    private readonly Lazy<(string Version, bool Installed)> _resolved;

    public ApplicationVersion()
    {
        _resolved = new Lazy<(string, bool)>(Resolve);
    }

    public string Current => _resolved.Value.Version;

    public bool IsInstalled => _resolved.Value.Installed;

    private static (string Version, bool Installed) Resolve()
    {
        if (Installed() is { } installed)
        {
            return (installed, true);
        }

        return (Assembled(), false);
    }

    /// <summary>
    /// Velopack's own answer. It throws rather than returning null when nothing called
    /// VelopackApp.Run, which is every harness that loads these types without starting
    /// the app.
    /// </summary>
    private static string? Installed()
    {
        try
        {
            return VelopackLocator.Current.CurrentlyInstalledVersion?.ToString();
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// The build's own version. The SDK appends the commit as build metadata, which is
    /// not part of the product version, so everything after the plus goes.
    /// </summary>
    private static string Assembled()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrEmpty(informational))
        {
            return string.Empty;
        }

        var metadata = informational.IndexOf('+', StringComparison.Ordinal);

        return metadata < 0 ? informational : informational[..metadata];
    }
}
