using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Gpu;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A control whose pixels come from the tool's own Vulkan device. The compositor hosts the
/// image as a child visual, so it clips and layers like any other control.
/// </summary>
public abstract class GpuSurface : Control
{
    /// <summary>How long a resize has to settle before the images are reallocated.</summary>
    private static readonly TimeSpan ResizeQuiet = TimeSpan.FromMilliseconds(120);

    public static readonly DirectProperty<GpuSurface, string> InfoProperty =
        AvaloniaProperty.RegisterDirect<GpuSurface, string>(
            nameof(Info), o => o.Info, (o, v) => o.Info = v);

    public static readonly DirectProperty<GpuSurface, bool> IsDrawingProperty =
        AvaloniaProperty.RegisterDirect<GpuSurface, bool>(
            nameof(IsDrawing), o => o.IsDrawing, (o, v) => o.IsDrawing = v);

    /// <summary>
    /// Keeps asking for frames. Off, a frame is drawn only when something changed, which is
    /// what an editor wants.
    /// </summary>
    public static readonly StyledProperty<bool> ContinuousProperty =
        AvaloniaProperty.Register<GpuSurface, bool>(nameof(Continuous));

    private readonly Action _update;
    private DispatcherTimer? _resizeTimer;
    private CompositionSurfaceVisual? _visual;
    private Compositor? _compositor;
    private CompositionSwapchain? _swapchain;
    private string _info = "";
    private bool _isDrawing;
    private bool _updateQueued;
    private bool _running;
    private PixelSize _renderSize;
    private PixelSize _pendingSize;
    private int _presented;
    private string? _capturePath;
    private FrameCapture? _capture;
    private Task? _teardown;

    protected GpuSurface() => _update = RenderFrame;

    /// <summary>What the device reported, or why there is nothing on screen.</summary>
    public string Info
    {
        get => _info;
        private set => SetAndRaise(InfoProperty, ref _info, value);
    }

    /// <summary>True once a device was taken and frames are reaching the compositor.</summary>
    public bool IsDrawing
    {
        get => _isDrawing;
        private set => SetAndRaise(IsDrawingProperty, ref _isDrawing, value);
    }

    public bool Continuous
    {
        get => GetValue(ContinuousProperty);
        set => SetValue(ContinuousProperty, value);
    }

    /// <summary>The pixel size the images were last allocated at.</summary>
    public PixelSize RenderSize => _renderSize;

    /// <summary>How many frames this surface has put on screen since it started.</summary>
    public int Presented => _presented;

    /// <summary>The device, once one was taken. Null before that and after shutdown.</summary>
    protected VulkanContext? Context { get; private set; }

    /// <summary>What this surface needs of the device. Read once, before the device is made.</summary>
    protected virtual VulkanNeeds Needs => default;

    /// <summary>A surface that always wants the next frame, whatever the toggle says.</summary>
    protected virtual bool AlwaysDraws => false;

    /// <summary>Asks for the next frame to be written out as a PNG.</summary>
    public void CaptureNextFrame(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _capturePath = path;
        Invalidate();
    }

    /// <summary>Asks for one more frame. Cheap when a frame is already queued.</summary>
    public void Invalidate()
    {
        if (!_running || _updateQueued || _compositor == null)
        {
            return;
        }

        _updateQueued = true;
        _compositor.RequestCompositionUpdate(_update);
    }

    /// <summary>
    /// Asks for one more frame from any thread. A composition update asked for from inside
    /// the render thread's own pass is dropped, so it is asked for on the UI thread.
    /// </summary>
    public void InvalidateFromAnyThread()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Invalidate();

