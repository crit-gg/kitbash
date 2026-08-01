# Slate inventory

Everything `Theme Slate.dc.html` covers, and the token table the stages build from.
Values are taken from the design page and cross checked against the design project's
`CLAUDE.md`, which is the written spec.

## What the theme page contains

Thirteen sections. The count in brackets is how many distinct pieces the section shows.

1. **Identity strip** the name, the one line summary and the palette swatches.
2. **Window** (2) the shell alone, active and inactive. 32px title bar, outer edge,
   status bar. Inactive drops the whole chrome to the muted tier.
3. **Control inventory** (17) every control in five states: normal, hover, pressed,
   focus, disabled. Primary button, secondary button, ghost button, danger button,
   icon button, split button, dropdown, text field, search field, checkbox with a
   mixed state, radio, toggle, segmented, stepper, slider, list row, tab, chip,
   menu item.
4. **Selected and active** (4) the states that persist with no pointer: selected row,
   active tab, highlighted menu item, selected and focused together.
5. **Status pill** (5) colour plus icon plus label, never colour alone. synced,
   modified, conflict, checked out, archived.

   The launcher uses a smaller thing that is not this: a badge, which is a label in a
   17px pill with an optional round dot and no icon. The engine strip's runtime badge and
   the workspace row's access badge are both that. A badge names something, a status pill
   reports a state, and only the second one is bound by the colour plus icon plus label
   rule. Stage 4 builds both.
6. **Progress** (2) determinate, and indeterminate as the same bar with a travelling
   fill at 30 percent width.
7. **Panels** (5) panel header, panel footer, the depth ladder, the splitter, the
   empty panel with a call to action.
7a. **Value editors** (10) spinbox, colour, date, time, date and time, hyperlink, and
   four sliders: continuous, stepped, range, and paired with a spinbox. Each in all five
   states. Then three popovers: the colour picker in three shape modes, the calendar, and
   the clock. Stage 8 builds the editors, stage 5 the shell they share, stage 12 the
   picker.
8. **Tree** (7) disclosure arrow, indent guides at 14px per level, and the row states:
   normal, hover, selected, selected with focus, modified, drop target, disabled.
9. **Tree data grid** (1) hierarchy in the first column with aggregates on the branch
   rows.
10. **Data grid** (6) row states, sort indicator, inline edit, group headers, the
    toolbar above and the footer below with selection count and pagination.
11. **Docking** (8) ProportionalDock, DocumentDock, ToolDock, DockTarget, the three
    drop target states, the three tab states, the floating tool window.
12. **Overlays** (6) context menu, dropdown popup, tooltip, popover, modal, and the
    workspace list popup.
13. **Notes** the written rules repeated inline next to the controls they govern.

## Tokens

### Surfaces

The root tone is unique and never repeats. Everything nested below it cycles three
tones and starts again, so a grandchild never matches its grandparent. Nothing
lightens progressively with depth.

| Token | Value | Where |
|---|---|---|
| `SurfaceRoot` | `#1e1f22` | window frame, title bars, sidebars, toolbars, status bars, dialog footers |
| `SurfaceNest1` | `#25262a` | first level inside the root |
| `SurfaceNest2` | `#2b2d31` | second level, and the resting fill of every control |
| `SurfaceNest3` | `#31343a` | third level, then the cycle repeats at Nest1 |
| `SurfaceWell` | `#161719` | input wells |
| `SurfaceWellFocus` | `#1a1b1e` | an input well while it is focused, from the value editors |
| `SurfaceRowAlt` | `#212328` | the alternating row in a data grid |
| `SurfaceControlOff` | `#232427` | a disabled control's flattened fill |

### Lines

