using System.IO.Pipes;
using System.Text;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;

namespace Kitbash.Core.Tests.Platform;

/// <summary>
/// The handover between two copies, over a real lock file and a real pipe. The link a
/// second copy was started for has to reach the copy that is already running, since that
/// is the only one with a window.
/// </summary>
public sealed class SingleInstanceTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "kitbash-instance-" + Guid.NewGuid().ToString("n"));

    /// <summary>Unique per test, since a pipe name is machine wide.</summary>
    private readonly string _application = "kbtest" + Guid.NewGuid().ToString("n")[..8];

    private readonly List<ISingleInstance> _held = [];

    public void Dispose()
    {
        foreach (var instance in _held)
        {
            instance.Dispose();
        }

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    [Fact]
    public async Task TheSecondCopyHandsItsLinkToTheFirst()
    {
        var heard = new TaskCompletionSource<string>();
        var first = Instance();

        first.AskedToComeForward += (_, asked) => heard.TrySetResult(asked.Message);

        Assert.True(first.TryHold());
        Assert.False(Instance().TryHold("kitbash://engine/4.7.1"));

        Assert.Equal("kitbash://engine/4.7.1", await Within(heard.Task));
    }

    /// <summary>A copy started from a menu carries nothing and only asks for the window.</summary>
    [Fact]
    public async Task ACopyWithNoLinkSendsNothing()
    {
        var heard = new TaskCompletionSource<string>();
        var first = Instance();

        first.AskedToComeForward += (_, asked) => heard.TrySetResult(asked.Message);

        Assert.True(first.TryHold());
        Assert.False(Instance().TryHold());

        Assert.Equal(string.Empty, await Within(heard.Task));
    }

    /// <summary>
    /// What a copy running the build before this one sends. It is understood as a request
    /// to come forward, and the byte is not mistaken for a link.
    /// </summary>
    [Fact]
    public async Task ACopyThatSendsOneByteIsStillHeard()
    {
        var heard = new TaskCompletionSource<string>();
        var first = Instance();

        first.AskedToComeForward += (_, asked) => heard.TrySetResult(asked.Message);

        Assert.True(first.TryHold());

        await using (var pipe = new NamedPipeClientStream(".", PipeName(), PipeDirection.Out))
        {
            await pipe.ConnectAsync(
                (int)Patience.TotalMilliseconds, TestContext.Current.CancellationToken);
            pipe.WriteByte(1);
            pipe.Flush();
        }

        var message = await Within(heard.Task);

        Assert.False(DeepLink.TryParse(message, out _));
    }

    /// <summary>The lock is dropped with the process, so the next copy takes it.</summary>
    [Fact]
    public void TheLockGoesBackWhenTheFirstCopyLeaves()
    {
        var first = Instance();

        Assert.True(first.TryHold());

        first.Dispose();

        Assert.True(Instance().TryHold());
    }

    [Fact]
    public async Task ALinkTooLongToBeOneIsNotSent()
    {
        var heard = new TaskCompletionSource<string>();
        var first = Instance();

        first.AskedToComeForward += (_, asked) => heard.TrySetResult(asked.Message);

        Assert.True(first.TryHold());
        Assert.False(Instance().TryHold(new string('a', 16 * 1024)));

        Assert.Equal(string.Empty, await Within(heard.Task));
    }

    private string PipeName() => $"{_application}-someone";

    private ISingleInstance Instance()
    {
        var instance = new SingleInstance(
            _application,
            new FakeUserDirectories(_root),
            new FileSystem(),
            new FakeEnvironment());

        _held.Add(instance);

        return instance;
    }

    private static async Task<string> Within(Task<string> heard)
    {
        Assert.Same(
            heard,
            await Task.WhenAny(heard, Task.Delay(Patience, TestContext.Current.CancellationToken)));

        return await heard;
    }

    /// <summary>No override variable, and one name, so the pipe is the same for both copies.</summary>
    private sealed class FakeEnvironment : IEnvironment
    {
        public string? GetVariable(string name) => name is "USER" ? "someone" : null;

        public string GetHomeDirectory() => "/home/someone";

        public IReadOnlyList<string> GetProcessCommand() => [];
    }

    private sealed class FakeUserDirectories(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state");

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "runtime");
    }
}