            return;
        }

        Dispatcher.UIThread.Post(Invalidate, DispatcherPriority.Render);
    }

    /// <summary>
    /// Gives up the device and every import made from it, once. The compositor has to still
    /// be running while this is awaited, because releasing an import is a message to it.
    /// </summary>
    public Task ShutdownAsync() => _teardown ??= Teardown();

    /// <summary>
    /// A composition child visual draws the pixels, so there is nothing here to paint. The
    /// rectangle exists because a control that renders nothing is never hit tested, and
    /// without it the surface takes no pointer input at all.
    /// </summary>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
    }

    /// <summary>Records this surface's work into an open buffer, with the target ready to draw on.</summary>
    protected abstract void Draw(VulkanContext context, CompositionSwapchain.Frame frame);

    /// <summary>Builds whatever this surface needs of the device. Runs on the UI thread, once.</summary>
    protected virtual void Opened(VulkanContext context)
    {
    }

    /// <summary>
    /// The frame is submitted. Anything the command buffer still reads, such as a staging
    /// buffer, is freed here rather than during <see cref="Draw"/>, where the queue has not
    /// been given the work yet and freeing it invalidates the recording.
    /// </summary>
    protected virtual void Drawn(VulkanContext context)
    {
    }

    /// <summary>
    /// Gives up whatever <see cref="Opened"/> built. The device is already idle and is still
    /// alive, so a Vulkan object made here is destroyed here.
    /// </summary>
    protected virtual void Closing(VulkanContext context)
    {
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Start();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _ = ShutdownAsync();
        base.OnDetachedFromLogicalTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);

        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty)
        {
            OnResized();
        }
        else if (change.Property == ContinuousProperty)
        {
            Invalidate();
        }
    }

    private async void Start()
    {
        try
        {
            var self = ElementComposition.GetElementVisual(this);

            if (self == null)
            {
                Info = "This surface was given no place to draw.";

                return;
            }

            _compositor = self.Compositor;

            var surface = _compositor.CreateDrawingSurface();
            _visual = _compositor.CreateSurfaceVisual();
            _visual.Size = new Vector(Bounds.Width, Bounds.Height);
            _visual.Surface = surface;
            ElementComposition.SetElementChildVisual(this, _visual);

            var interop = await _compositor.TryGetCompositionGpuInterop();

            if (interop == null)
            {
                Info = "This display backend cannot draw with the GPU.";

                return;
            }

            var (context, info) = VulkanContext.Acquire(interop, Needs);
            Info = info;

            if (context == null)
            {
                return;
            }

            Context = context;
            Opened(context);

            _swapchain = new CompositionSwapchain(context, interop, surface);
            _running = true;
            IsDrawing = true;

            Invalidate();
        }
        catch (Exception exception) when (exception is InvalidOperationException or DllNotFoundException)
        {
            Info = exception.Message;
        }
    }

    private async Task Teardown()
    {
        _running = false;
        IsDrawing = false;
        _resizeTimer?.Stop();
        _resizeTimer = null;

        var swapchain = _swapchain;
        var context = Context;
        _swapchain = null;
        Context = null;

        if (context == null)
        {
            return;
        }

        // The device has to stop touching the images before the imports are released.
        context.WaitIdle();

        _capture?.Dispose();
        _capture = null;

        Closing(context);

        try
        {
            if (swapchain != null)
            {
                await swapchain.DisposeAsync();
            }
        }
        finally
        {
            VulkanContext.Release();
        }
    }

    /// <summary>
    /// A drag changes Bounds every frame and each new size is a fresh set of images, so the
    /// exact size is only adopted once the pointer has been still for a moment.
    /// </summary>
    private void OnResized()
    {
        if (_visual != null)
        {
            _visual.Size = new Vector(Bounds.Width, Bounds.Height);
        }

        var scaling = this.GetPresentationSource()?.RenderScaling ?? 1d;
        _pendingSize = PixelSize.FromSize(Bounds.Size, scaling);

        if (_renderSize == default)
        {
            _renderSize = _pendingSize;
        }
        else
        {
            _resizeTimer ??= new DispatcherTimer(ResizeQuiet, DispatcherPriority.Background, OnResizeSettled);
            _resizeTimer.Stop();
            _resizeTimer.Start();
        }

        Invalidate();
    }

    private void OnResizeSettled(object? sender, EventArgs e)
    {
        _resizeTimer?.Stop();

        if (_renderSize == _pendingSize)
        {
            return;
        }

        _renderSize = _pendingSize;
        Invalidate();
    }

    private void RenderFrame()
    {
        _updateQueued = false;

        if (!_running || Context is not { } context || _swapchain == null)
        {
            return;
        }

        if (this.GetPresentationSource() == null)
        {
            return;
        }

        // One queue behind every surface, and a worker thread may be submitting on it.
        lock (VulkanContext.Gate)
        {
            if (!_swapchain.TryBeginFrame(_renderSize, out var frame))
            {
                return;
            }

            Draw(context, frame);

            if (_capturePath != null)
            {
                _capture ??= new FrameCapture(context, _capturePath);
                _capture.Record(frame.Recording.Buffer, frame.Image.Target);
            }

            _swapchain.EndFrame(frame);
            Drawn(context);

            if (_capture is { Done: false })
            {
                _capture.Save();
            }

            _presented++;
        }

        if (Continuous || AlwaysDraws)
        {
            Invalidate();
        }
    }
}
