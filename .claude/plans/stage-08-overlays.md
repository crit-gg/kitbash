# Stage 8: overlays

Everything that floats: menus, dropdown popups, tooltips, popovers and modals.

## Goal

One overlay surface definition that every floating thing uses, so a menu and a
dropdown cannot drift apart.

## The overlay surface

Anything floating sits on `SurfaceNest2` `#2b2d31` with a `LineControl` `#3d4045` edge
and is separated internally only by hairlines. That is the whole rule. Radius 5px for
menus and dropdowns, 8px for larger cards. Shadow is `ShadowPopup` for menus and
`ShadowOverlay` for anything larger.

## Build

**All built in types.** `ContextMenu`, `Menu`, `MenuItem`, `Separator`, `MenuFlyout`,
`Flyout`, `Popup` and `ToolTip`. The overlay surface is one set of setters shared by
their themes, which is what stops a menu and a dropdown drifting apart.

**Context menu and menu items.** 26px rows, 5px radius, 11.5px Archivo `InkPrimary`,
with the shortcut hint right aligned in 11px JetBrains Mono `InkMuted`. Hover
`StateHover`, pressed `StatePressed`. A separator is a hairline with 5px by 7px margin.
A destructive item takes `#ef6a6e` text and keeps the same hover.

**Dropdown popup.** The list a dropdown opens. Same surface, and it must clear the
control by a small gap rather than touching it. It virtualises, because a data tool will
open one over every recipe or every attribute, and a popup is the easiest place to
forget that. Build it on `ListBox`, which already virtualises, rather than on a stack of
items in an `ItemsControl`, which does not.

**Tooltip.** Small, same surface, a title line and an optional body. Delay comes from
Avalonia defaults unless it looks wrong. Placement does not: the launcher's activity rail
puts its tooltip to the right of a 32px item, 22px tall, and it drops in over 80ms, so
placement and the drop are part of the theme rather than left to the default.

**Popover.** A larger floating card, 8px radius, used for content rather than a list of
actions.

**Modal.** A real window, per the spec, with its own title bar and a deep shadow, and no
scrim. This is a departure from the usual pattern and the reason `DialogWindow` was
built in stage 3. The modal here is the content convention: 15px weight 600 title, body,
and a footer of actions sitting on `SurfaceRoot`, with the destructive action as the
solid danger button.

**The scrim, which is a real decision.** The launcher design darkens the whole window
behind the workspace list with `rgba(9,10,11,.62)`. A `Flyout` gives light dismiss but
draws nothing, so either brush Avalonia's light dismiss overlay layer or keep an explicit
scrim panel over the window. Decide it once here, because a modal and a workspace list
that dim the window differently would be obvious.

Note the spec's other rule alongside it: a dialog has no scrim. So the scrim belongs to a
popup that covers a page, not to every floating thing.

**Workspace list popup.** The launcher already has one. Move its surface definition to
the library and leave its content in the launcher. It is the worked example of a popup
with a header, a scrolling body and a footer of actions.

## What the platform actually does

Measured and recorded in `.claude/avalonia.md`: on X11 with default options a `Popup`
is a separate operating system window whose `TopLevel` is a `PopupRoot`, not a layer
inside the parent window. `IsUsingOverlayLayer` is false.

Two consequences.

- The popup solves its own corners, transparency and shadow. It does not inherit them
  from the window. The corner artefact already hit once in the launcher came from
  exactly this, and the fix was two borders, one drawing the stroke and one clipping,
  because `ClipToBounds` on the stroke border clips children to the outer rounded
  rectangle.
- `ShouldUseOverlayLayer` can force the in window path per popup if a floating window
  ever proves wrong on a given desktop. Know the switch exists before needing it.

Opening animation is a translate plus a fade, matching the design's 110ms drop. Animate
`TranslateTransform.Y`. Animating `RenderTransform` throws at startup.

## Done when

- Every floating surface in the app resolves to the same brushes from one definition.
- A menu, a dropdown and a tooltip open next to each other and are indistinguishable in
  ground, edge and radius.
- The workspace popup looks unchanged after moving to the library.
- Popup corners are uniform, verified on Linux.
- Escape and a click outside dismiss every overlay.
