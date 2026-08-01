# Stage 10: data grid

The densest surface in the system, and the one a data tool lives in.

## Goal

A grid that reads cleanly at 31px rows with thousands of rows, sorts, edits in place
and groups.

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
Uses stage 4 and stage 6 controls with no new styling.

**Footer.** Below the grid: selection count, the shown of total count and the sort in
force, all in monospace.

**Pagination is optional.** The design shows a rows per page dropdown and a page
selector, and those are built, but as a footer feature that can be turned off rather
than as the way the grid works. Virtualisation is the primary answer to a large set, so
the default is one continuous scrolling body with no pages. A caller that wants pages
switches them on.

The two must not fight. Sorting, selection and the counts all read the full set, not the
current page, so turning pagination on changes what is rendered and nothing else.

## The control is hand built

Decided, not open. The grid is built in house on `ItemsControl` with a virtualising
panel, the same way every other control in this library is built. The tree data grid in
stage 9 is ours too.

Do not plan around TreeDataGrid. It is licensed, not free.

Budget accordingly. The visuals are the cheap part. The expensive parts are column
layout, keeping the header in step with a horizontally scrolled body, virtualisation
that survives column resize, and inline edit inside a recycled container. Expect this
stage to be the largest in the plan.

Build it in this order, so each piece is verifiable before the next depends on it.

1. Column definitions and measurement, with a fixed header and no scrolling.
2. Virtualised rows against the column layout, reusing the stage 9 flat row list.
3. Horizontal scroll with the header tracking the body.
4. Sorting and the sort indicator.
5. Selection, reusing the stage 9 row states.
6. Inline edit.
7. Grouping and group headers.
8. Pagination, last, because it is optional and nothing else may depend on it.

Vertical virtualisation comes from stage 9 and is not rebuilt here. Column
virtualisation is a separate question and is not in scope. A grid with two hundred
columns is a real case for a data tool, but it is not one the design shows, so leave the
column layout able to accept it later rather than building it now.

## Done when

- Ten thousand rows scroll smoothly at 31px with the zebra intact.
- Sorting, grouping and inline edit work and look like the design.
- Selection of many rows shows a count in the footer.
- No cell sets its own brush.
