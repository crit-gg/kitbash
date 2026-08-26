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
/// A control whose pixels come from the tool's own Vulkan device. Where the compositor can
/// import an image it hosts one as a child visual, and where it cannot the frame is read
/// back and painted. **The app's own render backend is never changed for this**, so no
/// window gives up its transparency to get a GPU surface.
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
    private IGpuPresenter? _presenter;
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

        // A read back frame is drawn from Render, so what is asked for is a render pass.
        // Asking the compositor instead would paint the frame before it was written.
        if (_presenter is { IsHandedOver: false })
        {
            InvalidateVisual();

            return;
        }

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
    /// The rectangle is filled whatever the presenter does, because a control that renders
    /// nothing is never hit tested and without it the surface takes no pointer input.
    /// </summary>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_updateQueued && _presenter is { IsHandedOver: false })
        {
            RenderFrame();
        }

        var bounds = new Rect(Bounds.Size);

        context.FillRectangle(Brushes.Transparent, bounds);
        _presenter?.Present(context, bounds);
    }

    /// <summary>Records this surface's work into an open buffer, with the target ready to draw on.</summary>
    protected abstract void Draw(VulkanContext context, GpuFrame frame);

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

            var interop = await _compositor.TryGetCompositionGpuInterop();

            // Null when the importer takes no handle type this device can export, which is
            // what the read back path is for.
            var (context, info) = VulkanContext.Acquire(interop, Needs);

            if (context == null)
            {
                (context, info) = VulkanContext.Acquire(null, Needs);
                interop = null;
            }

            Info = info;

            if (context == null)
            {
                return;
            }

            Context = context;
            Opened(context);

            _presenter = interop == null
                ? new ReadbackPresenter(context)
                : new CompositionSwapchain(context, interop, HandOver());

            _running = true;
            IsDrawing = true;

            Invalidate();
        }
        catch (Exception exception) when (exception is InvalidOperationException or DllNotFoundException)
        {
            Info = exception.Message;
        }
    }

    /// <summary>
    /// The child visual the compositor draws into. Only made on the path that hands an
    /// image over, since a control with one paints nothing of its own.
    /// </summary>
    private CompositionDrawingSurface HandOver()
    {
        var surface = _compositor!.CreateDrawingSurface();

        _visual = _compositor.CreateSurfaceVisual();
        _visual.Size = new Vector(Bounds.Width, Bounds.Height);
        _visual.Surface = surface;
        ElementComposition.SetElementChildVisual(this, _visual);

        return surface;
    }

    private async Task Teardown()
    {
        _running = false;
        IsDrawing = false;
        _resizeTimer?.Stop();
        _resizeTimer = null;

        var presenter = _presenter;
        var context = Context;
        _presenter = null;
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
            if (presenter != null)
            {
                await presenter.DisposeAsync();
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

        if (!_running || Context is not { } context || _presenter is not { } presenter)
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
            if (!presenter.TryBeginFrame(_renderSize, out var frame))
            {
                return;
            }

            Draw(context, frame);

            if (_capturePath != null)
            {
                _capture ??= new FrameCapture(context, _capturePath);
                _capture.Record(frame.Buffer, frame.Target);
            }

            presenter.EndFrame(frame);
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
