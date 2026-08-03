using System.Diagnostics.CodeAnalysis;
using Kitbash.Core.Platform;

namespace Kitbash.Core.Godot;

/// <summary>
/// One downloadable editor file: which release, which platform, which processor, and
/// whether it is the .NET build.
/// </summary>
/// <param name="Id">The install this file would produce. Several builds share one.</param>
/// <param name="Platform">The platform this file runs on. Always the host's, in practice.</param>
/// <param name="Architecture">The processor this file runs on.</param>
/// <param name="FileName">The published file name.</param>
/// <param name="Address">Where to fetch it. Stable, unlike the signed URL it redirects to.</param>
public sealed record EngineBuild(
    EngineId Id,
    EnginePlatform Platform,
    EngineArchitecture Architecture,
    string FileName,
    WebAddress Address)
{
    private const string Downloads = "https://github.com/godotengine/godot-builds/releases/download";

    // Every desktop editor Godot 4 has published. Two Linux spellings and one macOS
    // spelling are here only for the 4.0 alphas, before the current names settled.
    private static readonly string[] Order = ["Windows", "Linux", "macOS", "Web", "Android"];

    private static readonly Dictionary<string, (EnginePlatform Platform, EngineArchitecture Architecture, bool Mono)> Shapes =
        new(StringComparer.Ordinal)
        {
            ["_win32.exe.zip"] = (EnginePlatform.Windows, EngineArchitecture.X86, false),
            ["_win64.exe.zip"] = (EnginePlatform.Windows, EngineArchitecture.X64, false),
            ["_windows_arm64.exe.zip"] = (EnginePlatform.Windows, EngineArchitecture.Arm64, false),
            ["_mono_win32.zip"] = (EnginePlatform.Windows, EngineArchitecture.X86, true),
            ["_mono_win64.zip"] = (EnginePlatform.Windows, EngineArchitecture.X64, true),
            ["_mono_windows_arm64.zip"] = (EnginePlatform.Windows, EngineArchitecture.Arm64, true),

            ["_linux.32.zip"] = (EnginePlatform.Linux, EngineArchitecture.X86, false),
            ["_linux.64.zip"] = (EnginePlatform.Linux, EngineArchitecture.X64, false),
            ["_linux.x86_32.zip"] = (EnginePlatform.Linux, EngineArchitecture.X86, false),
            ["_linux.x86_64.zip"] = (EnginePlatform.Linux, EngineArchitecture.X64, false),
            ["_linux.arm32.zip"] = (EnginePlatform.Linux, EngineArchitecture.Arm32, false),
            ["_linux.arm64.zip"] = (EnginePlatform.Linux, EngineArchitecture.Arm64, false),
            ["_mono_linux_x86_32.zip"] = (EnginePlatform.Linux, EngineArchitecture.X86, true),
            ["_mono_linux_x86_64.zip"] = (EnginePlatform.Linux, EngineArchitecture.X64, true),
            ["_mono_linux_arm32.zip"] = (EnginePlatform.Linux, EngineArchitecture.Arm32, true),
            ["_mono_linux_arm64.zip"] = (EnginePlatform.Linux, EngineArchitecture.Arm64, true),

            ["_osx.universal.zip"] = (EnginePlatform.MacOS, EngineArchitecture.Universal, false),
            ["_macos.universal.zip"] = (EnginePlatform.MacOS, EngineArchitecture.Universal, false),
            ["_mono_macos.universal.zip"] = (EnginePlatform.MacOS, EngineArchitecture.Universal, true),
        };

    public EngineTag Tag => Id.Tag;

    public bool IsMono => Id.IsMono;

    /// <summary>
    /// One build from a name a release manifest published. False when the name belongs to
    /// that release but is not a desktop editor, and false when it belongs to another
    /// release entirely.
    /// </summary>
    public static bool TryRead(EngineTag tag, string fileName, [NotNullWhen(true)] out EngineBuild? build)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        build = null;

        var prefix = $"Godot_v{tag}";

        if (!fileName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!Shapes.TryGetValue(fileName[prefix.Length..], out var shape))
        {
            return false;
        }

        build = new EngineBuild(
            new EngineId(tag, shape.Mono),
            shape.Platform,
            shape.Architecture,
            fileName,
            WebAddress.Parse($"{Downloads}/{tag}/{fileName}"));

        return true;
    }

    /// <summary>
    /// Every target a release published for, named for display, in a fixed order. Includes
    /// targets Kitbash cannot install, since the caller reports where a release shipped.
    /// </summary>
    public static IReadOnlyList<string> PublishedTargets(EngineTag tag, IEnumerable<string> fileNames)
    {
        ArgumentNullException.ThrowIfNull(fileNames);

        var prefix = $"Godot_v{tag}";
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in fileNames)
        {
            if (!name.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var suffix = name[prefix.Length..];

            if (Shapes.TryGetValue(suffix, out var shape))
            {
                seen.Add(TextFor(shape.Platform));
            }
            else if (suffix.StartsWith("_web", StringComparison.Ordinal))
            {
                seen.Add("Web");
            }
            else if (suffix.StartsWith("_android", StringComparison.Ordinal))
            {
                seen.Add("Android");
            }
        }

        // Export templates, the Android libraries, the debug symbols and the source
        // tarball are not a target anyone downloads an editor from, so they add nothing.
        return Order.Where(seen.Contains).ToList();
    }

    /// <summary>Every desktop editor among the names one release manifest published.</summary>
    public static IReadOnlyList<EngineBuild> ReadAll(EngineTag tag, IEnumerable<string> fileNames)
    {
        ArgumentNullException.ThrowIfNull(fileNames);

        var builds = new List<EngineBuild>();

        foreach (var name in fileNames)
        {
            if (TryRead(tag, name, out var build))
            {
                builds.Add(build);
            }
        }

        builds.Sort(Sort);

        return builds;
    }

    /// <summary>How Godot writes this processor, such as <c>x86_64</c>.</summary>
    public string ArchitectureText => TextFor(Architecture);

    /// <summary>
    /// How Godot writes a processor. Here rather than anywhere else because this type
    /// already owns every other piece of Godot's naming.
    /// </summary>
    public static string TextFor(EngineArchitecture architecture) => architecture switch
    {
        EngineArchitecture.X64 => "x86_64",
        EngineArchitecture.X86 => "x86_32",
        EngineArchitecture.Arm64 => "arm64",
        EngineArchitecture.Arm32 => "arm32",
        _ => "universal",
    };

    /// <summary>Reads back what <see cref="TextFor"/> writes.</summary>
    public static bool TryReadArchitecture(string? text, out EngineArchitecture architecture)
    {
        switch (text)
        {
            case "x86_64":
                architecture = EngineArchitecture.X64;

                return true;

            case "x86_32":
                architecture = EngineArchitecture.X86;

                return true;

            case "arm64":
                architecture = EngineArchitecture.Arm64;

                return true;

            case "arm32":
                architecture = EngineArchitecture.Arm32;

                return true;

            case "universal":
                architecture = EngineArchitecture.Universal;

                return true;

            default:
                architecture = default;

                return false;
        }
    }

    /// <summary>The platform as a person reads it.</summary>
    public string PlatformText => TextFor(Platform);

    private static string TextFor(EnginePlatform platform) => platform switch
    {
        EnginePlatform.Windows => "Windows",
        EnginePlatform.Linux => "Linux",
        _ => "macOS",
    };

    private static int Sort(EngineBuild left, EngineBuild right)
    {
        var by = left.Platform.CompareTo(right.Platform);

        if (by != 0)
        {
            return by;
        }

        by = left.Architecture.CompareTo(right.Architecture);

        return by != 0 ? by : left.IsMono.CompareTo(right.IsMono);
    }
}
