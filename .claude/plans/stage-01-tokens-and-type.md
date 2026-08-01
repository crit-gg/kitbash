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
rewritten. Until then it needs the old palette, in one temporary file deleted in stage
5, so the old names cannot leak into new work.

**As built, the old names carry a `Legacy` prefix.** The plan first assumed the old
names could simply map onto the new brushes. They cannot. Nine collide with a Slate
token of the same name and a different value:

```
Accent  AccentInk  AccentTint  AccentTintLine  InkMuted
LineControl  Ok  OkInk  Warn
```

`AccentInk` is the one that shows why it matters. In the old palette it is accent
coloured text, `#6fabe8`. In Slate it is the text drawn on an accent fill, `#0d1a29`.
Merging the two dictionaries would have resolved those keys to whichever loaded last and
silently restyled the launcher, with every build still green.

So `src/Workbench/Themes/LegacyTokens.axaml` holds the 47 old brushes at their old
values under `Legacy` prefixed names, and 83 references across `LauncherWindow.axaml`
and `WindowChrome.axaml` were rewritten to match. The prefix is the point: new work
cannot reach an old value by accident, and a grep for `Legacy` lists exactly what stage
5 has to replace.

Fonts are the exception and took no prefix. Nothing about them changed except the
assembly they live in, so the launcher moved straight to `FontFamilyUi` and
`FontFamilyMono`.

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
- The launcher still runs and looks unchanged, on the prefixed old palette.
- A grep for a hex literal outside `Tokens.axaml` returns nothing in `Workbench.Ui`.

Resolve every key at runtime rather than trusting the build. A token whose type will not
parse, or a font whose `avares://` path is wrong, both build clean and fail silently, the
font by falling back to the default rather than erroring.

Two things about probing this, both of which gave a false failure first time:

- Resources declared inside a `Styles` are not in `Application.Resources`. Querying that
  dictionary reports every token missing. Look them up the way a consumer does, through
  `TryFindResource` on a control.
- Check a font by asking `FontManager` for its glyph typeface and reading back the family
  name. A `FontFamily` resource resolves whether or not the font behind it exists.

## As built

81 tokens in `Tokens.axaml`, 15 in `Typography.axaml`, 47 in the temporary
`LegacyTokens.axaml`. All 96 resolve, both fonts report their real family names, and all
47 legacy brushes match the originals exactly, including the opacity on `Scrim`.
