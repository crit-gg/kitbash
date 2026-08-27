using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Platform.Linux;

namespace Kitbash.Core.Tests.Platform;

/// <summary>
/// The shadow declaration, against a real X server. It writes _GTK_FRAME_EXTENTS on a real
/// window and reads it back, which is the only way to catch the format 32 trap, where a
/// property written as ints reads back as half the values and three zeros.
/// </summary>
/// <remarks>Every test here skips itself off Linux, which is what the attribute states.</remarks>
[SupportedOSPlatform("linux")]
public sealed class X11WindowShadowTests
{
    private const string NoDisplay = "no X display on this machine";

    private static bool HasDisplay =>
        OperatingSystem.IsLinux() &&
        Environment.GetEnvironmentVariable("DISPLAY") is { Length: > 0 };

    [Fact]
    public void ADeclarationLandsOnTheWindow()
    {
        Assert.SkipUnless(HasDisplay, NoDisplay);

        using var probe = new Probe();

        probe.Shadow.Declare(probe.Window, "XID", new WindowShadowExtents(12, 12, 34, 12));

        Assert.Equal([12, 12, 34, 12], probe.Read());
    }

    /// <summary>Zero is what a maximized window says, and it has to overwrite what was there.</summary>
    [Fact]
    public void GivingTheGutterUpOverwritesTheLastOne()
    {
        Assert.SkipUnless(HasDisplay, NoDisplay);

        using var probe = new Probe();

        probe.Shadow.Declare(probe.Window, "XID", new WindowShadowExtents(12, 12, 12, 12));
        probe.Shadow.Declare(probe.Window, "XID", WindowShadowExtents.None);

        Assert.Equal([0, 0, 0, 0], probe.Read());
    }

    /// <summary>An HWND is not ours to write to, so nothing is said about it at all.</summary>
    [Fact]
    public void AHandleFromAnotherDesktopIsIgnored()
    {
        Assert.SkipUnless(HasDisplay, NoDisplay);

        using var probe = new Probe();

        probe.Shadow.Declare(probe.Window, "HWND", new WindowShadowExtents(12, 12, 12, 12));

        Assert.Empty(probe.ReadNow());
    }

    /// <summary>A window that has gone answers BadWindow, which must not take the app down.</summary>
    [Fact]
    public void AWindowThatHasGoneIsSurvived()
    {
        Assert.SkipUnless(HasDisplay, NoDisplay);

        using var probe = new Probe();
        var dead = probe.Window;

        probe.Destroy();

        probe.Shadow.Declare(dead, "XID", new WindowShadowExtents(12, 12, 12, 12));
    }

    [Fact]
    public void NoDisplayMeansNothingIsDeclared()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), NoDisplay);

        using var shadow = new X11WindowShadow(new FakeEnvironment(new Dictionary<string, string>()));

        Assert.False(shadow.CanDeclare);

        // The window is made up, so this reaching the server at all would be the failure.
        shadow.Declare(1, "XID", new WindowShadowExtents(12, 12, 12, 12));
    }

    /// <summary>One real unmapped window, and a second connection to read what was written.</summary>
    private sealed class Probe : IDisposable
    {
        private readonly nint _display;
        private readonly nint _atom;

        private nint _window;

        public Probe()
        {
            _display = XOpenDisplay(0);
            _atom = XInternAtom(_display, "_GTK_FRAME_EXTENTS", 0);
            _window = XCreateSimpleWindow(_display, XDefaultRootWindow(_display), 0, 0, 64, 64, 0, 0, 0);

            XSync(_display, 0);
        }

        public X11WindowShadow Shadow { get; } =
            new(new FakeEnvironment(new Dictionary<string, string>
            {
                ["DISPLAY"] = Environment.GetEnvironmentVariable("DISPLAY") ?? string.Empty,
            }));

        public nint Window => _window;

        /// <summary>
        /// The four cardinals, or nothing when the property was never written. Two
        /// connections are involved, so a read can arrive before the write is processed.
        /// </summary>
        public long[] Read()
        {
            for (var attempt = 0; ; attempt++)
            {
                var read = ReadNow();

                if (read.Length > 0 || attempt == 20)
                {
                    return read;
                }

                Thread.Sleep(25);
            }
        }

        /// <summary>What is on the window this instant, which is nothing before a write.</summary>
        public long[] ReadNow()
        {
            XSync(_display, 0);

            var read = XGetWindowProperty(
                _display, _window, _atom, 0, 4, 0, 0,
                out _, out var format, out var items, out _, out var value);

            if (read != 0 || value == 0)
            {
                return [];
            }

            try
            {
                if (items == 0)
                {
                    return [];
                }

                // Anything but 32 here is the property being written in the wrong shape.
                Assert.Equal(32, format);

                var values = new long[(int)items];

                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = Marshal.ReadIntPtr(value, i * nint.Size);
                }

                return values;
            }
            finally
            {
                XFree(value);
            }
        }

        public void Destroy()
        {
            if (_window == 0)
            {
                return;
            }

            XDestroyWindow(_display, _window);
            XSync(_display, 0);

            _window = 0;
        }

        public void Dispose()
        {
            Shadow.Dispose();
            Destroy();
            XCloseDisplay(_display);
        }

        [DllImport("libX11.so.6")]
        private static extern nint XOpenDisplay(nint name);

        [DllImport("libX11.so.6")]
        private static extern int XCloseDisplay(nint display);

        [DllImport("libX11.so.6")]
        private static extern nint XDefaultRootWindow(nint display);

        [DllImport("libX11.so.6")]
        private static extern nint XCreateSimpleWindow(
            nint display, nint parent, int x, int y, uint width, uint height,
            uint border, nint borderColour, nint background);

        [DllImport("libX11.so.6")]
        private static extern int XDestroyWindow(nint display, nint window);

        [DllImport("libX11.so.6", CharSet = CharSet.Ansi)]
        private static extern nint XInternAtom(nint display, string name, int onlyIfExists);

        [DllImport("libX11.so.6")]
        private static extern int XSync(nint display, int discard);

        [DllImport("libX11.so.6")]
        private static extern int XFree(nint data);

        [DllImport("libX11.so.6")]
        private static extern int XGetWindowProperty(
            nint display, nint window, nint property, nint offset, nint length,
            int delete, nint requested, out nint type, out int format,
            out nint items, out nint after, out nint value);
    }

    private sealed class FakeEnvironment(Dictionary<string, string> variables) : IEnvironment
    {
        public string? GetVariable(string name) =>
            variables.TryGetValue(name, out var value) ? value : null;

        public string GetHomeDirectory() => "/home/someone";

        public IReadOnlyList<string> GetProcessCommand() => [];
    }
}
