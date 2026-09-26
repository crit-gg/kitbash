using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kitbash.Core.Tests.Godot;

/// <summary>
/// A provider built the way the launcher builds one, over a temporary home, with the
/// network, the process runner and the process list faked. Files are real.
/// </summary>
internal sealed class EngineRepositoryHost : IDisposable
{
    public const string Api = "https://api.github.com/repos/crit-gg/godot-slopworks/releases?per_page=100";

    private readonly List<Published> _releases = [];

    public EngineRepositoryHost()
    {
        Home = Path.Combine(Path.GetTempPath(), "kitbash-engines-" + Guid.NewGuid().ToString("n"));
        Workspace = Path.Combine(Home, "workspace");

        var services = new ServiceCollection();

        // First, so the TryAdd calls inside the registrations leave them alone.
        services.TryAddSingleton<IUserDirectories>(new TestDirectories(Home));
        services.TryAddSingleton<IWebContent>(Web);
        services.TryAddSingleton<IProcessRunner>(Processes);
        services.TryAddSingleton<IRunningPrograms>(Running);

        Provider = services.AddKitbashEngines().BuildServiceProvider();

        Directory.CreateDirectory(Workspace);
    }

    public string Home { get; }

    public string Workspace { get; }

    public FakeWeb Web { get; } = new();

    public FakeProcesses Processes { get; } = new();

    public FakeRunning Running { get; } = new();

    public ServiceProvider Provider { get; }

    public T Get<T>() where T : notnull => Provider.GetRequiredService<T>();

    public string ExportTemplates => Get<IEngineFiles>().ExportTemplatesDirectory;

    /// <summary>The global config lists slopworks.</summary>
    public void ListGlobally(string name = "slopworks", string url = "https://github.com/crit-gg/godot-slopworks")
    {
        var file = Path.Combine(Home, "config", "kitbash.toml");

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.AppendAllText(file, $"[[godot.repositories]]\nname = \"{name}\"\ntype = \"github\"\nurl = \"{url}\"\n\n");
    }

    /// <summary>A .NET Godot project whose features name 4.7, and a team config.</summary>
    public void Project(string teamConfig)
    {
        File.WriteAllText(
            Path.Combine(Workspace, "project.godot"),
            "config_version=5\n\n[application]\n\nconfig/name=\"Game\"\n"
            + "config/features=PackedStringArray(\"4.7\", \"Forward Plus\")\n\n[dotnet]\n\nproject/assembly_name=\"Game\"\n");

        var team = Path.Combine(Workspace, ".kitbash", "config", "kitbash.toml");

        Directory.CreateDirectory(Path.GetDirectoryName(team)!);
        File.WriteAllText(team, teamConfig);
    }

    /// <summary>
    /// Publishes a release with an editor for every desktop shape and the export templates,
    /// then rewrites the list the API answers with.
    /// </summary>
    public void Publish(string tag, DateTimeOffset at, bool prerelease = false, bool draft = false, bool badDigest = false)
    {
        var editor = EditorZip();
        var templates = TemplatesZip("4.7.2.slopworks.mono");
        var assets = new List<(string Name, byte[] Bytes)>();

        foreach (var suffix in new[]
                 {
                     "_mono_linux_x86_64.zip", "_mono_linux_arm64.zip", "_mono_win64.zip",
                     "_mono_windows_arm64.zip", "_mono_macos.universal.zip",
                 })
        {
            assets.Add(($"Godot_v{tag}{suffix}", editor));
        }

        assets.Add(($"Godot_v{tag}_mono_export_templates.tpz", templates));

        foreach (var (name, bytes) in assets)
        {
            Web.Files[Download(tag, name)] = bytes;
        }

        _releases.Insert(0, new Published(tag, at, prerelease, draft, assets, badDigest));
        Web.Texts[Api] = ListJson();
    }

    /// <summary>A tag that is not a repository build, which is counted rather than listed.</summary>
    public void PublishUnreadable(string tag)
    {
        _releases.Add(new Published(tag, DateTimeOffset.UnixEpoch, false, false, [], false));
        Web.Texts[Api] = ListJson();
    }

