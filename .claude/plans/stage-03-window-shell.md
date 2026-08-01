# Stage 3: window shell

Move `ChromelessWindow` and the frame into `Workbench.Ui` and restyle it to Slate, so
every tool gets the same window without copying anything.

## Goal

A tool author derives from one base class, supplies a title bar row, and has a window
that matches the launcher.

## Build

**Move.** `Views/ChromelessWindow.cs` and `Themes/WindowChrome.axaml` go to
`Workbench.Ui`. The icon it loads is launcher specific, so the base class takes the
icon rather than reaching for a fixed `avares://` path. That is the one real change to
its behaviour.

**Restyle to Slate.**

| Thing | Value |
|---|---|
| title bar height | 32px, down from 40 |
| title bar and window ground | `SurfaceRoot` |
| seam under the title bar | `LineSeam` |
| window outer edge | `LineWindow`, a step lighter because the desktop is behind it |
| window radius | 8px |
| window shadow | `ShadowWindow` |
| title text | 12.5px weight 600, `InkTitle`, letter spacing `.02em` |
| caption button | 32 by 32, square, `InkCaption` glyphs |
| caption hover | `StateHover`, pressed `StatePressed` |
| close hover | `#d9494f` with white glyph, pressed a shade darker |

**Keep the current glyphs.** The minimise, maximise and close marks are already right
and the user asked for them unchanged. Everything else about the buttons may move.

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
