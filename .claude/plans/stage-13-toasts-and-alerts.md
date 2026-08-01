# Stage 13: toasts and alerts

How the app says something happened, and how it says something is wrong.

Last, because a toast is a popover nobody opened and an alert is a panel nobody asked
for, so both are cheap once the overlay surfaces and the panels exist.

## Depends on

Stage 2 for icons, 4 for buttons and the semantic tiers, 5 for the overlay surface a
toast borrows, and 7 for the panel an alert sits in.

## Goal

Anything in the app can report a transient result without reaching for a global, and any
surface can carry a persistent condition without inventing a look for it.

## The rule that decides which one

**If it describes something that just happened, it is a toast. If it describes the state
of what is on screen, it is an alert.** The same condition never appears as both.

Two more from the design, both worth enforcing rather than documenting:

- A toast never carries a destructive action, and never holds the only copy of anything.
  Whatever is worth keeping is also in the log or the problem list.
- Anything the user must act on before continuing is an alert, not a toast.

## Toasts are built by hand, and injected

No toast library. This is a control library with a service behind it, and it is written
the way everything else here is written.

- **`IToastService`, taken through a constructor.** No static `Toast.Show`, no ambient
  host, no service locator. A view model that reports progress is handed the service the
  same way it is handed a file system.
- **Registration lives beside the others.** `AddWorkbenchToasts` in `Workbench.Ui`,
  following the shape `AddWorkbenchIO` and the rest already have, with `TryAdd` so a
  caller can substitute its own.
- **The service knows nothing about Avalonia's visual tree.** It owns toasts, regions,
  dwell and grouping. A `ToastHost` control observes it and draws. That split is what
  makes the timing testable without a window.

**This adds a dependency.** `Workbench.Ui` references Avalonia and `Workbench.Core` today
and nothing else. Registration methods mean
`Microsoft.Extensions.DependencyInjection.Abstractions`, which is the same package Core
already uses for its own. Take the abstractions package rather than the full one, so the
library asks for the contract and the application picks the container.

`Workbench.Core` does not gain toasts. Core is the contract and stays free of a UI
framework, and a toast is a UI thing.

## Toasts

A toast takes the popover treatment exactly: `SurfaceNest2` on a `LineControl` edge with
`ShadowOverlay`, 8px radius, 24px controls. It is stage 5's surface reused, not a second
definition of it.

**Five tiers**, four semantic and one busy. The mark is the same tinted pair the chips
and status marks use, so nothing new enters the palette.

**Anatomy**, and none of it is optional except the body.

| Part | What |
|---|---|
| status mark | 20px tinted square, semantic colour plus icon, never colour alone |
| title | Archivo 600 at 12px, one or two lines, wraps rather than truncates |
| body | optional, the secondary tier, the detail needed to act |
| actions | 24px buttons, at most two, and the primary one is accent only when it is safe to press without reading |
| timer | a 2px bar in the semantic colour, absent on a toast that requires an action |

That last column is a rule, not a decoration: a toast with no timer is a toast that will
not go away by itself, and it should look like one.

**The busy mark spins its glyph, not its square.** The square holds still, on
`SurfaceNest3` with a `LineControlDeep` edge at 5px, and a 13px `RefreshCw` turns inside
it at 1.1s linear. Recorded because the design was corrected on this exact point: the
first version rotated the whole mark, which swung the tint and the border around with it.

The spin is the one continuous animation in the library, and `.claude/avalonia.md` has
the trap. A keyframe on `RenderTransform` throws at startup, and one against a
`TransformOperations` value does nothing at all and logs nothing. Give the glyph a real
`RotateTransform` and animate `RotateTransform.Angle`.

**Four variants.** Compact for an acknowledgement, an undo bar for a reversible edit,
progress for long work, and a grouped form when the same kind repeats.

**Stack and motion.** Newest nearest the anchored edge, older cards scale back and fade,
three visible at once and the rest collapse into a count. Entry and exit are 120ms, a
fade with the height collapsing and the stack sliding to follow.

**Dwell.** 4s for an acknowledgement, 8s when there is an action, and indefinite for an
error or anything still running. Hovering pauses every timer in that region and only
that region. A toast that fires more than twice in a row collapses into the grouped form
with a count rather than pushing the stack.

## Regions

**Eight anchors, all of which can be occupied at once**: four corners, two side centres,
top centre and bottom centre. This is the part most toast implementations get wrong, so
build it in rather than adding it later.

Each region owns its own stack, its own dwell timers and its own three deep limit. A card
appearing top left does not reflow the bottom right stack. That is what lets a tool panel
report its own progress while the window reports a save.

- **Ownership.** A region belongs to whatever raised it: the window for app level, a tool
  panel for tool level. A panel's region clips to the panel rather than the window.
- **Growth.** Top regions grow down, bottom regions grow up, the side centres grow down
  from their midpoint. The newest card is always nearest the anchored edge.
- **Entry.** A card slides 12px from the nearest edge. The two centre columns slide
  vertically instead, having no near edge.
- **Default.** Bottom right, unless the toast is about something visible elsewhere, in
  which case it goes to the region nearest that thing.

A panel region clipping to its panel is the requirement that decides the design. A host
control per region, resolved from the service, rather than one host at the window.

## In content alerts

An alert is content. No shadow and no dwell, and it sits in the layout until the
condition goes away. The surface is the semantic tint with its own edge, the same pair
the chips use, so **an alert never needs a coloured bar down its left side**.

**Four forms.**

| Form | Where |
|---|---|
| block | the default, between the things it concerns, wraps freely, dismissible only when advisory |
| strip | full bleed at the top of a panel, document or dialog, on the 31px row height, one line, ellipsis rather than wrap |
| inline | attached to one control, no surface and no border, an icon and one line in the semantic text tier, under the field |
| in place | the empty state, replacing the content entirely |

A strip replaces neither the title bar nor the status bar. It sits under the header of
the thing it applies to and scrolls away with it.

**Only an error recolours the control it is attached to**, and it recolours the border and
the text, never the label. A warning or a hint leaves the field alone.

An alert carrying an action always has a way to reach the full detail, because the alert
itself is a summary.

## Not in this stage

The problem list and the log an alert points at. Both are surfaces of their own and
neither is designed yet. Build the alert so it can address one, and leave the target for
whoever builds it.

## Done when

- A view model reports a toast through an injected service, with no static anywhere in
  the path.
- Two regions carry toasts at once and neither reflows the other.
- A toast raised by a panel clips to the panel.
- Dwell, pause on hover and repeat grouping are verified without a window, because the
  service owns them.
- A toast with a required action shows no timer bar.
- Every alert form takes the semantic tint and none of them draws a left bar.
- Only an error recolours its control, and never the label.
