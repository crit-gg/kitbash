# A list that virtualises rows of different heights

Every list surface in this app virtualises, which is a rule the build plan already states.
Avalonia's `VirtualizingStackPanel` does that well when every row is the same height and
badly when they are not, and the engines page is the first place rows are not.

Not a Slate stage. It sits beside them, it is library work, and two things already waiting
need it.

## What is wrong, measured

Measured 2 August 2026 on the engines page, 183 release cards, a closed card 61 tall and an
open one about 580. The list was scrolled to four offsets and the scroll extent read at
each.

| Scrolled to | Extent it reported | Containers realised |
|---|---|---|
| 0, nothing open | 1730 | 8 |
| 0, one card open | 2310 | 6 |
| 200 | 6196 | 5 |
| 1000 | 2948 | 10 |
| 4000 | 1730, and the offset clamped to 1187 | 8 |
| back to 0 | 2136 | 2 |

**The same list reports an extent between 1730 and 6196 depending on where it is
scrolled.** That is the whole bug. The scrollbar thumb changes size as it moves, dragging
it lands somewhere other than where it was released, and asking to scroll to 4000 stops at
1187 because by then the list believes it is shorter than that.

**Why.** `VirtualizingStackPanel` knows the height of the rows it has realised and nothing
about the rest, so it takes the average of what it can see and multiplies by the count.
With one tall row among short ones, that average is a different number at every offset. It
is a reasonable estimate for a uniform list and it is wrong here by a factor of three.

Nothing about the app causes it. Measured with the row template reduced to a bare
`TextBlock` and the behaviour is the same, so it is not what a row draws.

## What it takes

`VirtualizingPanel` is public and has the surface this needs. Confirmed by reflecting over
Avalonia 12.1.1 rather than read from a page:

```
abstract Control ContainerFromIndex(int index)
abstract int IndexFromContainer(Control container)
abstract IEnumerable<Control> GetRealizedContainers()
abstract Control ScrollIntoView(int index)
abstract IInputElement GetControl(NavigationDirection direction, IInputElement from, bool wrap)
        ItemContainerGenerator ItemContainerGenerator
        IReadOnlyList<object> Items
        void AddInternalChild / RemoveInternalChild / RemoveInternalChildRange
virtual void OnItemsChanged(IReadOnlyList<object> items, NotifyCollectionChangedEventArgs e)
```

So a panel of our own is buildable. `MeasureOverride` and `ArrangeOverride` come from
`Panel`, and owning the extent means implementing `ILogicalScrollable`, which is how the
built in one tells a `ScrollViewer` how tall it is.

## The shape

**Remember what each row measured.** One array of heights the length of the item count,
filled in as a row is realised and kept after it is recycled. An index never measured takes
an estimate, and the estimate is the average of what has been measured rather than a
constant, so a list settles as it is scrolled instead of drifting.

**The extent is a sum, not a product.** Add the array. That is the number that was wrong
above, and it is exact for everything already seen.

**A height that changes has to move the offset with it.** A card opening above the viewport
grows the content above what is on screen, and leaving the offset alone would slide
everything under the pointer. When a measured height replaces an older one for an index
above the first visible row, add the difference to the offset in the same pass. This is the
part that is easy to get subtly wrong and it is what a test has to cover.

**Recycling stays the framework's.** `ItemContainerGenerator` does that already and
`ui:Tree` records what it costs to get wrong: a container is prepared and has to be untold
in the same place, including when it is kept and moved.

## What this is for

Two things beyond the engines page, which is why it is library work rather than a fix on
one view.

- **Stage 11's two grids.** A tree data grid has rows that open, so it has the same problem
  the moment it lists anything real.
- **`ui:Tree`.** It virtualises today and its rows are one height. A tree row that wraps or
  carries a second line would meet this immediately.

## Hazards

**Do not start this without the measurement above to check against.** The failure is not
visible in a screenshot and is easy to declare fixed. The check is the extent reading the
same number at every offset, and a drag landing where it was released.

**An `ItemsPanel` setter is accepted and ignored.** Recorded in the root `CLAUDE.md` and
measured on a `ListBox`: the property reports the panel that was asked for while the
realised panel is the default one. So this cannot be dropped in by naming it in a theme,
and how a list is given the panel has to be settled first.

**Keyboard navigation is part of the contract.** `GetControl` is abstract for a reason, and
a list that scrolls correctly and cannot be arrowed through is not finished.

## Until then

The engines page keeps `VirtualizingStackPanel`. Scrolling a list with an open card is
imprecise and everything else about the page is right, which is the better trade against
realising 183 cards, the 348 to 709 ms that cost, and the same rule broken the other way.
