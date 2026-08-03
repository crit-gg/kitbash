using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;
using Kitbash.Settings;

namespace Kitbash.Updates;

/// <summary>
/// The update side of Velopack, behind an interface so nothing else in the launcher
/// takes a dependency on it.
/// </summary>
public sealed class VelopackUpdates : IApplicationUpdates
{
    /// <summary>
    /// What SimpleWebSource gives an http request before giving up. Velopack's own
    /// default is thirty minutes, which leaves a check nobody is waiting for holding a
    /// socket for half an hour.
    /// </summary>
    /// <remarks>
    /// Safe to make this short. The feed is read with GetStringAsync, so this bounds it
    /// end to end, but a package is read with HttpCompletionOption.ResponseHeadersRead,
    /// so for the download it bounds only the wait for the first response and never the
    /// body. Read from Velopack's HttpClientFileDownloader rather than assumed.
    /// </remarks>
    private static readonly TimeSpan HttpLimit = TimeSpan.FromSeconds(15);

    private readonly UpdateSettingsSchema _schema;
    private readonly IApplicationSettings _settings;
    private readonly UpdateLog _log;
    private readonly Lock _gate = new();

    private UpdateManager? _manager;
    private bool _resolved;

    public VelopackUpdates(UpdateSettingsSchema schema, IApplicationSettings settings, UpdateLog log)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(log);

        _schema = schema;
        _settings = settings;
        _log = log;
    }

    public string Feed
    {
        get
        {
            if (!_schema.IsEnabled)
            {
                // Off at the schema, so a hand edited file cannot turn it on either.
                return string.Empty;
            }

            var stored = _schema.Feed.Read(_settings.Global);

            return IsAddress(stored) || IsFolder(stored) ? stored : string.Empty;
        }
    }

    public async Task<AvailableUpdate?> CheckAsync(CancellationToken cancellation = default)
    {
        if (Manager is not { } manager)
        {
            return null;
        }

        try
        {
            // CheckForUpdatesAsync takes no cancellation token, and SimpleWebSource waits
            // thirty minutes by default, so a token handed to Task.Run would only stop it
            // starting. Stopping waiting is the only bound available. Lowering the
            // source's own timeout is not, since the download goes through the same one.
            var check = Task.Run(manager.CheckForUpdatesAsync, CancellationToken.None);
            var deadline = Task.Delay(Timeout.Infinite, cancellation);

            if (await Task.WhenAny(check, deadline).ConfigureAwait(false) != check)
            {
                _log.Say("check gave up before the feed answered");

                // It is still running against a server that has not replied. Left to end
                // on its own, with its failure read so it is never unobserved.
                Abandon(check);

                return null;
            }

            var found = await check.ConfigureAwait(false);

            return found is null
                ? null
                : new AvailableUpdate(found.TargetFullRelease.Version.ToString(), found.TargetFullRelease.Size)
                {
                    Found = found,
                };
        }
        catch (Exception exception)
        {
            // Nobody asked for this, so no failure of it reaches a person. A dead server,
            // no network and a feed that will not parse all land here.
            _log.Say("check failed", exception);

            return null;
        }
    }

    /// <summary>
    /// Reads the end of a check nobody is waiting for any more, so a failure on it is
    /// never an unobserved task exception.
    /// </summary>
    private void Abandon(Task check) =>
        _ = check.ContinueWith(
            ended => _log.Say("the abandoned check ended badly", ended.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    public async Task DownloadAsync(
        AvailableUpdate update, IProgress<int> progress, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(progress);

        if (Manager is not { } manager || update.Found is not { } found)
        {
            throw new InvalidOperationException("There is nothing to download.");
        }

        _log.Say($"downloading {update.Version}");

        try
        {
            // Velopack reports from its own thread, so Report has to be safe to call from
            // one. A Progress built on the UI thread is, which is what the dialog hands
            // over.
            await manager.DownloadUpdatesAsync(found, progress.Report, cancellation).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Said here rather than at the dialog, so the view only has to close.
            _log.Say($"could not download {update.Version}", exception);

            throw;
        }
    }

    public void ApplyAndRestart(AvailableUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (Manager is not { } manager || update.Found is not { } found)
        {
            throw new InvalidOperationException("There is nothing to apply.");
        }

        _log.Say($"applying {update.Version} and restarting");

        try
        {
            manager.ApplyUpdatesAndRestart(found);
        }
        catch (Exception exception)
        {
            _log.Say($"could not apply {update.Version}", exception);

            throw;
        }
    }

    /// <summary>
    /// Built once and kept. Null when there is nowhere to look or this copy was not
    /// installed, which is every developer run.
    /// </summary>
    private UpdateManager? Manager
    {
        get
        {
            lock (_gate)
            {
                if (_resolved)
                {
                    return _manager;
                }

                _resolved = true;
                _manager = Create();

                return _manager;
            }
        }
    }

    private UpdateManager? Create()
    {
        var feed = Feed;

        if (feed.Length == 0)
        {
            return null;
        }

        try
        {
            IUpdateSource source = IsAddress(feed)
                ? new SimpleWebSource(feed) { Timeout = HttpLimit.TotalMinutes }
                : new SimpleFileSource(new DirectoryInfo(feed));

            var manager = new UpdateManager(source);

            // Velopack answers every other question for a copy that was not installed by
            // throwing, so the one build that is never packaged is settled here instead.
            if (!manager.IsInstalled)
            {
                _log.Say("this copy was not installed, so it never updates itself");

                return null;
            }

            _log.Say($"reading releases from {feed}");

            return manager;
        }
        catch (Exception exception) when (exception is NotInstalledException or ArgumentException or UriFormatException)
        {
            _log.Say("no update source", exception);

            return null;
        }
    }

    /// <summary>
    /// Checked through the value object before it reaches Velopack, so a scheme other
    /// than http or https never becomes an update source.
    /// </summary>
    private static bool IsAddress(string feed)
    {
        if (string.IsNullOrWhiteSpace(feed))
        {
            return false;
        }

        try
        {
            WebAddress.Parse(feed);

            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or UriFormatException)
        {
            return false;
        }
    }

    /// <summary>A relative path is not a place, which is the rule the settings already follow.</summary>
    private static bool IsFolder(string feed) =>
        !string.IsNullOrWhiteSpace(feed) && Path.IsPathRooted(feed);
}
