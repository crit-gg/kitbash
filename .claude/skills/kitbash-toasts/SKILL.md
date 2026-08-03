---
name: kitbash-toasts
description: "Kitbash toast and alert rules. When something is a toast versus an alert, the toast service seam, dwell and grouping, the eight regions, deck layout, and the three alert forms. Read before raising a toast or drawing an alert."
---

### Toasts and alerts

How the app says something happened, and how it says something is wrong.

**If it describes something that just happened, it is a toast. If it describes the state of
what is on screen, it is an alert.** The same condition is never both. Anything a person
has to act on before continuing is an alert.

**A toast is a service taken through a constructor.** There is no static `Toast.Show`, no
ambient host and no service locator. `AddKitbashToasts` registers `IToastService`,
`IToastServiceFactory`, `IToastScheduler` and `ToastOptions`, all with `TryAdd`, so any of
the four can be replaced by registering it first.

```csharp
toasts.Show(new ToastRequest
{
    Tier = ToastTier.Error,
    Title = "Export failed",
    Body = "Godot 4.7.1 is not installed for this workspace.",
    Actions = [new ToastAction("Install engine", Install) { IsPrimary = true }],
});
```

**The service knows nothing about a visual tree.** It owns toasts, regions, dwell and
grouping, and `ui:ToastHost` watches it and draws. That split is the reason dwell, pausing
and grouping are checked with no window anywhere, which they are: a manual clock, 47
assertions, no Avalonia beyond the assembly reference.

**`IToastScheduler` is the whole seam.** It supplies the time, says whether the caller is
on the toast thread, runs work on it and starts the one timer. `ToastScheduler` is the only
file in the toast service that names Avalonia, and it is `Dispatcher.UIThread` and a
`Stopwatch`. Time is monotonic on purpose, so a clock correction cannot leave a toast
dwelling for an hour.

**`Show` runs on the toast thread and `Post` runs anywhere.** `Show` hands back the live
`Toast`, and it cannot, from another thread, because the answer depends on what is already
showing. A worker with something to say and nothing to follow up on calls `Post`. A worker
with long work to report raises the toast first and then writes `Progress` and `Body` from
wherever it likes, since every member of `Toast` marshals itself.

**Dwell.** 4 seconds, 8 when there is an action, and indefinite for an error, for anything
busy and for anything reporting progress. A dwell of zero or less stays until it is
dismissed. `ToastOptions` holds all of it and every other number the service behaves by.

**A toast that will not go away by itself draws no bar.** That is the rule the bar exists
for rather than a decoration. The bar says time left when a toast is counting down and work
done when it is reporting progress, which is why `Toast.Meter` has one name and not two.
**This departs from the design on the undo bar**, which the design draws without one. It is
the one short form carrying something worth pressing, and a hidden clock on a button a
person means to press is exactly what the bar prevents. The compact form has nothing to
press and keeps the design's plain pill.

**A repeat collapses rather than pushing the stack.** The same toast fired again lands on
the one already showing, takes its newer body, restarts its timer and counts. What counts
as the same is `ToastRequest.GroupKey`, and a null one is the tier, the form and the title
together, so a repeat collapses without anyone arranging it. Only onto the newest, so two
different toasts alternating stay two cards.

**Two actions, and never a destructive one.** A third is refused rather than trimmed, and
there is no destructive kind for a toast button to take. A toast is read after the fact and
often out of the corner of an eye, so nothing on one may destroy anything.

**Eight regions, each with its own stack, timers and limit.** Three deep, and the rest
collapse into a count. Top regions grow down, bottom regions grow up and the side centres
grow down from their midpoint, with the newest always nearest the anchored edge. A card
appearing top left never reflows the bottom right stack.

**A region is a deck, not a column.** The newest card is drawn whole and the ones behind
it show a strip of their top, scaled back and faded. `ui:ToastDeck` lays that out and is
ours because nothing in Avalonia does it. Three full cards would be most of a window and
would read as a list rather than as something passing through.

**Every region stacks the same way.** The newest card is at the foot of the deck and the
ones behind it pile up above it. What the anchor decides is where the deck sits and which
way it grows as cards arrive, not which end the newest is at, so a top region grows
downward with its newest card moving away from the top edge. There is no second layout to
get backwards, which is what the first attempt did: it mirrored the deck for the top and
centre regions and they came out layered the wrong way round.

