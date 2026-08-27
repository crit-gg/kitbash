using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Kitbash.Core.IO;

namespace Kitbash.Core.Platform.Linux;

/// <summary>
/// Declares the shadow through _GTK_FRAME_EXTENTS, the property a window that draws its own
/// frame sets to say part of it is not really there. KWin, Mutter, Xfwm4, Muffin and Marco
/// read it. A window manager that does not is left doing what it did before.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class X11WindowShadow : IWindowShadow, IDisposable
{
    /// <summary>What Avalonia calls an X11 window in a platform handle.</summary>
    private const string Xid = "XID";

    private const string Property = "_GTK_FRAME_EXTENTS";

    /// <summary>Xlib's own numbers: XA_CARDINAL is a predefined atom and PropModeReplace is 0.</summary>
    private const nint Cardinal = 6;
    private const int Replace = 0;

    /// <summary>Held in a field so the stub it is called through is not collected.</summary>
    private static readonly XErrorHandler Handler = OnError;

    /// <summary>
    /// Xlib keeps one error handler for the whole process, so these are static. Ours is the
    /// only connection whose errors are swallowed and anybody else's go on as they did.
    /// </summary>
    private static nint _ours;
    private static nint _previous;

    private readonly IEnvironment _environment;
    private readonly object _gate = new();

    private nint _display;
    private nint _atom;
    private bool _opened;
    private bool _disposed;

    public X11WindowShadow(IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        _environment = environment;
    }

    /// <summary>
    /// Whether there is a display to talk to. Whether the window manager reads the property
    /// is not asked, since nothing answers that and writing it costs one round trip.
    /// </summary>
    public bool CanDeclare => _environment.GetVariable("DISPLAY") is { Length: > 0 };

    public void Declare(nint window, string kind, WindowShadowExtents extents)
    {
        if (window == 0 || !string.Equals(kind, Xid, StringComparison.Ordinal))
        {
            return;
        }

        lock (_gate)
        {
            if (!TryOpen())
            {
                return;
            }

            // Format 32 means an array of long to Xlib, whatever the property itself holds,
            // so these are nint. An int array writes each value into half a slot.
            nint[] values = [extents.Left, extents.Right, extents.Top, extents.Bottom];

            XChangeProperty(_display, window, _atom, Cardinal, 32, Replace, values, values.Length);
            XFlush(_display);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_display != 0)
            {
                XCloseDisplay(_display);

                _display = 0;
                _ours = 0;
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Our own connection, since Avalonia's is not ours to reach. A property write from a
    /// second connection lands on the same window, and one failure is enough to stop asking.
    /// </summary>
    private bool TryOpen()
    {
        if (_disposed)
        {
            return false;
        }

        if (_display != 0)
        {
            return true;
        }

        if (_opened || !CanDeclare)
        {
            return false;
        }

        _opened = true;

        nint display;

        try
        {
            display = XOpenDisplay(0);
        }
        catch (DllNotFoundException)
        {
            // A session with no Xlib installed at all, which a Wayland only machine can be.
            return false;
        }

        if (display == 0)
        {
            return false;
        }

        var atom = XInternAtom(display, Property, 0);

        if (atom == 0)
        {
            XCloseDisplay(display);

            return false;
        }

        _display = display;
        _atom = atom;
        _ours = display;

        // A window that has gone answers BadWindow, and a declaration can always race a
        // close. The default Xlib handler ends the process on one, so it is replaced before
        // anything is written.
        _previous = XSetErrorHandler(Handler);

        return true;
    }

    /// <summary>
    /// The first argument is the connection the failed request was made on, so an error of
    /// ours is told apart from one belonging to whoever else speaks X in this process.
    /// Theirs goes on to the handler that was installed before ours, which is Avalonia's.
    /// </summary>
    private static unsafe int OnError(nint display, nint error)
    {
        if (display == _ours || _previous == 0)
        {
            return 0;
        }

        // A delegate cannot be made from this pointer, since it is already somebody else's
        // reverse stub and the runtime refuses to cast one delegate type to another.
        return ((delegate* unmanaged[Cdecl]<nint, nint, int>)_previous)(display, error);
    }

    [DllImport("libX11.so.6")]
    private static extern nint XOpenDisplay(nint name);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(nint display);

    [DllImport("libX11.so.6", CharSet = CharSet.Ansi)]
    private static extern nint XInternAtom(nint display, string name, int onlyIfExists);

    [DllImport("libX11.so.6")]
    private static extern int XChangeProperty(
        nint display,
        nint window,
        nint property,
        nint type,
        int format,
        int mode,
        nint[] data,
        int count);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(nint display);

    [DllImport("libX11.so.6")]
    private static extern nint XSetErrorHandler(XErrorHandler handler);

    /// <summary>Winapi means Cdecl off Windows, which this only ever runs on.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int XErrorHandler(nint display, nint error);
}
