# Stage 1: tokens and type

Create `Workbench.Ui` and put the Slate token set in it. Nothing looks different when
this stage lands, because nothing consumes the tokens yet. That is the point. It is a
pure substitution with a known blast radius.

## Goal

One place that answers what colour, what size, what radius, and no answer given twice.

## Build

**The project.**

```
src/Workbench.Ui/Workbench.Ui.csproj      Avalonia, Workbench.Core
src/Workbench.Ui/Themes/Tokens.axaml      brushes and scalar values
src/Workbench.Ui/Themes/Typography.axaml  families, sizes, weights
src/Workbench.Ui/Themes/WorkbenchTheme.axaml  the one entry point a consumer includes
src/Workbench.Ui/Assets/Fonts/            moved from src/Workbench
```

Add it to `Workbench.slnx`. `src/Workbench` references it. `Workbench.Core` does not,
and must not, so the contract stays free of a UI framework.

A consumer includes one thing:

```xml
<StyleInclude Source="avares://Workbench.Ui/Themes/WorkbenchTheme.axaml" />
```

**The tokens.** Take the table in `theme-inventory.md` verbatim. Apply its findings on
repeated values first, so nothing is entered twice by accident.

Name by role, not by appearance. `SurfaceRoot`, not `GreyDark`. A reader changing the
theme later needs to know what a value is for.

Two tokens may share a value when they are different roles. `LineControl` and
`StatePressed` do, and stay separate for that reason. What must not happen is one role
written out twice, or a token named for a colour rather than a job.

**Type.** Archivo and JetBrains Mono, weights 400, 500 and 600. The sizes in the
inventory become named resources, so a control asks for `FontSizeBody` rather than
carrying 11.5.

**Scalars.** Radius, control height, row height, icon size and the shadow definitions
become resources too. The density table is as much a part of the theme as the palette.

## Replaces

`src/Workbench/Themes/Tokens.axaml` goes away. Its 40 brushes were named for how they
look and carry six near identical grounds, which is the duplication the new set exists
to avoid. Nothing may reference the old keys after this stage.

The launcher keeps working through stage 4 because stage 5 is where its markup is
rewritten. Until then it needs the old keys or a temporary alias sheet. Prefer the
alias sheet, one file, deleted in stage 5, so the old names cannot leak into new work.

## Detail worth getting right

- Brushes go in a `ResourceDictionary`, not a `Styles`. Getting that wrong fails at
  runtime rather than at build, which `.claude/avalonia.md` records.
- Use `DynamicResource` at every consumer so a later theme swap is possible. Static
  resolves once.
- Do not add a light variant. Slate is one theme. Leave the `ThemeVariant` plumbing
  alone until something asks for it.
- The focus halo is a colour plus a thickness, not a brush alone. Carry both.

## Done when

- `Workbench.Ui` builds for `linux-x64` and `win-x64`.
- Every value in the inventory table exists exactly once.
- The launcher still runs and looks unchanged, through the alias sheet.
- A grep for a hex literal outside `Tokens.axaml` returns nothing in `Workbench.Ui`.
