# Stage 6: launcher relayout

Rewrite `LauncherWindow.axaml` against the library. This is the milestone where the
app becomes Slate.

Read against `Workbench Launcher.dc.html` as it stands now. The design changed after
this plan was first written, and the shape below is the current one.

## Goal

561 lines of one off markup replaced by composition of `Workbench.Ui` controls, with
only launcher specific layout left behind.

## What changed in the design

The launcher is no longer one column. It is a title bar over a rail and a panel, and the
rail switches which panel is shown. That is the significant change, and it makes the
launcher a shell that holds pages rather than a single page.

- **An activity rail** down the left, 48px wide, holding Workspace, Godot engines and
  Settings. Settings sits at the bottom.
- **Godot engines is its own page**, `Engine Installs.dc.html`, reached from the rail.
  Not built in this stage. Stage 6 builds the shell that can host it and the workspace
  page that fills it.
- **The settings action left the workspace bar** and became the bottom rail item.
- **The tool card is down to four things**: the mark, the name with a version beside it,
  the description, and the action. It carried a status pill and a row of count chips in
  an earlier revision and both were taken out. The section label lost its count too.
- **The workspace list grew a per row overflow menu** and a second footer action.
- **The engine action button takes the colour of the engine state**, so it is not always
  the accent one.

## Nothing new is built here, with two exceptions

Every piece below already exists as a themed control. The launcher composes them and adds
layout. If this stage finds itself writing a control, that control belongs in
`Workbench.Ui` and probably belongs to an earlier stage.

| Launcher piece | What it is |
|---|---|
| title bar | `WindowTitleBar` from stage 3 |
| rail item | `ListBoxItem` in a `ListBox`, so selection is the control's own |
| rail tooltip | `ToolTip`, from stage 5 |
| workspace selector | a `DropDownButton` with a `Flyout`, from stages 4 and 8 |
| workspace row menu | `MenuFlyout`, from stage 5 |
| engine action | the stage 4 split button, in a semantic fill |
| engine menu | `MenuFlyout`, from stage 5 |
| engine and workspace badges | not the stage 4 status pill, see below |
| launch action | the stage 4 primary button |
| tool list | `ItemsControl`, or `ListBox` if a card ever becomes selectable |
| section label | a text style from stage 7, not a control |
| git strip readouts | plain text, not controls |

The two exceptions, both of which belong in `Workbench.Ui` rather than the launcher,
because a tool will want the same shell:

- **The activity rail.** A `ListBox` turned vertical with a themed 32px item. Selection,
  keyboard navigation and the selected state all come from `ListBox`, so what is written
  is a control theme, not a control. The bottom item is separated by margin rather than
  by being a different thing.
- **The page host.** Whatever shows one page at a time next to the rail. Start with the
  simplest thing that works and do not invent a navigation framework for three items.

## Layout

The window is 940 by 700. A title bar across the top, then a row of rail and page.

```
title bar                                   32, SurfaceRoot, LineSeam under
rail | page
 48    fills
```

The rail is `SurfaceRoot` with a `LineSeam` down its right edge, so the top and left
chrome read as one surface and the page sits inside it.

The workspace page is then a column of four bands.

| Band | Height | Ground | Contents |
|---|---|---|---|
| workspace bar | auto, 14px top and 13px bottom, 20px sides | `SurfaceRoot` | workspace selector |
| engine strip | 56 | `SurfaceNest1` | engine badge, version, badges, note, action |
| tools | fills, 16px top, 18px bottom, 20px sides | `SurfaceNest1` | section label, tool cards |
| status bar | 32 | `SurfaceRoot` | branch chip, counts, fetch |

Seams of `LineSeam` between every band. The `SurfaceRoot` bands at top and bottom bracket
the `SurfaceNest1` middle, which is the depth rule doing its job rather than a decision
to make.

## The pieces

**Activity rail.** 48px wide, items 32 by 32 with 4px between them and 8px at each end,
5px radius, icons at 20px. Resting icon is `#9ba6b0`, which is between `InkSecondary` and
`InkMuted` and is a value the palette does not have. Add it as a token rather than
writing it inline, and note it is the rail's own resting tone. Selected takes `AccentTint`
with an `Accent` icon. Hover is `StateHover` and pressed is `StatePressed`, as everywhere.
The Settings item is pushed to the bottom.

