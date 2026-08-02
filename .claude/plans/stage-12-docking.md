# Stage 12: docking

Adopt Dock and theme it to Slate. This is the last stage because it consumes almost
everything the earlier ones built.

## The library

`Dock.Avalonia` by Wieslaw Soltes, https://github.com/wieslawsoltes/Dock, MIT.

Version 12.1.0 is current and tracks Avalonia 12.1, which is what this project uses.
Packages needed: `Dock.Avalonia`, `Dock.Avalonia.Themes.Fluent` as the base to override,
and one model package. Prefer `Dock.Model.Mvvm` so the docking model uses
CommunityToolkit observables like the rest of the app, rather than pulling ReactiveUI in
for one feature.

The design names Dock's own types directly, so the mockup was drawn against this
library: `ProportionalDock` splits the window, `DocumentDock` holds editable documents,
`ToolDock` holds the panels around them, and `DockTarget` is the drop indicator. That is
a strong signal to use its layout model as it comes and to spend the effort on theming
rather than on a parallel abstraction.

## Where it lives

`Workbench.Ui` takes the dependency, so tools get docking without each one wiring it.
The launcher does not dock anything and should not pay for it. If that separation
matters, split a `Workbench.Ui.Docking` project so the launcher references only
`Workbench.Ui`. Decide when the first tool exists, not now.

## Theme

**The whole surface gets themed.** Not the parts that are easy to reach. A docked tool
that is Slate everywhere except its scrollbar, its splitter grip or its pin button reads
as unfinished, and those are exactly the parts a stock theme leaves behind.

Start with one tab and one drop target. That is a spike to learn how Dock's themes are
put together and where they resist, not a decision point about whether to continue. If
the spike goes badly the answer is a different theming technique, not a smaller scope.

Dock is the one third party control package in this project, and it is the exception to
the rule that a control is a built in Avalonia type with a theme over it. The rule still
applies underneath it: where Dock builds on a stock type, such as a `TabItem`, a
`ScrollViewer` or a `GridSplitter`, the theme for that type is the one already written,
not a second one for docking.

Techniques in order of preference, and expect to need more than one:

1. Override the `ControlTheme` for a Dock type in `Workbench.Ui`. A `Style` beats a
   `ControlTheme`, measured and recorded in `.claude/avalonia.md`, so an override in the
   app wins over the packaged theme without touching it.
2. Replace the whole `ControlTheme` where the packaged one has no hook for what the
   design needs.
3. Vendor the relevant theme file from Dock into `Workbench.Ui` and edit the copy, if a
   part is not reachable any other way. Record which file and which version it came
   from, so a Dock upgrade has a starting point.

Take stock of the full surface before starting, so nothing is discovered late. At
minimum: tab strip and tabs, tab close and overflow, document tab against tool tab,
dock target indicators, the splitter between docks, the floating window frame, pin and
unpin affordances, the tool chrome header and its buttons, scrollbars inside docked
content, and the empty dock state.

Every value comes from stage 1 tokens. No Fluent value survives.

**Tabs.** Three states from the design: active, inactive, and inactive dock, which is a
tab in a dock that does not hold focus. Active takes an inset 2px `Accent` line, from the
`inset 0 2px 0 #569eff` in the design page. Inactive is `InkSecondary` text with no
fill. Inactive dock drops further to `InkMuted`.

**Drop targets.** Three states, and all three have to be visually distinct at a glance
because they appear during a drag when the user cannot read:

| State | Look |
|---|---|
| available | a quiet `LineControl` outline on a faint fill |
| under the cursor | `Accent` outline over `AccentTint` |
| disallowed | `#ef6a6e` outline, no fill |

**Floating tool window.** A real window using the stage 3 shell, with `ShadowWindow` and
`LineWindow`. It is a window, so it gets the window treatment rather than a panel one.

**Pinned.** A collapsed tool showing as a tab on the dock edge, expanding on hover or
click.

**Panel surfaces.** Docked content sits inside the depth ladder from stage 7. A dock is a
container, so it adds a level, and its content starts one deeper. Verify the tones cycle
correctly through a dock boundary, because that is where an attached depth property is
most likely to be dropped.

## Per view layout

The original requirement was that docking is configurable per major view, and that a
layout survives a restart. Dock serialises its layout, so the work is choosing where the
file goes. It is per person and per machine and not shared through a workspace, so it
belongs in application state under `IApplicationState`, keyed by tool and view, not in
workspace settings.

## Risks

- Dock's own control themes may not expose every part this design needs to restyle. The
  spike answers how, not whether. Vendoring a theme file is the accepted fallback and
  the cost of it is a Dock upgrade becoming manual.
- The library is a large surface with its own opinions about the model. Keep the docking
  model inside the tool, and do not let dock types leak into `Workbench.Core`.
- Floating windows on Linux go through the same manual chrome path as every other window
  here, so they inherit the constraints in `.claude/avalonia.md` rather than getting
  native decorations for free.

## Done when

- A harness shows a proportional split with a document dock and two tool docks.
- A tool tears out into a floating window and redocks.
- All three drop target states are distinguishable during a real drag.
- A layout survives a restart.
- Docked panels obey the depth ladder across the dock boundary.
- Nothing in a docked view still looks like Fluent. Walk the surface list above and check
  each one, including the scrollbars and the empty dock.
