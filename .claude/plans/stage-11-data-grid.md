# Stage 11: data grid

**Built.** See Grids in `.claude/skills/workbench-surfaces` for what the library holds and
"Where this departed from the plan" at the foot of this file for what the design said that
this did not.

The densest surface in the system, and the one a data tool lives in. Both grids are
here, the flat one and the tree one, because they share a column layout and splitting
them would mean writing that twice.

## Goal

A grid that reads cleanly at 31px rows with thousands of rows, sorts, edits in place
and groups, and a tree grid that puts hierarchy in its first column.

## Both grids are ours

**Decided. Build the data grid and the tree data grid from scratch.** This is a
deliberate choice made after the option below was on the table, so do not reopen it and
do not quietly adopt a built in type partway through.

`TableView` ships in `Avalonia.Controls` and is not a Pro control, verified by reflecting
over 12.1.1. It was the plan for a while. It is not what this stage builds. `TreeDataGrid`
is the paid one and was never an option.

This is the one place in the library where the rule to theme what Avalonia ships is set
aside on purpose. The reasons:

- The tree grid has no built in type either way, since `TableView` is flat. Taking
  `TableView` for one grid means two column models, two row lifetimes and two sets of
  behaviour that have to be kept saying the same thing.
- Sorting, grouping, inline edit and the group header row are this stage's work under
  either choice. They are most of the stage.
- `TableView` was reflected, never used, so its behaviour under a real column set and how
  far its own themes reach are both unproven. Building on it means finding that out with
  the stage already committed to it.

What that costs, stated plainly, because all of it comes free with `TableView` and none of
it comes free here: column layout, keeping the header in step with a horizontally scrolled
body, virtualisation that survives a column resize, and the resize thumb itself.

**Virtualisation is not written from scratch.** The body is a `ListBox` underneath, the
way `ui:Tree` already is, since that is the only items control in Avalonia 12 that
virtualises. The row stays a `ListBoxItem`, so the seven row states from stage 9 apply to
a grid row with nothing added.

**One column model, used by both grids.** It is the reason the two are one stage. Write it
once and let the tree grid lay out the same columns the flat grid does.

## Build

**Rows.** 31px, separated by a `LineRow` `#2a2c30` hairline, with the alternating row on
`SurfaceRowAlt` `#212328`. Row states are the seven from stage 9 and follow the same
rule that a row never carries both a fill and an outline.

**Header.** Column titles at 11px weight 600 `InkMuted`, letter spacing `.1em`, on
`SurfaceRoot` since a header is chrome. A sort indicator on the active column, and
resizable column edges with the same generous hit area the splitter needed.

**Cells.** Identifiers, values, counts and paths in JetBrains Mono. Names and labels in
Archivo. Status cells reuse the stage 4 status pill rather than inventing a cell style.

**Inline edit.** A cell becomes a text field in place, keeping the row height. Commit on
Enter or blur, cancel on Escape. The editing row shows the modified mark.

**Group header.** A row that spans the grid, carrying the group name and aggregate
values for the group. Distinct from a tree grid branch row: this one is flat grouping,
not hierarchy.

**Toolbar.** Above the grid: a search field, a columns chooser and a primary action.
Uses stage 4 and stage 8 controls with no new styling.

**Footer.** Below the grid: selection count, the shown of total count and the sort in
force, all in monospace.

**Pagination is optional.** The design shows a rows per page dropdown and a page
selector, and those are built, but as a footer feature that can be turned off rather
than as the way the grid works. Virtualisation is the primary answer to a large set, so
the default is one continuous scrolling body with no pages. A caller that wants pages
switches them on.

The two must not fight. Sorting, selection and the counts all read the full set, not the
current page, so turning pagination on changes what is rendered and nothing else.

## The tree data grid

Hierarchy in the first column, aggregates on the branch rows. The branch row shows
rolled up values in the same columns its children use, in monospace, at `InkSecondary`
so it reads as a summary rather than as data.

Built on the stage 9 tree, which is where the hierarchy and the flat row list already
are. A tree grid is a tree that also lays out columns.

It takes the same column model the flat grid uses rather than a second one. That is the
whole reason this sits in stage 11 and not stage 9: the columns have to exist first.

Its group header is the branch row, which is hierarchy. The flat grid's group header
below is grouping, which is not. Two different rows, and neither one is the other.

## Build order

Each piece is verifiable before the next depends on it.

1. The column model and the layout it drives, over a fixed set of rows, unthemed. Every
   later step assumes columns measure and place, so prove that first.
2. The body over a `ListBox`, so virtualisation and selection are reused rather than
   written, with the header kept in step with a horizontally scrolled body.
