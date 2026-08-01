# Avalonia 12 notes

Researched against the 12.1.1 tag, the official v12 breaking changes page and the
12.0 and 12.1 release notes. Avalonia 12 is recent and much online material still
describes 11, so prefer this file and the sources at the bottom over memory.

## Baseline

- Avalonia 12.1.1 on .NET 10. Version 12 dropped .NET Framework and netstandard.
- Skia is the only rendering backend. Direct2D was removed.
- `Avalonia.Diagnostics` was removed. The replacement is `AvaloniaUI.DiagnosticsSupport`
  with `AttachDeveloperTools()`, which is a paid tier.

## Window chrome

This is the area that changed most, and the area where version 11 advice is wrong.

Removed in 12: `ExtendClientAreaChromeHints`, `TitleBar`, `CaptionButtons`,
`ChromeOverlayLayer`. Renamed: `SystemDecorations` is now `WindowDecorations`, with
values `None`, `BorderOnly` and `Full`.

There are two supported ways to draw your own frame.

**1. Tag your own elements with roles.** Set `ExtendClientAreaToDecorationsHint="True"`,
then mark elements with the `WindowDecorationProperties.ElementRole` attached property.
Roles are `TitleBar`, `ResizeN` through `ResizeSW`, `CloseButton`, `MinimizeButton`,
`MaximizeButton`, `FullScreenButton`, `DecorationsElement` and `User`. The platform
then treats those elements as frame, so dragging, resizing, snapping and the system
window menu are native. The role has no effect unless
`ExtendClientAreaToDecorationsHint` is true.

**2. Retheme `WindowDrawnDecorations`.** A control that supplies the whole frame.
Give it a `ControlTheme` built from `WindowDrawnDecorationsTemplate` and
`WindowDrawnDecorationsContent`, which has `Underlay`, `Overlay` and `Popover` layers.
It exposes `DefaultTitleBarHeight`, `DefaultFrameThickness` and
`DefaultShadowThickness`, the parts `PART_TitleBar`, `PART_CloseButton`,
`PART_MinimizeButton` and `PART_MaximizeButton`, and the pseudo classes `:normal`,
`:maximized`, `:fullscreen`, `:has-shadow`, `:has-border`, `:has-titlebar`,
`:has-minimize` and `:has-maximize`. Avalonia's own Fluent theme for it is the best
worked example.

Other notes:

- On Windows `ExtendClientAreaToDecorationsHint` was fixed in 12. The old margin
  workarounds people applied when maximized should be removed.
- The framework passes shadow extents to the platform, so the frame shadow belongs to
  the platform and the theme, not to app drawn effects.
- `WindowState` is now a direct property, so it cannot be set from a style. Selectors
  such as `[WindowState=Maximized]` still read it.
- `TopLevel` is no longer guaranteed to be the visual root. Use
  `TopLevel.GetTopLevel(visual)` rather than casting.

### Roles do not work on Linux yet

Measured on 12.1.1 on this machine, with the hint set from the window constructor:

```
hint=True titleBarHint=-1 decorations=Full extended=False   backend=XID
hint=True titleBarHint=-1 decorations=Full extended=False   backend=Wayland
```

`IsExtendedIntoWindowDecorations` stays false, so the roles never take effect.

The cause is in `X11Window.SetExtendClientAreaToDecorationsHint`, which returns early
unless `X11PlatformOptions.EnableDrawnDecorations` is set. That option is
`[Experimental("AVALONIA_X11_CSD")]` and its own message reads "Experimental, used
mostly for testing". When it is enabled, X11 maps the roles onto `_NET_WM_MOVERESIZE`,
which is the real native move and resize protocol, so the design is right and only the
gate is closed.

`WaylandPlatformOptions` has no equivalent option at all, so on a Wayland session there
is currently no way to turn this on.

So on Linux, custom chrome still needs `WindowDecorations="None"` with `BeginMoveDrag`
and `BeginResizeDrag`, which is what our `ChromelessWindow` does. The launcher keeps
its `ElementRole` tags anyway: they cost one attribute each, they document intent, and
they become live if the option is ever enabled or on a platform that honors the hint.

## Styling and theming

- A `Style` matches controls by selector and layers on top. A `ControlTheme` replaces a
  control's whole look and is keyed by type. Use `ControlTheme` to redefine a control,
  `Style` to adjust one.
- Selectors support `/template/` to reach template parts, `^` to refer to the styled
  control inside a `ControlTheme`, `[Property=Value]`, classes and pseudo classes.
- Styles belong in `Application.Styles` or `Control.Styles`. Resources belong in
  `Application.Resources`. A `Styles` file is included with `StyleInclude` and a
  `ResourceDictionary` with `ResourceInclude`. Swapping the two fails at runtime, not
  at build.
- Use `DynamicResource` inside control templates so a theme change is picked up.
  `StaticResource` resolves once.

## Bindings