**Rail tooltip.** Shown to the right of the item, 22px tall, `SurfaceNest2` on
`LineControl`, 5px radius, `ShadowPopup`, 11.5px Archivo `InkPrimary`, and it drops in
over 80ms. This is the stage 5 tooltip with a placement, not a launcher control.

**Title bar icon.** The design draws a 15px square holding a letter. That is a
placeholder for an icon, not a lettermark to build. The launcher passes its own icon to
`WindowTitleBar.Icon` and nothing else changes.

**Workspace selector.** 40px tall, minimum 266px wide, `SurfaceNest2` on `LineSeam`, 5px
radius, border becomes `Accent` while open. A 9px state square at 2px radius, the name at
13px weight 600 `InkPrimary`, the path below at 11px monospace `InkSecondary`, and a 16px
chevron that rotates. The fixed minimum width and the path shortening already exist and
stay.

**Engine strip.** A 28px badge at 5px radius on `AccentTint` with `AccentTintLine`,
carrying two monospace letters in `AccentTintInk` at 11px weight 700. Then the version at
12.5px weight 600, then a row of badges, then a note line at 11px monospace
`InkSecondary`.

The badge row is a list, not one pill. It always carries the runtime badge, and carries a
state pill only when the engine state is worth saying. The runtime badge uses the `Data`
colour family rather than a semantic one, since a runtime is a fact and not a status.

**Engine action.** The stage 4 split button, filled with the colour of the engine state:
accent when the engine matches, warn when it mismatches, destructive when it is missing.
27px tall. See the stage 4 amendment below.

**Tool card.** A grid of 48px mark, flexible middle, action on the right, 15px between
them, 14px by 15px padding, `SurfaceNest2` on `LineControl`, 8px radius. The mark is 48px
at 8px radius on `AccentTint` with `AccentTintLine`, holding the tool's icon.

The design draws a letter in that square. It is a placeholder for the tool's icon, the
same way the title bar's square is, and it is not built. A tool supplies an icon or the
square is empty. No lettermark anywhere.

The middle is the name at 14.5px weight 600 with the version beside it in monospace at
11.5px `InkSecondary`, then the description at 12px on 1.45 line height. Nothing else.
The action is the primary button at 28px.

A card says what a tool is and offers one action. It does not report the tool's state or
its contents, so there is no pill and no chip row on it. That is the design's second
answer, not its first, and it is the one to build.

**Badges are not status pills, and this is the finding of this review.** The launcher
carries two badge kinds, and neither matches the status pill stage 4 built.

| Where | Parts | Height |
|---|---|---|
| engine strip | a round 5px dot and a monospace label | 17px |
| workspace row | a monospace label alone | 17px |

Stage 4's `StatusPill` draws a square mark, an icon and a label, and none of the three can
be turned off, because the theme page says a status reads as colour plus icon plus label
and a pill showing colour alone is a defect. That rule is right for a file's status. It is
not what these are. A runtime name and a workspace's access are labels, not statuses, so
they carry no icon and the workspace one carries no colour at all.

So the launcher needs a `Badge`: a label in a 17px pill at 8px radius, with an optional
round dot before it, taking a surface, a border and a text colour. Add it in stage 4
beside the pill rather than loosening the pill, because the pill's rule is worth keeping
and a badge that can drop its icon would quietly become a pill that can.

The tool card used to carry a real status pill and no longer does, so with the card
simplified the launcher uses no `StatusPill` at all. That does not make the pill dead. It
is the control a data grid's status cell uses, which stage 10 names.

**Status bar.** 32px. A branch chip at 22px and 8px radius on `SurfaceNest2` with a
`LineSeam` border, holding a 14px branch icon, the branch name in monospace with a 96px
minimum, the ahead and behind counts, and a chevron. Then a spacer, then the count
readouts as a 6px square mark plus a monospace value plus an Archivo label. Then the fetch
button at 22px and 5px radius with a 13px icon.