| Token | Value | Where |
|---|---|---|
| `LineSeam` | `#33353a` | structural hairline, and a control border on root or Nest1 |
| `LineRow` | `#2a2c30` | the row rule inside trees and grids, quieter than a seam |
| `LineControl` | `#3d4045` | a control border on Nest2, and the popup edge |
| `LineControlDeep` | `#43464e` | a control border on Nest3 |
| `LineWindow` | `#4a4e55` | the window outer edge, a step lighter since the desktop sits behind it |
| `LineWell` | `#565d66` | the border of a checkbox, radio or toggle well |
| `LineControlOff` | `#2c2e32` | a disabled control's border |

### States

| Token | Value | Where |
|---|---|---|
| `StateHover` | `#34363c` | every hover, control and row alike |
| `StatePressed` | `#3d4046` | every pressed fill |
| `AccentTint` | `#14293f` | selection tint, and any accent tinted surface |
| `AccentTintLine` | `#2b4a6b` | the border on an accent tinted surface |
| `AccentTintInk` | `#8fbef5` | text on an accent tinted surface |
| `SelectionInk` | `#cfe2fb` | text on a selected row |
| `AccentTintStrong` | `#1d3a58` | an accent tinted surface carrying a full accent border |

Selection is the tint alone. No left marker, and a row never carries both a fill and
an outline.

### Accent

| Token | Value | Where |
|---|---|---|
| `Accent` | `#569eff` | the one accent |
| `AccentHover` | `#6fadff` | |
| `AccentPressed` | `#3f86e0` | |
| `AccentInk` | `#0d1a29` | text and glyphs on an accent fill |
| `AccentInkPressed` | `#0b1622` | the same, on the pressed fill |
| `AccentMuted` | `#3f5f85` | the accent mark on an inactive window |
| `FocusHalo` | `#569eff` at 30 percent | a soft 2px halo hugging the control |

Focus never replaces a border, it sits outside one. Grid rows use a low alpha inset
accent line instead, so focus survives the selection tint.

### Text

| Token | Value | Where |
|---|---|---|
| `InkTitle` | `#e6ebf2` | window and panel titles |
| `InkPrimary` | `#e3e5e9` | body |
| `InkSecondary` | `#a9aeb6` | supporting text, default icon colour |
| `InkMuted` | `#7b8089` | section labels, shortcut hints |
| `InkDisabled` | `#5c6169` | disabled text |
| `InkCaption` | `#eef1f4` | caption button glyphs |
| `InkChip` | `#c9ced6` | chip text, and the toggle knob |
| `InkRail` | `#9ba6b0` | the resting icon in the activity rail, from the launcher design |

Disabled keeps its shape and flattens its fill. Never opacity.

### Semantic

Each role carries four values: the mark, the tinted surface, its border and its text.

| Role | Mark | Surface | Line | Ink |
|---|---|---|---|---|
| ok | `#4ec98a` | `#1a2b22` | `#2b4a38` | `#7cd6ad` |
| modified | `#e0a94a` | `#2d2416` | `#4d3d1c` | `#e8bd72` |
| error | `#ef6a6e` | `#2f1e20` | `#573034` | `#f4878a` |
| graph authored | `#a78bfa` | | | |
| pure data | `#5bc8a8` | | | |

Destructive is the one solid fill in the system: `#b0454a`, hover `#c25055`, pressed
`#973b40`, text `#fff1f1` and `#ffe8e8` when pressed. The close button hover is
`#d9494f`, which is its own value because it belongs to the window and not to a
control. `ClosePressed` is `#b53c42`, taken from the launcher design. Stage 3 had
derived `#bd3a41` from the hover before the design gave the value, so a checkout still
carrying the derived one is out of date rather than wrong on purpose.

Warn is the one semantic role that is ever flooded without being destructive, so it also
carries the tones a flood needs: hover `#ecb85f`, pressed `#c8933b`, and `#241802` as its
text on the flood. Data carries a tinted surface `#1b2c28` and border `#2f4a44` for the
badge that names a runtime.

**Nothing in this library paints with a translucent brush.** The design's markup lays a
white sheet over a coloured fill for hover. Every such case is a named tone here instead,
so a hover on a split button and a hover on a button are the same colour rather than two
ways of arriving near it.

