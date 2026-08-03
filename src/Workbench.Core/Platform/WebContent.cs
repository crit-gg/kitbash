using System.Net;

namespace Workbench.Core.Platform;

/// <summary>
/// <see cref="IWebContent"/> over one <see cref="HttpClient"/>.
/// </summary>
internal sealed class WebContent : IWebContent, IDisposable
{
    private const int BufferSize = 81920;

    /// <summary>Long enough for a slow first byte, short enough that a dead host gives up.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a download may go without a byte arriving. A total limit cannot be used
    /// here, since a build is a hundred megabytes and a slow line is not a failure, but a
    /// server that accepts a connection and then says nothing has to end somewhere.
    /// </summary>
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

    private readonly HttpClient _client;

    public WebContent()
    {
        var handler = new SocketsHttpHandler
        {
            // A connection made before this is closed rather than reused, so a DNS change
            // is noticed by a process that stays open for days.
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(15),

            // Left off. Bodies here are tens of kilobytes, and asking for compression
            // lets a server answer a HEAD with a compressed length or none at all, which
            // Measure needs to be the real size on disk.
            AutomaticDecompression = DecompressionMethods.None,
        };

        _client = new HttpClient(handler, disposeHandler: true)
        {
            // Off, since one number cannot bound both kinds of request here and each call
            // below sets its own. Note that under ResponseHeadersRead this timeout stops
            // once the headers land, so it never bounds a download. The idle guard does.
            Timeout = Timeout.InfiniteTimeSpan,
        };

        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Workbench/1.0");
    }

    public async Task<string> ReadTextAsync(WebAddress address, CancellationToken cancellationToken)
    {
        using var bounded = Bound(cancellationToken, RequestTimeout);

        try
        {
            using var response = await _client
                .GetAsync(address.Value, bounded.Token)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(bounded.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw TimedOut(address, RequestTimeout);
        }
    }

    public async Task<long?> MeasureAsync(WebAddress address, CancellationToken cancellationToken)
    {
        using var bounded = Bound(cancellationToken, RequestTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Head, address.Value);

        try
        {
            using var response = await _client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, bounded.Token)
                .ConfigureAwait(false);

            // Redirects are followed, so this is the length off the final response. The 302
            // a download URL answers with reports zero and is never what lands here.
            return response.IsSuccessStatusCode ? response.Content.Headers.ContentLength : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw TimedOut(address, RequestTimeout);
        }
    }

    public async Task DownloadAsync(
        WebAddress address,
        string path,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // The countdown is restarted after every block that arrives, so this bounds silence
        // rather than the transfer. A hundred megabytes over a slow line never trips it.
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            idle.CancelAfter(RequestTimeout);

            using var response = await _client
                .GetAsync(address.Value, HttpCompletionOption.ResponseHeadersRead, idle.Token)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            await using var source = await response.Content
                .ReadAsStreamAsync(idle.Token)
                .ConfigureAwait(false);

            await using var target = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true);

            var buffer = new byte[BufferSize];
            var written = 0L;

            while (true)
            {
                idle.CancelAfter(IdleTimeout);

                var read = await source.ReadAsync(buffer, idle.Token).ConfigureAwait(false);

                if (read == 0)
                {
                    break;
                }

                await target.WriteAsync(buffer.AsMemory(0, read), idle.Token).ConfigureAwait(false);

                written += read;
                progress?.Report(written);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw TimedOut(address, IdleTimeout);
        }
    }

    public void Dispose() => _client.Dispose();

    /// <summary>
    /// The caller's token with a deadline on top. Cancelling and timing out have to be
    /// told apart afterwards, which is what the caller's own token is checked for.
    /// </summary>
    private static CancellationTokenSource Bound(CancellationToken cancellationToken, TimeSpan after)
    {
        var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        bounded.CancelAfter(after);

        return bounded;
    }

    private static TimeoutException TimedOut(WebAddress address, TimeSpan after) =>
        new($"{address} did not answer within {after.TotalSeconds:0} seconds.");
}
