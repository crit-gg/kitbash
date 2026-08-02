# Stage 5: overlays

Everything that floats: menus, dropdown popups, tooltips, popovers and modals.

## Goal

One overlay surface definition that every floating thing uses, so a menu and a
dropdown cannot drift apart.

## The overlay surface

Anything floating sits on `SurfaceNest2` `#2b2d31` with a `LineControl` `#3d4045` edge
and is separated internally only by hairlines. That is the whole rule. Shadow is
`ShadowPopup` for menus and `ShadowOverlay` for anything larger.

**Radius is 5px, including the popover.** An earlier note here said 8px for larger cards.
The design says otherwise, in as many words: the radius matches the control that opened
it, so a popover reads as an extension of its trigger rather than a separate floating
card. One radius for everything that floats.

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

**The picker shell, which three editors share.** Colour, date and time all open the same
way: the popup edge, a deep shadow, and a monospace readout in the footer of exactly what
will be written. Build the shell once here and let stage 8 and stage 13 fill it, because
three pickers that drift apart is the failure this section exists to prevent.

The footer carries Cancel and Apply when the edit commits more than one value at once,
which is the date and time case and the colour popover case.

**One body, two hosts.** A picker body has to work in the shell above and dropped
straight into a panel. In a panel it loses the shadow, takes the panel surface and loses
the buttons, because the value applies live and the footer only reads the literal back.
That is a rule about the body, so build the body knowing it, rather than discovering it
when a property panel wants a colour editor inline.

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
- Popup corners are uniform, verified on Linux.
- Escape and a click outside dismiss every overlay.

## As built

`Themes/Controls/Overlays.axaml` holds every floating thing in one file, so a grep for
`SurfaceNest2` there finds all of them at once. `MenuItem`, `Separator`,
`MenuFlyoutPresenter`, `ContextMenu`, `FlyoutPresenter` and `ToolTip` are all built in
types with a theme over them. `Popover` is ours.

**The surface is not aliased into a token.** Each theme names `SurfaceNest2`,
`LineControl` and `RadiusControl` itself, and a probe opens a menu, a popover and a
tooltip and asserts all three resolve to the same ground, edge and radius. Enforcement is
the measurement rather than a shared name, which is the same approach the rest of this
library takes.

**`Popover` is a `HeaderedContentControl` with a footer**, since a header over content is
exactly what that type already is and only the footer was missing. That one control is
both the workspace list and the picker shell: the shell is a popover whose footer holds a
readout and its buttons. Two uses, one control, which is why no separate `PickerShell` was
written.

Its `inPanel` class is the two hosts rule. Floating it takes the overlay surface and
`ShadowOverlay`. In a panel it drops both and keeps its body, which is what lets a picker
open in a popover and also sit inside a property panel without being built twice.

The template is two borders, the outer drawing the stroke and the shadow and the inner
clipping, because one border cannot do both without the corner artefact the launcher hit
once already.

**The scrim is an element a page draws, decided.** Avalonia's `LightDismissOverlayLayer`
is not public, so it cannot be brushed from a theme. `Scrim` is a token and the page that
covers itself draws it. That also keeps the design's other rule intact, which is that a
dialog has no scrim at all, because it is a real window and the window manager owns
modality rather than a dimming layer.

**A shortcut hint is spelled without symbols.** `GestureText` writes `Ctrl Return` rather
than the platform's punctuation, in the order a person says the modifiers, which keeps a
column of hints lining up in monospace and keeps the copy rules intact.

Two things measured on the way.

A `BoxShadow` cannot be cleared by a setter with an empty value. It throws
`InvalidCastException` at layout, not at build. `ShadowNone` is a zero shadow that is also
transparent, and it is what a control takes when it gives up its shadow.

A popup on X11 is its own operating system window, already recorded, and it bites the
test rather than the code: a menu and a tooltip are not descendants of the window that
opened them, so a probe has to reach them through a control it put inside them.

**A menu would not close on a click inside the window, and the cause was the window.**
Clicking outside the application dismissed it, clicking inside did not, which is the
signature of the light dismiss layer being absent rather than of anything in this stage.

`ChromelessWindow` was written from scratch in stage 3 and its template had no
`VisualLayerManager`. Adding one changed nothing, because `TopLevel` finds that part **by
name** and installs the overlay and adorner layers into it. Unnamed, it gets neither, and
nothing is logged. The fix is one attribute:

```xml
<VisualLayerManager Name="PART_VisualLayerManager">
```

Read out of Avalonia's own `Window` template by dumping a plain window's visual tree at
runtime, rather than guessed. Two wrong turns preceded it and are worth not repeating:
forcing `ShouldUseOverlayLayer` on every popup throws `Unable to create IPopupImpl and no
overlay layer is found`, which is the same missing layer reported from the other end, and
an unnamed manager looks right in the tree and does nothing.

This was a stage 3 defect, not a stage 5 one. Every Workbench window had no overlay layer
at all, so adorners and tooltips had nowhere to attach either. `stage-03`'s probe now
asserts the named part exists.