Graph pin colours, from the spec rather than this page, for the tool that needs them
later: Float `#5bc8a8`, Int `#6ea8e8`, Bool `#d97b7b`, Enum `#a78bfa`, Struct and Exec
`#dde3ea`.

### Type

Archivo for anything read as language. JetBrains Mono for identifiers, values, counts,
paths and shortcuts. Weights 400, 500 and 600 only. The type floor is 11px.

Both families are already embedded under `src/Workbench/Assets/Fonts` and every file
reports the same family name, so weight selection works. Archivo Bold is present and
unused, since the spec stops at 600.

| Size | Weight | Where |
|---|---|---|
| 11px | 600 | section labels, pill text, tiny counts |
| 11px | 400, 500 | monospace values and hints |
| 11.5px | 400, 500 | the workhorse size, menu items and secondary text |
| 12px | 400, 500 | button text, list rows |
| 12.5px | 600 | window title, panel title |
| 13px | 400, 600 | workspace name |
| 14.5px | 600 | tool card name |
| 15px | 600 | dialog title |

Letter spacing: `.02em` on the window title, `.05em` on pill text, and `.1em` to
`.12em` on the small capitalised section labels.

### Shape and depth

Radius is 5px on every control and 8px on pills, cards, panels and window shells.
Nothing is square cornered. Small marks use 2px and 3px, and a knob or dot is round.
The 8px token is `RadiusSurface` rather than `RadiusPill`, because a window shell takes
it too and a window has no business reading a pill.

| Token | Value | Where |
|---|---|---|
| `ShadowFloating` | `0 6px 18px rgba(0,0,0,.5)` | a raised control |
| `ShadowPopup` | `0 10px 28px rgba(0,0,0,.55)` | menus and dropdowns |
| `ShadowOverlay` | `0 20px 48px rgba(0,0,0,.6)` | large popups and modals |
| `ShadowWindow` | `0 26px 64px rgba(0,0,0,.7)` plus `0 2px 6px rgba(0,0,0,.5)` | see below |

`ShadowWindow` is the one token the app deliberately does not use. A window that draws
its own frame casts its shadow into a transparent gutter, and both parts of this token
carry a vertical offset the gutter would clip into a hard line. The window shadow is
`drop-shadow(0 0 12 #60000000)` in a 12px gutter instead, decided and not open. Stage 3
has the reasoning. The token stays defined because it is what the design specifies and a
future technique may be able to draw it.

### Density

| Thing | Size |
|---|---|
| title bar | 32px |
| caption button | 32 by 32 |
| control | 26px, 27px where it holds two lines |
| tree row | 25px |
| grid row | 31px |
| icon button hit area | 24 by 24 minimum |
| chip | 21px |
| pill | 17px to 18px |
| gap between rows in a list surface | 2px |
| tree indent per level | 14px |

Icon sizes are 16px in tables, trees, the status bar and inline chips, and 20px in the
activity rail, launcher tool cards and empty states.

## The value editors dedupe

Worth recording because the section is the largest on the page and almost all of it is
already paid for. 47 distinct colours, 31 of them tokens that exist. One is new,
`#1a1b1e`, the focused well. The rest are the page's own furniture, gradient endpoints
and mock content.

The focus halo in that section is byte for byte the one stage 4 built, ticks are
`LineControl`, today's ring on the calendar is `AccentTintLine`, selection is
`AccentTint`, and adjacent month days are `InkDisabled`. Nothing new was needed for any
of it.

**One trap, measured.** The colour picker's swatch and recent grids are filled with the
Slate palette as example content, so a naive scan reads them as the section using those
tokens. It does not. `Destructive` appears five times in the section and all five are
swatches. Count a colour as used only where it is chrome. Stage 12 records the matching
rule that the grids ship empty.

## Where the design repeats a value

The design page uses 67 distinct hex values. Several are the same colour doing the
same job under two names, and a few are the same colour doing genuinely different
jobs. Resolve them like this.

