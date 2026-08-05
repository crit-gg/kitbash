using System.IO.Pipes;
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

    public event EventHandler? AskedToComeForward;

    public bool TryHold()
    {
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
            return HandOver();
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
    /// Tells the copy that holds the lock to come forward. False asks this copy to exit.
    /// </summary>
    private bool HandOver()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName(), PipeDirection.Out);

            pipe.Connect((int)HandoverTimeout.TotalMilliseconds);
            pipe.WriteByte(1);
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

                    if (pipe.ReadByte() >= 0)
                    {
                        AskedToComeForward?.Invoke(this, EventArgs.Empty);
                    }
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

    private static string Safe(string name)
    {
        var safe = name.AsSpan().Trim();

        return safe.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && safe.Length > 0
            ? safe.ToString()
            : "anyone";
    }
}