- Compiled bindings are on by default in 12, so `x:DataType` is required on views and
  on every `DataTemplate`.
- `IBinding` was removed. Everything derives from `BindingBase`. `Binding` now always
  means `ReflectionBinding`. In code prefer `CompiledBinding.Create`.
- `InstancedBinding` was removed. The equivalent is `BindingExpressionBase`.
- Properties registered with `enableDataValidation` now report errors automatically.
  Remove `UpdateDataValidation` overrides that only forwarded the error.

## Text

- `TextBlock.LetterSpacing` moved to `TextElement.LetterSpacing` and is an inherited
  attached property. Writing it on a `TextBlock` in XAML still works.
- `RenderOptions.TextRenderingMode` moved to `TextOptions.TextRenderingMode`, which
  also carries `TextHintingMode` and `BaselinePixelAlignment`.

## Resources and fonts

- Assets are addressed as `avares://AssemblyName/Path`.
- Embedded fonts are `AvaloniaResource` items, referenced as
  `avares://AssemblyName/Folder#Family Name`.
- When embedding separate weight files, check that every file reports the same family
  name, otherwise weight selection silently fails. `fc-scan` shows the family a file
  reports.

## Testing

- `Avalonia.Headless` 12.1.1, with `Avalonia.Headless.XUnit` or `Avalonia.Headless.NUnit`.
  Version 12 moved to xUnit v3 and NUnit 4.
- `Avalonia.Headless.Vnc` runs a headless app that can be viewed over VNC.
- Headless runs with no display, so it is the right way to check UI behavior here.
  Screenshots of a live desktop are not, because they capture whatever else is open.

## Platform backends

- `UsePlatformDetect` selects X11 on Linux. `Avalonia.Wayland` 12.1.1 is a separate
  package enabled with `UseWayland`, or a fallback form called after `UsePlatformDetect`
  that uses Wayland when a compositor is available.
- This machine is a Wayland session, so the app runs through XWayland today.
- Windows 12.1 added `Win32Properties.WindowCornerPreference` for Windows 11 corners.

## Hit testing

A `Border` with no `Background` is not hit tested. Anything clickable needs a
background even when it paints nothing, so give it `Transparent`. A control whose only
background comes from a `:pointerover` setter can never be hovered, because the pointer
never reaches it in the resting state.

This bit the launcher: the caption buttons only responded on the few pixels their glyph
covered, and clicks elsewhere in the button fell through to the title bar and started a
window drag. Verify with `this.InputHitTest(point)`, but run it after layout. At
`Opened` the tree is not measured yet and every hit returns null.

## What a title bar has to do

Taken from GTK's own client drawn title bar, `gtk/gtkwindowhandle.c` and
`gtk/gtkwindowcontrols.c`, which is the reference implementation, plus the GNOME
defaults in `org.gnome.desktop.wm.preferences`.

A title bar handles exactly three configurable gestures, each mapped to an action of
`none`, `toggle-maximize`, `lower`, `minimize` or `menu`:

| Gesture | GNOME default |
|---|---|
| Primary double click | `toggle-maximize` |
| Middle click | `none` |
| Right click | `menu` |

So middle click doing nothing is correct. Right click normally opens a window menu,
which GTK asks the compositor for and otherwise builds itself. **Workbench leaves
right click unhandled on purpose**, so do not add a window menu back as a fix.

A primary press starts a move drag, and a press with more than one click cancels that
drag so it cannot fight the double click.

The caption buttons are real buttons. They carry tooltips and accessible labels, the
maximize button swaps its glyph and tooltip to restore while maximized, and it is not
created at all when the window cannot resize.

Avalonia has no API for `lower` or for the system window menu, so the menu is built in
the app, which is the same fallback path GTK takes.

## Wayland backend maturity

`Avalonia.Wayland` 12.1.1 inflates window height. Measured with the same build, a
window asking for 940 by 700:

```
X11       clientSize=940, 700
Wayland   clientSize=940, 728
```

X11 is exact. The backend was tried and removed from this project. Treat it as young
if it is ever revisited.

## Traps already hit in this project

- `ExtendClientAreaChromeHints` does not exist. Build error, easy to spot.
- `TextOptions.TextRenderingMode` is not settable the way version 11 set it.
- A runtime identifier cannot be passed to a solution, only to a project.
- An undecorated window was once sized to the whole screen by the compositor. Verify
  window geometry by logging `ClientSize` rather than trusting a screenshot.

## Sources

- Breaking changes: https://docs.avaloniaui.net/docs/avalonia12-breaking-changes
- Release notes: https://github.com/AvaloniaUI/Avalonia/releases
- Decorations source: `src/Avalonia.Controls/Chrome/` at tag 12.1.1
- Fluent decorations theme: `src/Avalonia.Themes.Fluent/Controls/WindowDrawnDecorations.xaml`
- SourceGit, a well built Avalonia app, though its chrome targets version 11:
  https://github.com/sourcegit-scm/sourcegit
