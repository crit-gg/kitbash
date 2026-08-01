# Stage 5: launcher relayout

Rewrite `LauncherWindow.axaml` against the library. This is the milestone where the
app becomes Slate.

## Goal

561 lines of one off markup replaced by composition of `Workbench.Ui` controls, with
only launcher specific layout left behind.

## Layout, from Workbench Launcher.dc.html

The window is 940 by 700, a column of five bands.

| Band | Height | Ground | Contents |
|---|---|---|---|
| title bar | 32 | `SurfaceRoot` | app mark, title, caption buttons |
| workspace bar | auto, 14px padding | `SurfaceRoot` | workspace selector, settings icon button |
| engine strip | 56 | `SurfaceNest1` | engine badge, version, note, split button |
| tools | fills | `SurfaceNest1` | section label, tool cards |
| status bar | 32 | `SurfaceRoot` | branch chip, counts, fetch button |

Seams of `LineSeam` between every band. The two `SurfaceRoot` bands at top and bottom
bracket the `SurfaceNest1` middle, which is the depth rule doing its job rather than a
decision to make.

**Workspace selector.** 40px tall, minimum 266px wide, `SurfaceNest2` on `LineSeam`,
border becomes `Accent` while open. A 9px state square, the name at 13px weight 600,
the path below at 11px monospace `InkSecondary`, and a chevron that rotates. The fixed
minimum width and the path shortening already exist and stay.

**Engine strip.** A 28px rounded badge on `AccentTint` with `AccentTintLine` and
`AccentTintInk`, the version at 12.5px, an optional status pill, a note line in
monospace, and the split button from stage 4 on the right.

**Tool card.** A grid of 48px mark, flexible middle, action on the right. 14px by 15px
padding, `SurfaceNest2` on `LineControl`, 8px radius. The mark is a lettermark on
`AccentTint` until a real mark is drawn. Name at 14.5px weight 600, a status pill, a
version in monospace, a description at 12px, then a row of chips.

**Status bar.** A branch chip on `SurfaceNest2`, a spacer, count readouts as mark plus
value plus label, and a fetch button.

## No mock data

The design fills these with invented content. None of it is built.

- No recipe or machine counts. The chip row is driven by whatever a tool actually
  reports, and is empty until it reports something.
- No invented workspaces. The registry already supplies real ones.
- No hardcoded Godot version, branch name or ahead and behind counts. The engine strip
  and git strip are mock today. Either wire them to the real workspace root in this
  stage or render them empty. Do not carry the invented values forward.
- One tool card, Foundry, and it is not built, so it reports not installed.

`ITool` has to grow before the card can be driven properly. It currently says nothing
about a mark, a version, a state or counts. Either extend it here or bind the card to a
launcher view model and leave the contract alone. Extending it is the better answer,
since every tool will need the same fields, but it is a contract change and belongs in
its own commit.

## Arrows

The git strip currently writes ahead and behind with arrow characters, which the copy
rules forbid. The design writes them as `ahead 2  behind 0` in words. Take the design's
form and the rule violation goes away on its own.

## Delete

- `src/Workbench/Themes/Tokens.axaml`, already gone in stage 1.
- `src/Workbench/Themes/LegacyTokens.axaml`, the temporary old palette from stage 1, and
  with it every `Legacy` prefixed key. Grep the prefix to find them. At the end of stage
  1 there were 83 references across `LauncherWindow.axaml` and `WindowChrome.axaml`, and
  the second file moves to `Workbench.Ui` in stage 3, so it should be on Slate tokens
  before this stage begins. A grep returning nothing is the check that this is finished.
- `src/Workbench/Themes/Icons.axaml`, replaced by the generated one.
- Every brush, radius and size literal in `LauncherWindow.axaml`.

## Done when

- The launcher matches the design at 940 by 700.
- No hex literal, no font size and no radius appears in the launcher markup.
- The empty state still works and uses library controls.
- Nothing invented is on screen.
- The window still opens, moves, resizes, maximises and closes on Linux.