A card behind is arranged as exactly the strip that shows, so the strips tile: one card's
foot is the next one's head. Cards are not all the same height, and a deck of mixed
heights cannot both put the newest on the anchored edge and show an even strip of each
card behind it. Stepping the tops leaves a short newest card floating clear of the edge,
and stepping the bottoms hides a short card behind a tall one completely. Giving each one
its strip settles both, and it is also what stops a faded card reading through to the text
of the card under it, which it did.

**A strip runs on under the card in front of it by the surface radius**, which is what
`ToastDeck.Tuck` is. A strip that stops level with the front card's edge leaves that
card's two corner curves showing the page through them, and a deck with daylight in its
corners is not a deck.

**A card behind keeps the whole shape of a card.** The tucked end is hidden rather than
changed. Squaring it off was tried and it is wrong for a reason only a corner shows: the
notch at each corner of the front card is filled by the card behind, and a square corner
with a straight edge running up through a curve reads as a card in front.

**The tuck is frame and never content.** `PART_Frame` is held back by the same amount and
clips there, so what runs on under the front card is fill alone. A card behind is faded,
so any of its own content left under the card in front reads through as a second line of
text across the deck. Measured twice, once for each way of getting it wrong.

The strip is 31, which is the card's own padding plus its status mark, so a card behind
shows a whole row rather than a mark sliced through the middle. The design draws 20 and
26 and both cut it.

The bar goes with it: a card behind draws none, since the bar lives along a foot that is
no longer there. Which card is in front is the card's own `ZIndex`, taken from its depth,
so it holds at either end of the deck.

The drawn order is the region's, not the view's: `IToastRegion.Visible` is newest first for
a region that grows down and newest last for one that grows up, so a stack is a plain
vertical list either way.

**Hovering pauses one region.** `ui:ToastStack` sets `IsPaused` on the region under the
pointer and nothing else, and the pointer reaches the stack because the cards are what it
lands on. An exit still runs while a region is paused, since a card that has been dismissed
is already gone as far as the person is concerned.

**A region belongs to whatever raised it.** The window owns the application's service and
a tool panel that reports inside itself asks `IToastServiceFactory` for another. Two
services means two sets of eight regions. `ui:ToastHost` clips, so the panel's cards stay
in the panel: measured, a 352 card in a 300 panel is cut at the panel's edge and the
window's service sees none of it. A panel narrower than a card usually wants the compact
form.

**The host is a `Panel` of eight overlaid stacks, not a three by three grid.** Measured
with the grid first and it was wrong twice: a 352 card in a third of a 1000px window was
cut off at both side edges, and three tall cards came to 280 in a 233 row, which arranged
from the top and put the newest card off the bottom of the window. A cell can be smaller
than a card and the whole surface never is. The cost is that two adjacent regions can
overlap when both are full, which is honest and rare.

**Entry and exit are separate mechanisms and must stay so.** Entry is a keyframe animation
on the card, `Opacity` and a real `TranslateTransform`. The depth scale is a
`TransformOperations` transition on `PART_Root`, a different element, because a keyframe
against a `TransformOperations` value does nothing at all and logs nothing. Exit is 120ms
and the service keeps a dismissed toast for exactly that long before removing it, so the
fade and the removal are one number.

**A card must not clip.** `TemplatedControl` clips to its bounds by default, and so does
`ItemsControl`, so the card, the stack and the stack's items control all turn it off.
Measured with the clips on: the shadow reached two pixels below a card, nothing to either
side, and left a dark wedge in each rounded corner where the card's bounds sit outside its
curve. Only the host clips, and that is what keeps a panel's toasts in the panel.

**An alert is content.** No shadow and no dwell, and it sits in the layout until the
condition goes away. The surface is the tier's tint with the tier's edge, which is why an
alert never draws a coloured bar down its left side. `IsOpen` takes it out of the layout,
so a view model binds the condition rather than wiring a click.

Three forms. **Block** is the default, wrapping, with a title, a detail and actions.
**Strip** is full bleed under a header, on the row height, one line, cut off rather than
wrapped, drawing only its own seam so whatever holds it supplies the frame. **Inline** is
attached to one field, no surface and no border, an icon and one line in the tier's own
colour.

The design's fourth form, in place, is the empty state and it was already built. It is
`StackPanel.emptyState`, on the surface it stands on rather than on a tint, so nothing was
added for it.

**Only an error recolours the control it is attached to.** It recolours the border and the
value and never the label, which is the `error` class and the `DataValidationErrors` state
on `TextBox`. A warning and a hint leave the field alone and say what they have to say in
an inline alert under it.

**An alert's two lines are strings, unlike every other header and content in this library.**
They are prose and they wrap, and a `ContentPresenter` has no way to say so. Anything
richer goes in `Actions`, which is a slot.

