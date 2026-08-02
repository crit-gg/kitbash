using System.Net;

namespace Workbench.Core.Platform;

/// <summary>
/// <see cref="IWebContent"/> over one <see cref="HttpClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// **One client for the process, held for its life.** That is what the type is built for.
/// A client per request exhausts sockets, because a closed one leaves its connections in
/// TIME_WAIT for minutes, and this app fires several at once when a card opens.
/// </para>
/// <para>
/// The handler is given a connection lifetime, which is the half a long lived client gets
/// wrong on its own: a connection held forever keeps talking to an address that has moved,
/// since the DNS answer is only consulted when a connection is made. Five minutes is the
/// usual figure and costs one handshake.
/// </para>
/// <para>
/// `IHttpClientFactory` solves both and is deliberately not used. It lives in
/// `Microsoft.Extensions.Http`, and `Workbench.Core` takes Tomlyn and the DI abstractions
/// and nothing else. A single configured client is the documented alternative.
/// </para>
/// <para>
/// The container owns this. It is registered as a singleton, so the provider disposes it
/// at shutdown, and disposing the client disposes the handler with it.
/// </para>
/// </remarks>
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

            // Left off on purpose. The only bodies read here are a 48 KB feed and an 8 KB
            // manifest, so compression saves nothing worth having, and asking for it makes
            // a server free to answer a HEAD with a compressed length or with no length at
            // all. Measure has to report the real size of the file on disk.
            AutomaticDecompression = DecompressionMethods.None,
        };

        _client = new HttpClient(handler, disposeHandler: true)
        {
            // Off, because one number cannot bound both kinds of request here. Every one
            // below carries its own. Measured on .NET 10, since the rule is not the one it
            // is usually said to be:
            //
            //   ResponseContentRead, the default, is bounded body and all. A 3 second
            //   timeout against a body arriving over 9 seconds threw at 3003 ms.
            //
            //   ResponseHeadersRead stops the clock once the headers land. The same 3
            //   second timeout read the whole 9 second body and finished at 9006 ms.
            //
            // So the default 100 seconds would never have cut a download off, and it would
            // also never have ended one that stalled. An idle guard is what does that.
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