**Workspace list.** 436px wide, 5px radius, `SurfaceNest2` on `LineControl`,
`ShadowOverlay`, opening below the selector. A header row carrying the section label, a
scrolling body capped at 296px, and a footer.

A row is a 9px state square, the name at 12.5px weight 600, a badge pill, the path in
monospace at 11.5px, the action word, and an overflow button at 24 by 24 with the
`DotsVerticalRounded` glyph. The current workspace's row takes `AccentTint`.

The overflow menu is 212px and holds four items, the last of which is destructive and
takes `Error` text while keeping the ordinary hover. The footer holds two actions, each
27px with a 15px icon: add from a folder, and clone from git.

**The scrim is real.** The design darkens the whole window behind the workspace list with
`rgba(9,10,11,.62)`. A `Flyout` gives light dismiss but not a visible scrim, so decide in
stage 5 whether to brush Avalonia's light dismiss overlay or keep an explicit scrim panel.
Note it there rather than solving it here.

## Amendments this design forces on earlier stages

Fold these into their own stages rather than working around them here.

**Stage 3, the close button pressed tone.** Stage 3 derived `ClosePressed` as `#bd3a41`
because the design gave only the hover. The design gives the pressed value directly:
`#b53c42`. Done.

**Stage 4, the split button takes a kind.** The engine action is accent, warn or
destructive depending on the engine state, so the split button needs the same kinds the
plain button has, each naming its own hover and pressed tone. Done.

**Stage 5, tooltip placement and the scrim**, both noted above.

## No mock data

The design fills these with invented content. None of it is built.

- No recipe, machine or graph counts anywhere. The design removed the card's chip row,
  which removes the temptation with it.
- No invented workspaces. The registry already supplies real ones.
- No hardcoded Godot version, install path, runtime badge, branch name or ahead and
  behind counts. The engine strip and git strip are mock today. Either wire them to the
  real workspace root in this stage or render them empty. Do not carry the invented values
  forward.
- One tool card, Foundry, and it is not built, so it reports not installed.
- The engine menu and the workspace row menu are real menus with real commands, or they
  are not built. No item that does nothing.

`ITool` has to grow before the card can be driven properly, but far less than it did.
The card needs a mark, a name, a version and a description, and `ToolDescriptor` already
carries the last two of those. So this is one or two fields, not a state model. Either
extend it here or bind the card to a launcher view model and leave the contract alone.
Extending it is the better answer, since every tool will need the same fields, but it is
a contract change and belongs in its own commit.

## The empty state is not in the design

The design shows a launcher that always has a workspace. The app has to answer the case
where none has been added, and today it does, so keep it and rebuild it on library
controls. It is derived rather than specified, which is worth saying in the markup.

## Arrows

The git strip currently writes ahead and behind with arrow characters, which the copy
rules forbid. The design writes them as `ahead 2  behind 0` in words. Take the design's
form and the rule violation goes away on its own.

## Delete

- `src/Workbench/Themes/LegacyTokens.axaml`, the temporary old palette from stage 1, and
  with it every `Legacy` prefixed key. Grep the prefix to find them. At the end of stage
  1 there were 83 references across `LauncherWindow.axaml` and `WindowChrome.axaml`, and
  the second file moved to `Workbench.Ui` and off the legacy palette in stage 3, so only
  the launcher's should remain. A grep returning nothing is the check that this is
  finished.
- The `Button.action` style in `LauncherWindow.axaml`. It is a `Style`, so it beats the
  stage 4 control theme and is the reason the launcher shows none of stage 4 yet.
- Every brush, radius, font size and spacing literal in `LauncherWindow.axaml`.

## Icons

All eight the design names are already generated: `window`, `cube`,
`dots-vertical-rounded`, `git-branch`, `refresh-cw`, `folder-open`, `chevron-down` and
`cog`. No generator run is needed.

## Done when

- The launcher matches the design at 940 by 700, with the rail selecting the workspace
  page.
- No hex literal, no font size and no radius appears in the launcher markup.
- The rail keeps its selection through the keyboard as well as the pointer.
- The empty state still works and uses library controls.
- Nothing invented is on screen.
- The window still opens, moves, resizes, maximises and closes on Linux.