**Collapse into one token.**

- `#14293f` appears as both the selection tint and the accent tinted surface. It is one
  idea, an accent wash on the ground. One token, `AccentTint`.
- `#34363c` is the hover for controls and the hover for rows. One token, `StateHover`.
- `#2b2d31` is the second nested surface and the resting fill of every control. The
  spec states that as a rule, that controls do not ride the depth ramp and stay at this
  tone. One token used by both, and a comment saying why.

**Keep separate, they will move apart.**

- `#e0a94a` as the modified mark against `#e8bd72` as modified text. The mark needs
  saturation at 5px and the text needs contrast at 11px.
- `#4ec98a` against `#7cd6ad`, for the same reason.
- `#0d1a29` against `#0b1622`, text on the accent fill and on the pressed accent fill.
- `#d9494f` close hover against `#b0454a` destructive. One belongs to the window, one
  to a control, and only one of them is a button fill.

**Two tokens, one value.**

`#3d4045` and `#3d4046` differ by one unit in blue and are indistinguishable on screen,
but they are not the same role. Checked across every use in the page:

- `#3d4045` is a line, in all 27 uses. 23 are a `border`, `border-top` or
  `border-bottom`, covering the control border on a `SurfaceNest2` ground, the popup
  outer edge, the hairlines inside a popup and a dashed placeholder outline. Two are the
  slider track, a 3px bar. One is a menu separator, a 1px bar. It is never a surface.
- `#3d4046` is a pressed fill, in all 13 uses: secondary button, ghost button, icon
  button, chip, tab, list row, menu item, segmented option and splitter thumb. It is
  never a line.

So keep `LineControl` and `StatePressed` as separate tokens that happen to resolve to
the same value. Merging them would fold a fill role into a line role and make a later
change to one of them a search through every use.

One open look question, not a token question. The chip specimen draws a `#3d4045`
border on a `#3d4046` fill, so a pressed chip loses its edge. Either that is intended,
the border dissolving into the press, or pressed should darken a step so bordered
controls keep an edge. Built as drawn until someone decides otherwise.

**Not tokens.** The theme page paints its own documentation chrome in `#0e0f10`,
`#eceff3`, `#7b848e`, `#6f7883`, `#191a1c` and a few others. Those belong to the page
that documents the theme, not to the app. Do not carry them across.

## Icons

The design names 48 icons and every one of them exists in the Box Icons Pro Solid
Rounded set at
`/home/jason/Seafile/gamedev-assets/icons/box-icons-pro-solid-rounded`, as
`bx-<name>.svg`. The mapping is direct with no substitutions needed.

```
alert-circle      alert-triangle   arrow-to-bottom  bolt
check             check-shield     chevron-down     chevron-right
chevron-up        cog              columns          copy
crosshair         cube             database         dots-vertical-rounded
factory           filter           folder-open      git-branch
git-commit        history          info-circle      layout
link              maximize         message          minimize
network-chart     package          play-circle      plus
plus-circle       refresh-cw       save             search
select-all        shapes           sitemap          table
tag               thermometer      timer            trash
undo              window           workflow         x
```

The theme page itself draws neutral geometric placeholders wherever an icon belongs,
so it shows position and size rather than the glyph. Take the glyph from the set.

Custom marks are drawn in house and are not in the set: the per tool launcher marks
and the data type glyphs for recipe, machine, product, attribute, stat, effect, tag
and the four graph kinds. Those are out of scope until a tool needs them.

## What the design leaves open

- The design targets Windows. The project targets Windows and Linux, so the window
  shell has to reach the same look through the Linux path recorded in
  `.claude/avalonia.md`.
- The spec names an Avalonia icon pack, `IconPacks.Avalonia.BoxIcons`. Stage 2 uses the
  local SVG set instead, so the app ships only the glyphs it uses and no dependency.
- Inactive window chrome is specified as a tier drop with no per token mapping. Stage 3
  has to derive it.
