# Stage 11: data grid

The densest surface in the system, and the one a data tool lives in. Both grids are
here, the flat one and the tree one, because they share a column layout and splitting
them would mean writing that twice.

## Goal

A grid that reads cleanly at 31px rows with thousands of rows, sorts, edits in place
and groups, and a tree grid that puts hierarchy in its first column.

## Build it on TableView, not from scratch

`TableView` ships in `Avalonia.Controls`, the package this project already references,
and it is not a Pro control. Verified by reflecting over 12.1.1 rather than read from a
page. `TreeDataGrid` is the paid one and is still out.

That changes this stage from writing a grid to theming one. What comes with it:

| | |
|---|---|
| `TableView : ListBox` | so virtualisation is already there, and the row states from stage 9 apply because a row is a `ListBoxItem` |
| `TableViewColumn` | `Header`, `Width` as a `GridLength`, `Binding`, `CellTemplate`, and `CellTheme` and `HeaderTheme` for styling a column on its own |
| `TableViewRow` | a `ListBoxItem` holding `PART_CellsPresenter` |
| `TableViewCell` | a `ContentControl` that knows its `Column` |
| `TableViewColumnHeader` | holds `PART_Resizer`, a `Thumb`, so column resizing is built |
| `CanUserResizeColumns` | on the view, and `CanUserResize` per column |

What it does not give, and so is still this stage's work: sorting and the sort
indicator, grouping and group header rows, and inline editing. Build those over the
control rather than replacing it.

Read the type before designing against this table. It was reflected, not used, so its
behaviour under a real column set is unproven.

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

`TreeDataGrid` is a Pro control and is not an option, and `TableView` is flat, so this
one surface has no built in type behind it. It is the exception, and it is built on the
stage 9 tree, which is where the hierarchy and the flat row list already are.

Take the column layout from `TableViewColumn` rather than inventing a second one. That
is the whole reason this sits in stage 11 and not stage 9: a tree grid is a tree that
also lays out columns, so the columns have to exist first.

Its group header is the branch row, which is hierarchy. The flat grid's group header
below is grouping, which is not. Two different rows, and neither one is the other.

## What is ours and what is not

Decided, not open. The grid is `TableView` with a control theme over it. Do not plan
around `TreeDataGrid`, which is a Pro control.

That takes the expensive parts off this stage. Column layout, keeping the header in step
with a horizontally scrolled body, virtualisation that survives a column resize, and the
resize thumb itself all come with the control. What is left is the look, plus sorting,
grouping and inline edit built over it.

Read the control before budgeting. It was found by reflection, so its behaviour under a
real column set, and how far its own themes reach, are both unproven. If it turns out
not to carry this surface, say so and reopen the question rather than quietly writing a
grid.

Build it in this order, so each piece is verifiable before the next depends on it.

1. A `TableView` with real columns, unthemed, to find out what it does on its own.
2. The row, header and cell themes, which is the look.
3. Selection, which should be `ListBox` behaviour reused rather than written.
4. Sorting and the sort indicator.
5. Inline edit.
6. Grouping and group headers.
7. Pagination, because it is optional and nothing else may depend on it.
8. The tree data grid, last, since it takes the column layout from everything above.

Step one is not optional. Everything after it assumes the control behaves, and that is
the assumption to break early rather than late.

Virtualisation and column layout come from the control. Column virtualisation is a
separate question and is not in scope. A grid with two hundred columns is a real case
for a data tool, but it is not one the design shows, so leave it for later rather than
building it now.

## Done when

- Ten thousand rows scroll smoothly at 31px with the zebra intact.
- Sorting, grouping and inline edit work and look like the design.
- Selection of many rows shows a count in the footer.
- No cell sets its own brush.
- The tree grid lays out the same columns as the flat one, and its branch rows carry
  aggregates that read as a summary rather than as data.