    public void Dispose()
    {
        Provider.Dispose();

        try
        {
            Directory.Delete(Home, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private static string Download(string tag, string name) =>
        $"https://github.com/crit-gg/godot-slopworks/releases/download/{tag}/{name}";

    private string ListJson()
    {
        var list = _releases.Select(release => new Dictionary<string, object?>
        {
            ["tag_name"] = release.Tag,
            ["draft"] = release.Draft,
            ["prerelease"] = release.Prerelease,
            ["published_at"] = release.At.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["html_url"] = $"https://github.com/crit-gg/godot-slopworks/releases/tag/{release.Tag}",
            ["assets"] = release.Assets.Select(asset => new Dictionary<string, object?>
            {
                ["name"] = asset.Name,
                ["browser_download_url"] = Download(release.Tag, asset.Name),
                ["digest"] = "sha256:" + (release.BadDigest
                    ? new string('0', 64)
                    : Convert.ToHexStringLower(SHA256.HashData(asset.Bytes))),
            }).ToList(),
        });

        return JsonSerializer.Serialize(list);
    }

    /// <summary>
    /// A .NET editor archive the way the Linux one is shaped, a folder holding the editor
    /// and GodotSharp. It carries an executable for every platform's finder.
    /// </summary>
    private static byte[] EditorZip()
    {
        using var stream = new MemoryStream();

        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "Godot_mono/godot", "editor", executable: true);
            Add(zip, "Godot_mono/Godot.exe", "editor", executable: false);
            Add(zip, "Godot_mono/GodotSharp/Api/GodotSharp.dll", "api", executable: false);
        }

        return stream.ToArray();
    }

    private static byte[] TemplatesZip(string version)
    {
        using var stream = new MemoryStream();

        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "templates/version.txt", version + "\n", executable: false);
            Add(zip, "templates/linux_debug.x86_64", "template", executable: false);
        }

        return stream.ToArray();
    }

    private static void Add(ZipArchive zip, string name, string text, bool executable)
    {
        var entry = zip.CreateEntry(name);

        if (executable)
        {
            entry.ExternalAttributes = 0b111_101_101 << 16;
        }

        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);

        writer.Write(text);
    }

    private sealed record Published(
        string Tag,
        DateTimeOffset At,
        bool Prerelease,
        bool Draft,
        List<(string Name, byte[] Bytes)> Assets,
        bool BadDigest);

    private sealed class TestDirectories(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state", application);

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "run");
    }
}

/// <summary>Answers from a table and counts what was asked, so a test can see the cache work.</summary>
internal sealed class FakeWeb : IWebContent
{
    public Dictionary<string, string> Texts { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public List<string> Asked { get; } = [];

    public bool IsOffline { get; set; }

    public Task<string> ReadTextAsync(WebAddress address, CancellationToken cancellationToken)
    {
        Asked.Add(address.ToString());

        return !IsOffline && Texts.TryGetValue(address.ToString(), out var text)
            ? Task.FromResult(text)
            : throw new HttpRequestException($"{address} is not here.");
    }

    public Task<long?> MeasureAsync(WebAddress address, CancellationToken cancellationToken) =>
        Task.FromResult<long?>(Files.TryGetValue(address.ToString(), out var bytes) ? bytes.Length : null);

    public async Task DownloadAsync(
        WebAddress address,
        string path,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        if (IsOffline || !Files.TryGetValue(address.ToString(), out var bytes))
        {
            throw new HttpRequestException($"{address} is not here.");
        }

        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        progress?.Report(bytes.Length);
    }
}

/// <summary>Every editor says it is a slopworks build, which is what the store asks it.</summary>
internal sealed class FakeProcesses : IProcessRunner
{
    public void Run(ProcessRequest request) => throw new NotSupportedException();

    public Task<ProcessOutput> ReadAsync(ProcessRequest request, CancellationToken cancellation = default) =>
        Task.FromResult(new ProcessOutput(0, "4.7.2.slopworks.mono.custom_build.18d5d19\n", string.Empty));

    public Task<ProcessOutput> ReadLinesAsync(
        ProcessRequest request, Action<string> onLine, CancellationToken cancellation = default) =>
        throw new NotSupportedException();
}

internal sealed class FakeRunning : IRunningPrograms
{
    public bool Everything { get; set; }

    public bool IsRunning(string executable) => Everything;
}