3. The row, header and cell look.
4. Column resizing and the resize thumb, which has to survive virtualisation.
5. Sorting and the sort indicator.
6. Inline edit.
7. Grouping and group headers.
8. Pagination, because it is optional and nothing else may depend on it.
9. The tree data grid, last, since it takes the column model from everything above.

Steps 1 and 2 are where this stage can go wrong quietly. A column layout that only works
because every row is realised, or a header that drifts once the body scrolls sideways,
both look right on a short list and fail on a long one. Check both against ten thousand
rows before building anything on top.

Column virtualisation is a separate question and is not in scope. A grid with two hundred
columns is a real case for a data tool, but it is not one the design shows, so leave it
for later rather than building it now. Leave room for it in the column model rather than
writing something a later change would have to unpick.

## Done when

- Ten thousand rows scroll smoothly at 31px with the zebra intact.
- Sorting, grouping and inline edit work and look like the design.
- Selection of many rows shows a count in the footer.
- No cell sets its own brush.
- The tree grid lays out the same columns as the flat one, and its branch rows carry
  aggregates that read as a summary rather than as data.

All five hold. Measured in the gallery, headless with real drawing, over ten thousand rows:

```
deep    items=10000 extent=310000 realised=9 cells=63 alternate=4
columns pick=34@0 ID=190@34 NAME=546@224 KIND=150@770 VALUE=96@920 TIER=104@1016 STATE=118@1120
across  body=154 header=154 extent=1392 viewport=1238
resize  id 190 -> 250, name starts at 284, its cell at 34 width 250
group   'Graph' count=2500, click leaves rows 10004 -> 7504, caret now closed
cancel  1.5 typed 999 then escape leaves 1.5
footer  '2 selected' '100 of 10,000 shown' 'sorted by ID'
tree    indents L0:0 L1:14 L1:14 L1:14 L2:28 L2:28 L0:0 L0:0
```

Nine rows realised out of ten thousand, four of them wearing the stripe. The header sits at
exactly the offset the body does. A resize moves every column after it and the rows follow.
Escape puts the old value back through `IEditableObject`.

## Where this departed from the plan

Seven things above are wrong against the design page or against what the stage could hold.
The design was read again and won each time.

**Focus on a grid row is an inset line, not the halo.** This is the opposite of the answer
stage 9 reached for a list row, and both are right. A list row is spaced, so a halo has room
outside it. A grid row is flush against the rows above and below and against the frame, so
the same halo would land on its neighbours. The design draws `inset 0 0 0 1px
rgba(86,158,255,.6)` here and says so in its own caption. `FocusLine` is that value.

**A grid's chrome sits on `SurfaceRoot`.** The toolbar, the header and the footer are all
`#1e1f22` in the design, in three separate places, and this stage said the same. It is the
one surface other than a window frame that takes the root tone, and the reason is that a
grid is a document with chrome of its own rather than a panel.

**A column is pixel or star and never auto.** Auto has to measure every row, so a width that
only works while the whole set is realised is the one thing a virtualised grid cannot have.
`GridColumn.Width` refuses it outright rather than resolving it to something.

**The tree grid has no zebra and no indent guides.** The design draws neither. A branch row
takes `SurfaceRowAlt` instead, which is what tells a heading from what hangs under it. Guides
would be wrong as well as absent: they are drawn from the row's leading edge, and the design
puts a picker column in front of the names.

**The tree grid indents 14, not the 22 the design's markup steps.** The design disagrees with
itself here the way it did in stage 9, where the answer was 14 with the caret slot reserved on
every row. A tree grid is that tree, so it steps the same.

**The editing row draws no modified mark.** The plan asked for one. The design draws no mark
on a grid row anywhere and says the state in the status column instead, which is the column's
job. The row carries an `:editing` pseudo class so a view can say more if it has more to say.

**Pagination is a control of its own rather than a footer feature.** `ui:GridPager` sits under
the grid, which is where the design draws it, and a grid that never pages never carries it.
That is a stronger version of what this stage asked for: the grid does not know pages exist,
and turning them on is attaching the pager.

**The pager's numbers sit to the left of its steps, not between previous and next.** The
design draws them in the middle of the row, which means the run of numbers changes width
under the buttons and next walks away from the pointer as it is pressed. The numbers grow
into the gap on their left instead, and the readout on the right holds the room its widest
reading needs, so the four steps hold one place. Measured across pages 1, 2, 50, 99 and 100.

One more thing this had to decide, which the plan did not cover.

**A grid is given a `GridRows`, the way a tree is given a `TreeRows`.** Sorting, grouping,
paging and the stripe all need a view over the source rather than the source itself, and the
tree already set the shape. A plain list handed to `ItemsSource` draws rows and does none of
those, and nothing pretends otherwise.
