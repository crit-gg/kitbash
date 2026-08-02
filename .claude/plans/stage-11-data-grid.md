# Stage 11: data grid

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
