---
name: kitbash-gpu
description: "Kitbash GPU surface rules. The shared Vulkan device, the exported image ring, the composition handoff, the resize debounce and the teardown order. Read before drawing anything with a GPU, or before touching anything under Kitbash.Ui/Gpu."
---

### The GPU surface

A control whose pixels come from the tool's own Vulkan device. The compositor hosts the
image as a child visual, so it clips, layers and scrolls like any other control.

**The library owns the device and the handoff. The tool owns what is drawn.** The Vulkan
context, the exported image ring, the composition handoff, the resize debounce and the
teardown order are about being a Kitbash tool that draws with a GPU, and any tool wanting a
viewport needs all of them. The scene, the shaders and the pipelines are the tool's.

Subclass `ui:GpuSurface` and answer three things:

```csharp
protected override VulkanNeeds Needs => new() { Tessellation = true };

protected override void Opened(VulkanContext context) { }   // build, once
protected override void Draw(VulkanContext context, CompositionSwapchain.Frame frame) { }
protected override void Closing(VulkanContext context) { }  // give up, once
```

`Draw` records into an open command buffer whose target is already a color attachment. Do
not submit it. The surface submits, hands the image over and signals the pair.

### The app has to ask for a compositor that can hand images over

Avalonia's default on Linux is OpenGL, and that backend imports nothing, so a surface under
it draws nothing at all. **An app with a GPU surface names the rendering modes itself**, in
its own `Program.cs`, because the options live in the desktop packages rather than here:

```csharp
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .With(new X11PlatformOptions
    {
        RenderingMode = [X11RenderingMode.Vulkan, X11RenderingMode.Egl, X11RenderingMode.Glx],
    })
    .With(new Win32PlatformOptions
    {
        RenderingMode = [Win32RenderingMode.Vulkan, Win32RenderingMode.AngleEgl],
    })
```

The fallbacks stay behind it, so a machine with no Vulkan compositor still shows a window
and the surface says why it is empty.

### A window holding a GPU surface gives up the drawn frame

**The Vulkan compositor presents an opaque surface, so a window that draws its own frame
comes out with a black gutter and square corners.** `ActualTransparencyLevel` still answers
`Transparent`, so the window believes it has transparency and only the composited result
says otherwise. Measured on Linux under Vulkan against the same window under EGL.

An app with a GPU surface takes the desktop's frame instead, registered before the library
because its own is a `TryAdd`:

```csharp
services.AddSingleton(new WindowChromeRule(WindowChromeKind.Desktop));
```

That leaves the setting meaning what it means everywhere else. A person who asked for the
native frame already had it, and one who did not now gets it because the alternative is a
black box.

### One device for the process

`VulkanContext.Acquire` makes the device on the first call and shares it after. Every
caller pairs it with `Release`, and the device goes when the last one lets go. **The first
caller's `VulkanNeeds` are the ones used**, so two surfaces in one window get the union of
nothing: whichever attached first decides. Ask for what the heaviest surface needs.

There is one queue behind every surface. **Anything that submits takes `VulkanContext.Gate`
first**, including a bake on a worker thread, or two surfaces race the same queue.

### What the compositor will and will not do

- **`R8G8B8A8UNorm` is the only format the importer understands.** A surface working in any
  other format converts on the way into the presented image.
- **`TopLeftOrigin` must be true.** Without it every surface presents upside down.
- **Timeline semaphores are not available for this handoff.** Both interop backends report
  `Semaphores` and never `TimelineSemaphores`, so it is `UpdateWithSemaphoresAsync` with a
  pair of exportable binary semaphores.
- **An image and a semaphore are exported once each.** The importer owns each handle it is
  given, so a second export leaks a file descriptor per frame.
- **A compositor may have no GPU interop at all.** Measured on Linux: the OpenGL backend
  reports no importable image handle type, so Vulkan is the only path there. `Info` says
  why there is nothing on screen and the surface draws nothing rather than throwing. **A
  tool always draws something ordinary in that case.**

### Flipping Y reverses the winding

Vulkan clip space needs the flip, and that makes a mesh authored counterclockwise present
as clockwise in framebuffer coordinates. `FrontFace.Clockwise` with `CullMode.Back`, or the
near faces are culled and the inside of the far half of the model is what shows.

### Teardown has to finish while the compositor is running

Releasing an imported image is a message to the compositor, so disposing on window close
races the shutdown and the process dies inside the driver. **A window holding a surface
cancels its first close, awaits `ShutdownAsync`, then closes.**

### Resize

Bounds change every frame of a drag and each new size is a fresh set of images, so the size
is only adopted once the pointer has been still for 120ms. Frames keep being drawn at the
old size meanwhile. Nothing else may reallocate on `Bounds`.

Avalonia's Vulkan backend treats `VK_SUBOPTIMAL_KHR` as an error and throws on it from
`VulkanDisplay.EndPresentation` even though it is a success code. It appears on resize under
XWayland. It is noise rather than a failure.

### Testing

**The headless backend has no GPU interop, so nothing here draws in a test.** What a
headless test proves is the path with no device: `IsDrawing` is false, `Presented` is zero,
`Info` says why, and the tool's own empty state is what the window shows. A real frame is
checked with `CaptureNextFrame`, which reads the presented image back to a PNG, and that is
a run on a machine with a device rather than a test.
