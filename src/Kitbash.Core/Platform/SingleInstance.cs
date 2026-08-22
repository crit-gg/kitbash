using System.IO.Pipes;
using System.Text;
using Kitbash.Core.IO;

namespace Kitbash.Core.Platform;

/// <summary>
/// An exclusive handle on a file, plus a pipe the second copy speaks to. A file lock
/// rather than a named mutex, because the mutex prefix means opposite things on the two
/// platforms: Linux needs Global to be visible across processes at all, and on Windows
/// Global is machine wide and would stop a second person signed in from opening the app.
/// </summary>
public sealed class SingleInstance : ISingleInstance
{
    /// <summary>Set to anything to allow several copies. For working on the app itself.</summary>
    public const string OverrideVariable = "KITBASH_MANY_LAUNCHERS";

    private const string LockFileName = "instance.lock";

    /// <summary>
    /// The most a second copy may send. A deep link is the only thing sent today and no
    /// desktop hands one anywhere near this long.
    /// </summary>
    private const int MessageLimit = 8 * 1024;

    /// <summary>
    /// How long the second copy waits for the first to answer. The first takes the lock
    /// before it starts listening, so a copy started in that window has to wait out the
    /// gap rather than conclude nobody is there.
    /// </summary>
    private static readonly TimeSpan HandoverTimeout = TimeSpan.FromSeconds(2);

    private readonly string _application;
    private readonly IUserDirectories _directories;
    private readonly IFileSystem _files;
    private readonly IEnvironment _environment;
    private readonly CancellationTokenSource _closing = new();

    private FileStream? _held;
    private bool _gone;

    public SingleInstance(
        string application,
        IUserDirectories directories,
        IFileSystem files,
        IEnvironment environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(application);
        ArgumentNullException.ThrowIfNull(directories);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(environment);

        _application = application;
        _directories = directories;
        _files = files;
        _environment = environment;
    }

    public event EventHandler<ComeForwardEventArgs>? AskedToComeForward;

    public bool TryHold(string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!string.IsNullOrWhiteSpace(_environment.GetVariable(OverrideVariable)))
        {
            return true;
        }

        var directory = _directories.RuntimeFor(_application);

        try
        {
            _files.CreateDirectory(directory);

            // FileShare.None is exclusive on both platforms, and the kernel drops the
            // handle when the process dies, including on a kill, which a pid file does not.
            _held = new FileStream(
                Path.Combine(directory, LockFileName),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException)
        {
            return HandOver(message);
        }
        catch (UnauthorizedAccessException)
        {
            // A directory that will not take a lock is not a reason to refuse to start.
            return true;
        }

        Listen();

        return true;
    }

    public void Dispose()
    {
        // Disposing twice is allowed, and cancelling a cancellation source that has been
        // disposed is not, so this only ever runs once.
        if (_gone)
        {
            return;
        }

        _gone = true;

        _closing.Cancel();
        _held?.Dispose();
        _held = null;
        _closing.Dispose();
    }

    /// <summary>
    /// The name is per user, since a pipe is machine wide on Windows and a socket in the
    /// shared temp directory on Linux, so two people signed in would otherwise collide.
    /// </summary>
    private string PipeName()
    {
        var person = _environment.GetVariable("USER")
            ?? _environment.GetVariable("USERNAME")
            ?? "anyone";

        return $"{_application}-{Safe(person)}";
    }

    /// <summary>
    /// Tells the copy that holds the lock to come forward, and hands it the message.
    /// False asks this copy to exit.
    /// </summary>
    private bool HandOver(string message)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName(), PipeDirection.Out);

            pipe.Connect((int)HandoverTimeout.TotalMilliseconds);

            var payload = Encoding.UTF8.GetBytes(message);

            // The message is the whole stream rather than a framed value, so a copy running
            // an older build, which sends one byte and no length, is still understood.
            if (payload.Length is > 0 and <= MessageLimit)
            {
                pipe.Write(payload);
            }

            pipe.Flush();

            return false;
        }
        catch (Exception exception) when (exception is TimeoutException or IOException
            or UnauthorizedAccessException or ObjectDisposedException)
        {
            // Nobody answered, so there is nothing to come forward and this copy opens.
            return true;
        }
    }

    private void Listen() => _ = Task.Run(
        async () =>
        {
            while (!_closing.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(
                        PipeName(),
                        PipeDirection.In,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await pipe.WaitForConnectionAsync(_closing.Token).ConfigureAwait(false);

                    var message = await ReadAsync(pipe).ConfigureAwait(false);

                    AskedToComeForward?.Invoke(this, new ComeForwardEventArgs(message));
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception exception) when (exception is IOException
                    or UnauthorizedAccessException)
                {
                    // One refused connection is not a reason to stop listening for the next.
                }
            }
        },
        _closing.Token);

    /// <summary>
    /// Everything the second copy sent, as text. It closes the pipe when it is done, so
    /// the end of the stream is the end of the message. Anything past the limit is dropped
    /// rather than read, so a caller that will not stop cannot fill this process.
    /// </summary>
    private async Task<string> ReadAsync(Stream pipe)
    {
        var buffer = new byte[MessageLimit];
        var filled = 0;

        while (filled < buffer.Length)
        {
            var read = await pipe
                .ReadAsync(buffer.AsMemory(filled), _closing.Token)
                .ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        return filled == 0 ? string.Empty : Encoding.UTF8.GetString(buffer, 0, filled);
    }

    /// <summary>
    /// A unix domain socket path is capped at 104 bytes on macOS, and a pipe name is part
    /// of one, so a long name is cut rather than allowed to make an unopenable socket.
    /// </summary>
    private const int NameLimit = 32;

    private static string Safe(string name)
    {
        var safe = name.AsSpan().Trim();

        if (safe.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || safe.Length == 0)
        {
            return "anyone";
        }

        return safe.Length > NameLimit ? safe[..NameLimit].ToString() : safe.ToString();
    }
}
