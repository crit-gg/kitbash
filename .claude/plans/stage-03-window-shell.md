# Stage 3: window shell

Move `ChromelessWindow` and the frame into `Workbench.Ui` and restyle it to Slate, so
every tool gets the same window without copying anything.

## Goal

A tool author derives from one base class, supplies a title bar row, and has a window
that matches the launcher.

## Build

**Move.** `Views/ChromelessWindow.cs`, `Views/WindowTitleBar.cs` and
`Themes/WindowChrome.axaml` go to `Workbench.Ui`. The icon `ChromelessWindow` loads is
launcher specific, so the base class takes the icon rather than reaching for a fixed
`avares://` path. That is the one real change to its behaviour.

`WindowTitleBar` already exists and already owns the icon, the title, the caption
buttons, the move drag and the double click, with the window's content slot for
anything a window adds. So the Slate restyle is values in one `ControlTheme` rather than
markup in every window. It mirrors its window onto itself as the classes
`nativeChrome`, `maximized` and `fixedSize`, so no selector reaches across the window
into a template. Keep that, and add new state the same way.

`IWindowSettings` moves nowhere. It already lives in `Workbench.Core/Settings` and is
already registered by `AddWorkbenchApplicationStorage`, so a tool that composes core
services gets it without doing anything.

**Two frames, not one.** The window already supports both, and the Slate restyle has to
keep that working rather than assume the drawn frame.

`UsesNativeChrome` on `ChromelessWindow` is fed from
`IWindowSettings.UseNativeChrome`, a global user only setting under the key
`window.nativeChrome`. It drives two mutually exclusive classes, and every style in
`WindowChrome.axaml` hangs off one of them:

| Class | Frame | Caption buttons | Drag | Double click |
|---|---|---|---|---|
| `chromeless` | drawn by Workbench, `WindowDecorations="None"` | shown | moves the window | toggles maximise |
| `nativeChrome` | drawn by the desktop, `WindowDecorations="Full"` | hidden | nothing | nothing |

So every value in the table below applies to the `chromeless` case. Under
`nativeChrome` the desktop owns the outer edge, the corner radius and the shadow, and
Workbench must not draw its own. The title bar row itself stays either way, because it
carries the icon and the title, and because the launcher hangs its own content off it.

Under `nativeChrome` the row is ordinary content rather than a title bar, so both of
its gestures are off. `BeginMoveWindow` and `ToggleMaximizedFromTitleBar` each return
early, which means a window that wires the row the standard way needs no test of its
own.

The row also collapses entirely under `nativeChrome` when the window put no content in
it, which is the launcher's case. Restyling must not break that, so check all three:
drawn frame, native with no content, native with content. Measured today, the row is
36px, absent with height 0 and its template never realised, and 36px with the caption
panel hidden.

`ToggleMaximized` stays unguarded and remains available to code. Measured: under
`nativeChrome` the gesture leaves the window `Normal` while a direct `WindowState`
assignment still maximises.

**Restyle to Slate.**

| Thing | Value |
|---|---|
| title bar height | 32px, down from 40 |
| title bar and window ground | `SurfaceRoot` |
| seam under the title bar | `LineSeam` |
| window outer edge | `LineWindow`, a step lighter because the desktop is behind it |
| window radius | 8px |
| window shadow | unchanged, see below |
| title text | 12.5px weight 600, `InkTitle`, letter spacing `.02em` |
| caption button | 32 by 32, square, `InkCaption` glyphs |
| caption hover | `StateHover`, pressed `StatePressed` |
| close hover | `#d9494f` with white glyph, pressed a shade darker |

**Keep the current glyphs.** The minimise, maximise and close marks are already right
and the user asked for them unchanged. Everything else about the buttons may move.

**The shadow does not change. Decided.** It stays the 12px gutter with
`drop-shadow(0 0 12 #60000000)` that is already built. Do not restyle it, and do not
wire `ShadowWindow` into the frame.

The reason it is settled rather than open: Avalonia 12 has no usable shadow API for a
window that draws its own frame, so the shadow is an effect falling into a transparent
gutter, and anything past the gutter is clipped at the window edge. A clipped gaussian is
a hard line, not a soft edge, so the shadow must have no offset and its blur must equal
the gutter. A bigger shadow is only ever a bigger gutter, and the gutter is part of every
window's `Width` and `Height`.

Slate's `ShadowWindow` is `0 26px 64px rgba(0,0,0,.7)` plus `0 2px 6px rgba(0,0,0,.5)`.
Both carry a vertical offset and the first has a 64px blur, so drawing it literally needs
a gutter of about 90px on every window and a shadow this technique cannot make symmetric.
The window shadow therefore deviates from the design on purpose. That is the recorded
answer, not something to revisit while restyling.

**Inactive chrome.** The design shows an inactive window dropping the whole chrome to
the muted tier. The spec does not map that per token, so derive it: title text falls
from `InkTitle` to `InkMuted`, caption glyphs from `InkCaption` to `InkSecondary`, the
accent mark to `AccentMuted` `#3f5f85`. Drive it from the window's active state, not
from focus of a child.

## Cross platform

The design targets Windows. This project targets both, and the Linux path is the
constrained one.

`.claude/avalonia.md` records the measurement: role based decorations do not work on
Linux in Avalonia 12.1.1, because `X11Window.SetExtendClientAreaToDecorationsHint`
returns early unless an experimental option is set. So the frame stays manual, with
`WindowDecorations="None"`, `BeginMoveDrag` and `BeginResizeDrag`, which is what the
current implementation already does. Keep the `ElementRole` tags. They cost one
attribute, they document intent, and they go live if the platform ever honours them.

Two things follow that are easy to get wrong.

- Every clickable area needs a `Background`, even `Transparent`. A control with no
  background is not hit tested, which is how the caption buttons ended up clickable
  only on the glyph.
- Verify geometry by logging `ClientSize`, never by screenshot.

## Dialogs

The spec says dialogs are real windows with their own title bar and a deep shadow, and
that there is no scrim. That is a departure from the usual Avalonia modal, so build a
`DialogWindow` in `Workbench.Ui` now rather than discovering it in stage 8. It differs
from the main window in that it is not resizable, has only a close button, and its
footer sits on `SurfaceRoot`.

## Done when

- The launcher window is 32px titled, 8px cornered, and edged with `LineWindow`.
- Caption glyphs are unchanged from today.
- Clicking anywhere inside a caption button works, verified with `InputHitTest` after
  layout rather than at `Opened`.
- Activating and deactivating the window moves the whole chrome between tiers.
- A second window created from `Workbench.Ui` alone, with no launcher code, looks the
  same.
- Both frames still work. With `window.nativeChrome` on, the desktop draws the frame,
  the caption buttons are gone, the title bar double click does nothing, and Workbench
  draws no edge, radius or shadow of its own. Verify by running, not by reading. A
  dispatcher pump will not do it, since the window manager's state notification only
  arrives when the platform event loop runs, so use a `DispatcherTimer` and let the
  loop turn between the action and the check.
