---
name: kitbash-controls
description: "Kitbash UI control rules. Icons, the six button kinds, control theme precedence and lookup, the status bar, input wells and value editors, focus and disabled states, and the gallery. Read before writing or changing any ControlTheme, input, button or icon."
---

## Icons

Box Icons Pro, Solid Rounded, one weight, no mixing in outline or duotone. Draw one with
the `Icon` control and nothing else:

```xml
<ui:Icon Glyph="GitBranch" Size="16" />
```

Sizes are 16 in tables, trees, the status bar and inline chips, and 20 in the activity
rail, tool cards and empty states. The colour is inherited, so an icon follows the row or
button it sits in. Set a foreground only when the icon carries its own meaning, such as a
semantic mark.

`Glyph` is an enum, so a name that does not exist will not compile. Both the enum and the
geometry come from `tools/icons/generate.py`, which reads `tools/icons/icons.txt`, then
`tools/icons/local/`, then the licensed set outside the repo. **To add an icon, add its
name to that list and rerun the generator.** Never hand edit `Themes/Icons.axaml` or
`Controls/IconGlyph.cs`. The outputs are committed so a clean checkout builds without the
set present, and `generate.py --check` proves they have not drifted.

**A mark that is not in the set lives in `tools/icons/local/`**, which is read first, so a
brand mark is in the repo where it belongs and a glyph can be replaced without renaming it.
A file there goes through the same reader and the same checks as the set, which means it
has to arrive already normalised: a 24 by 24 box, paths and rects, no fill. The generator
is a reader and not a converter, so a glyph cannot quietly change shape because a
conversion improved. `local/README.md` says how a mark is fitted, and records what the
Godot one cost.

The set fills 18 to 20 units of its 24 box. A mark that fills more reads as bigger and
heavier than everything beside it, so fit the longer side to 19 and centre on 12,12.

The set mixes single paths, multiple paths and rects, so the generator merges shapes and
rewrites rects. The merge has three traps that all fail silently, recorded in
`.claude/plans/stage-02-icons.md`. Read that before changing it.

## Controls

**Theme what Avalonia already ships. Build only what it does not have.** Every basic
control is a built in type with a `ControlTheme` over it. Before writing a control, name
the built in type it should have been. `https://docs.avaloniaui.net/controls` is the
list, and anything not marked Pro is ours to use. Paid, and therefore out:
`TreeDataGrid`, Charts, Markdown, MediaPlayer, On Screen Keyboard and RichTextEditor.

Only two controls here are hand built, and only because Avalonia has no type for either:
`Chip` and `StatusPill`. `Icon`, `ChromelessWindow`, `WindowTitleBar`, `DialogWindow`,
`DialogFooter`, `SurfacePanel`, `Segmented`, `PathField`, `Alert` and the three toast
controls are ours for the same reason. `SurfacePanel` is the smallest of those: a `HeaderedContentControl`
plus a footer, since no built in type carries a header, a body and a footer at once.

`Tree` is the one that is a subclass rather than a new control. It derives from `ListBox`,
because a tree that virtualises is a flat list of the rows that can be seen and Avalonia has
no virtualising tree of its own. See the `kitbash-surfaces` skill.

Theme the whole type rather than reaching past it. Where a control publishes settings for
a theme to drive, such as `ProgressBar.TemplateSettings`, use them and take the values it
computes. Holding a built in control to a number it does not compute means owning it.

One `ControlTheme` per type in `Kitbash.Ui/Themes/Controls`, all merged by
`KitbashTheme.axaml`. A view names a kind and never a value.

**Fluent sits underneath, and it is what makes an unthemed control work.** Every
executable loads `<FluentTheme />` before `KitbashTheme`, so a type this library has not
themed still has a template and still draws. That is why a `ScrollViewer` scrolls and an
`ItemsControl` lists without either appearing in `Themes/Controls`.

So a theme written here is always a replacement rather than a first look, and dropping one
in takes away whatever Fluent was doing for that type. Do not add a bare theme to a type
that is working. Measured: with Fluent removed, an `ItemsControl` has a null template and
draws nothing at all, silently.

```xml
<Button Classes="primary" Content="Add workspace" />
<Button Classes="icon" ToolTip.Tip="Settings" AutomationProperties.Name="Settings">
    <ui:Icon Glyph="Cog" />
</Button>
<ui:Chip Content="machine" Mark="{DynamicResource Data}" IsRemovable="True" />
<ui:StatusPill Status="Ok" Text="synced" />
```

Buttons are 26px and come in six kinds. Secondary is what a button is when it is told
nothing. `primary` is the accent flood, `neutral` the grey one, `danger` the third,
`ghost` carries no fill and `icon` is a 24 by 24 hit area. An icon button needs a tooltip
and an accessible name, since nothing in the theme can supply the word.

**Two buttons lead, and which one depends on reach.** A view gets at most one `primary`,
for the action the whole screen is built around. Where a view repeats the same lead action
across many cards or rows, that one is `neutral`: it outranks the bordered secondary
inside its own card without nine of them fighting each other, and it leaves the accent
free to mean selection and focus. `neutral` is the only kind whose border holds one value
through every live state, since its fill moves and its edge does not.

`neutral` is carried by every button like control: `Button`, `CompactButton` and
`SplitButton`. Only the plain button's values are read from the design, which draws the
kind once. The split button's and the compact one's are derived, and each says so where it
departs. The split button's seam is the one real departure: every other kind takes its
pressed tone there, and `neutral` takes its border instead, since it is the only kind whose
edge does not track its fill.

They do not otherwise carry the same set. Only `secondary`, `primary` and `neutral` are on
all three. `Button` adds `ghost`, `danger` and `icon`, `CompactButton` adds `link`, `warn`
and `warnGhost`, and `SplitButton` adds `warn` and `danger`.

**`icon` and `danger` together are a third thing, not the two of them at once.** A trash
button in a row of actions rests quiet and floods only under the pointer, which is the
caption close button's rule rather than the danger kind's. `danger` alone is red at rest
because it is the only action in its row, and a row of five icons with one red square in it
is a row shouting at itself.

**`CompactButton`'s two warn kinds are for an alert's own actions**, where the surface under
the button is already the warn tint and the grey fill every other button takes reads as grey
stuck on amber. They take `WarnControl` rather than `Warn`: a flood is the mark's colour, and
a button wearing it beside the mark reads as a second alert inside the first. `warn` is the
one an alert recommends and `warnGhost` is the other, since the design fills only one of the
pair.

**The gaps are unbuilt rather than refused**, with two exceptions worth knowing before
closing one. `CompactButton` must never gain `danger`: it is the toast and alert button,
and the design says a toast never carries a destructive action, which is also the rule
in the `kitbash-toasts` skill. `SplitButton` has no `icon`, since it is a label and a caret
and there is no glyph only form of one. Everything else missing is simply a kind nobody has
needed yet, and adding one is a small change against the theme that lacks it.

**`ToggleButton` is a button that stays down, and it carries two kinds.** It is the
secondary button, and `:checked` takes the pressed fill with `InkPrimary` on it, so a
toggle that is on reads as a control held in rather than as a second kind of button. Its
live states step up from there through `Neutral`. The values are derived rather than read,
since the design draws the kind once, as the Create folder button on the new workspace
dialog. Use it where the answer is a state rather than an action, and a `CheckBox` where
the answer is a list of them.

Its kinds are `ghost` and `icon`, the Button theme's own two that carry no fill, for a
toolbar. A toolbar is a row of toggles and a bordered box around each is a grid of boxes, so
the fill has to be the only thing saying which one is on.

**Both are declared before the states rather than after, and that is forced.** They move the
resting look only, and order inside a theme is the only precedence there is, so a kind
written after `:checked` wins while a toggle is checked and takes away the one fill that says
so. The plain `Button` theme goes the other way, declaring `.icon` after the base states and
then restating `.icon:pointerover` and `.icon:pressed`, because there its kinds change the
live states too. Two shapes, and which one a theme needs depends on whether its kinds move a
state or only the rest.

`Expander` keys its own header off `ExpanderHeader` rather than this, so the two cannot
drift into each other.

`SplitButton` and `DropDownButton` are stock Avalonia types with a theme each. `Chip` and
`StatusPill` are ours.

`DropDownButton` has two kinds and they are separate axes. `compact` is for an inspector row,
where the label owns the left of the row and the value has what is left. `mono` is for a
button holding a value rather than a name, which is the same split every field draws: a size,
a format or a version is mono, and a person or a status is Archivo. Neither touches a fill,
so every state still reaches a button wearing one. A leading colour dot is content the view
puts in, since only the view knows the colour.

### The chip's three kinds

**A chip still says what is true, and none of these change that.** The kinds are about what
kind of true.

**`dot` makes the mark round.** Square says the chip names a thing, such as a machine or a
file kind. Round says it names a label somebody applied, such as a tag. That is the same
split the badge already draws with its dot, and it is why the two shapes exist at all.
`RadiusDot` is half of `SizeMark`, so the square becomes a circle and nothing else moves.

**At `SizeMark` 5 the two shapes are nearly the same shape.** The designs draw both at 7,
where a 2px radius reads as square and a 2.5px one reads as round. This is worth knowing
before leaning on the distinction to carry meaning, and it is the same decision as the
density note in `.claude/plans/hoard-controls.md`.

**`accent` is a filter that is in force**, rather than a fact about a thing. It is the one
chip a click removes something with, so it takes the colour that means selected everywhere
else, and it carries no mark: the label is the whole of it.

**`ChipAddButton` is the slot at the end of the row, and it is a `Button` and not a `Chip`.**
A chip says what is true and this does something, which is the line the chip's own doc draws.
Its box matches the chip beside it so a row stays even, and it sits on `SurfaceNest2`, a rung
under the chip's `SurfaceNest3`. Not `SurfaceNest1`, which is the fill of a panel one step in
and would leave the button invisible inside one.

A count inside a chip is content, styled by `TextBlock.chipCount`.

**`ui:Badge` gains `onMedia` for a badge laid over a picture.** Every tier is a tint picked
to read on a surface the theme knows and a thumbnail is any colour at all, so the fill becomes
`ScrimMedia` and the tier says its piece through the dot alone. Declared after the tiers, so
a badge keeps its dot and loses its tint.

### Row furniture

`Themes/Rows.axaml` holds the small pieces a view puts **inside** a row, so a sidebar, a tree
and a dropdown all say the same thing the same way.

| Class | On | For |
|---|---|---|
| `rowCount` | `TextBlock` | what a row says there is more of |
| `chipCount` | `TextBlock` | the same inside a chip, which sits on a fill |
| `dot` | `Ellipse` | a label somebody applied |
| `mark` | `Border` | names a thing |

**These are styles and not template parts, and that is forced rather than chosen.** A row's
modified mark is in the `ListBoxItem` template because a class can turn it on. A count cannot
be: it is a value, and a value can only arrive as content. So the library owns what a count
looks like and the view owns where it is. Growing `ListBoxItem` a `Count` property would mean
subclassing a type Avalonia already ships, which is the rule at the top of this file.

A count is always trailing, always mono, and a rung quieter than the label it follows, since
it is read after the name and never instead of it. **Never draw a zero**, which is rule 2 of
the status bar and holds everywhere.

**A status reads as colour plus icon plus label, never colour alone.** `StatusPill` draws
all three and none can be turned off, and each tier carries a default glyph, so a pill
that says its meaning in colour alone cannot be built. The tiers are `Ok`, `Modified`,
`Error`, `Accent` and `Neutral`.

### Two densities

**Dense is the default and most apps take it.** It is the design spec's own number, which
says "25px tree rows, 31px grid rows, 26 to 27px controls". An app that browses rather than
edits takes the other one, in one line, after `KitbashTheme`:

```xml
<StyleInclude Source="avares://Kitbash.Ui/Themes/KitbashTheme.axaml" />
<StyleInclude Source="avares://Kitbash.Ui/Themes/KitbashComfortable.axaml" />
```

`Themes/KitbashComfortable.axaml` holds nothing but the keys that differ. A Styles
collection is searched from its last child back, so it is reached before `Tokens.axaml`, and
every control theme already reads these through `DynamicResource`, which is a live lookup.
That is the whole mechanism, and it is the one docking uses.

**Density is geometry. No colour, no font family and no font size is in that file, and none
ever should be.** Measured against the asset tool designs, which are drawn at the roomier
density: they use 11, 11.5, 12, 12.5 and 13, which is the dense type scale exactly. A tool
that is roomier is not a tool whose words are bigger.

**The set is closed and this is it.** Twenty three keys.

| | Dense | Comfortable |
|---|---|---|
| `HeightControl` | 26 | 31 |
| `HeightControlTall` | 27 | 33 |
| `HeightControlSmall` | 24 | 28 |
| `SizeIconButton` | 24 | 30 |
| `HeightTreeRow` | 25 | 31 |
| `HeightGridRow` | 31 | 40 |
| `HeightGridHeader` | 28 | 36 |
| `HeightGridToolbar` | 33 | 40 |
| `HeightGridFooter` | 29 | 34 |
| `HeightGridGroup` | 26 | 31 |
| `HeightChip` | 21 | 27 |
| `HeightPill` | 17 | 20 |
| `HeightTab` | 28 | 33 |
| `HeightTooltip` | 22 | 26 |
| `RadiusControl` | 5 | 8 |
| `SizeRadiusControl` | 5 | 8 |
| `RadiusControlLeft` | 5,0,0,5 | 8,0,0,8 |
| `RadiusControlRight` | 0,5,5,0 | 0,8,8,0 |
| `SizeMark` | 5 | 7 |
| `RadiusDot` | 2.5 | 3.5 |
| `PaddingListRow` | 9,0 | 11,0 |
| `PaddingGridCell` | 10,0 | 12,0 |
| `PaddingTab` | 11,0 | 13,0 |

**Adding a size token means deciding whether it is in the set.** A key the comfortable file
does not name keeps its dense value, so forgetting one shows up as a single control that
does not scale with everything around it. There is no check for this.

**The title bar is not in the set**, and neither is the activity rail or the tree indent.
The spec draws a 32px title bar and so do the asset designs, so a window frame is one size
whatever is inside it.

**`RadiusControl` is the one thing in here that is not a size.** At the roomier density a
control takes the surface radius, so a button and the card under it share a corner. Dense
keeps 5 on a control and reserves 8 for the card alone.

**`SizeMark` moving is why the round and square marks are worth having.** At 5 the two are
nearly the same shape, which quietly costs the split its meaning. At 7, which is what both
asset pages draw, it reads.

**A pinned pixel width does not scale, and that is the trap.** A control grows, the column
or panel a view pinned around it does not, and the content clips. Found in the gallery: a
118px status column fitted its pill at dense and cut it off at comfortable. Size a fixed
width for the roomier density, or do not fix it.

**Check both.** The gallery's title bar carries a Comfortable toggle that adds and removes
the include at runtime, which is exactly what an app does at startup.

### Two rules that decide how a control theme is written

**Order inside a control theme is the only precedence there is.** A selector that varies
at runtime, which means any class or pseudo class, binds at `StyleTrigger` and beats a
plain type selector whatever the order. So the resting look goes in the setters and every
kind and state goes in a nested style, never a mix of the two. Then all of them are
activated, the later one wins, and order alone decides. Write base, base states, each
kind with its states, then disabled last.

**A control theme is found by the exact type and never falls back to a base type.** A
type derived from one that has a theme still needs its own, which is why
`DropDownButton` has one despite being a `Button`.

A corollary worth the words: a class on a shared control theme inherits everything that
theme ever gains. The caption buttons were a class on `Button` until the `Button` theme
gained a height, which shrank them inside the title bar. Where a control only resembles
the shared one, give it a keyed theme instead. `CaptionButton`, `SplitButtonPart` and
`ChipRemoveButton` are all keyed for that reason.

### The status bar

The row along the foot of a window. `Themes/Controls/StatusBar.axaml` holds all of it, so a
tool's status bar reads the same as the launcher's. Eight rules, from the design notes under
the launcher page, and they are the reason the row looks the way it does.

1. **A readout is not a control.** The branch has no border, no chevron and no hover.
   Switching branches belongs to a git tool. `TextBlock.branch`.
2. **A zero is never drawn.** "behind 0" is a line of text that says nothing happened.
   Every count is hidden below one, so anything present is worth reading and a quiet bar
   means a quiet repository.
3. **Colour plus icon plus label, never colour alone.** Each count carries its own glyph.
   The coloured dots this replaced said their meaning in hue and nothing else.
   `StackPanel.countReadout` with `ui:Icon.countMark`, `TextBlock.count`, `TextBlock.countLabel`.
4. **What blocks work escalates out of the row.** Everything else in the bar is worth
   knowing, a conflict stops you, so it leaves the row of counts and takes a tinted chip
   with an alert glyph. `Border.conflictChip`, which restyles the count classes inside it.
5. **Two groups, because they answer two questions.** Left is identity, which branch and
   what has changed in it. Right is the branch against its remote, how far ahead or behind
   and when it last looked.
6. **One action, and one word for it.** The button says Update and the label says Updated,
   whichever of the two things happened. **This is a deliberate change from the design,
   which says fetch only and never writes.** It always fetches, and it takes the new
   commits when there is nothing local that taking them could cost. Pushing and resolving
   a conflict still belong to a git client. See the `kitbash-git` skill for the guards.
7. **Counts are pluralised properly.** "1 conflict", "2 conflicts", never "1 conflict(s)".
   Humanizer does that, through `"conflict".ToQuantity(n, ShowQuantityAs.None)`, which
   inflects the noun and leaves the number out so the view can style the two apart. Only
   nouns inflect: a count of something described rather than named, such as twelve modified
   files, keeps its word.
8. **A slow action reports on itself in place.** The glyph turns for as long as the update
   runs and the label beside it says Updating. No toast, no dialog. `ui:Icon` with the
   `spin` class from `Themes/Motion.axaml`.

`StatusBarButton` is a keyed theme rather than a class on `Button`, for the reason the
caption buttons are keyed: the row is 32px including its seam and a class would inherit
every height and padding the shared theme ever gains.

Icons here are `IconSizeStatus`, which is 13. The design draws the branch glyph at 14 and
the rest at 13, and one size across the row was preferred to a 1px difference nobody can
see. The spinner is the toasts page's 1.1 seconds rather than this page's 0.9, since one
spin speed in the app beats two.

### Inputs

Everything that takes a value sits in a **well**, `SurfaceWell`, which is darker than
anything around it and stays that tone at any depth. A well is off the ramp, like every
other control.

**A well answers the pointer on its own ramp**, `SurfaceWellHover` then
`SurfaceWellPressed`, with its edge coming up to `LineWellHover`. `StateHover` is a control
fill and would lighten a well past the surface it sits on.

**Focus lightens the well.** The design's five columns show a pressed state for a text
field, which is the field being typed in, and that is the same thing as focus in a real
control. So the two are one state here: the well goes to `SurfaceWellFocus`, the border to
`LineControl`, and the halo appears.

**A value is mono and a label is Archivo.** A plain `TextBox` holds language, so it takes
the UI family. A number, a date or a path says what it is by taking `FontFamilyMono`, which
is what the numeric and picker themes do. That split is the most visible thing about this
theme and the easiest to get wrong in a form.

**A placeholder is language whatever the value is.** `PART_Placeholder` is pinned to
`FontFamilyUi` in the template rather than following the control's own family, so a mono
field still says "No file selected" in Archivo.

**`ui:SearchBox.query` is mono, and that is the same rule rather than an exception to it.** A
field holding `tag:character -status:raw` holds a syntax, and a thing with a syntax is not
prose. A field holding words a person is looking for stays Archivo. Parsing the prefixes is
the view model's, not the control's.

**`TextBox.notes` is the field that takes a paragraph.** It drops the fixed height for
`HeightNotes` as a floor and grows with what is typed. The well, the border and the focus
ramp stay the field's own, because a notes box is a field.

**`TextWrapping` on its own does nothing here, which is a trap worth knowing.** The template's
`PART_ScrollViewer` is written `HorizontalScrollBarVisibility="Hidden"`, and Hidden still
scrolls, so it measures the presenter at infinite width and no line ever runs out of room.
`Disabled` is what constrains it. The class reaches in with a `/template/` style to set both
that and the presenter's `VerticalAlignment`, which is a constant `Center` in the template and
would otherwise float a paragraph in the middle of a box that has grown. **A class carries an
activator and a constant does not**, which is the only reason a style can reach past either.

**The adorner rule is the whole look of a value editor.** A stepper, a calendar mark or a
colour swatch sits *inside* the well behind a hairline, never floating outside it. Nothing
is built for it: `TextBox` and `NumericUpDown` both carry `InnerLeftContent` and
`InnerRightContent`, and a spinbox is a `NumericUpDown` over a `ButtonSpinner` over a text
field with only the frame themed.

**Two of the sliders are settings rather than code.** Stepped is `TickFrequency`,
`IsSnapToTickEnabled` and `TickPlacement`, with a `TickBar` behind the track.

**A halo's radius is grown from the control's**, through `ui:HaloRadius.Grown`, rather than
named per shape. The halo sits outside the border and a corner radius describes an outer
edge, so the halo is the control's radius plus its own thickness. A square corner stays
square, which is what keeps one end of a split control right.

**There is no token for it, and there must not be one again.** A stored value has to be kept
in step with two things it is derived from, and it never was. It was wrong for the search
field, which takes 8 where every other field takes 5, wrong for the checkbox, whose box is 3,
wrong for the chip button, which takes the surface radius, and then wrong for every control
in the app at the second density, where a 7px ring sat inside an 8px button. All twenty three
sites read the control now, so none of those can come back.

```xml
CornerRadius="{TemplateBinding CornerRadius, Converter={x:Static ui:HaloRadius.Grown}}"
```

**A control whose round part is not itself binds to that part instead.** A radio has no
radius of its own, so its halo grows from `#PART_Box`, which is the circle. That is the only
one, and an element name binding is what reaches it.

**Do not write the halo's radius from a style.** The split button's two ends used to, so that
each could override it, and they no longer need to: each end sets its own `CornerRadius` and
the template binding follows. That also settles the precedence problem the old comment there
described, since nothing overrides anything now.

**Checking it is a measurement, not a look.** Read the halo `Border`'s `CornerRadius` back off
a rendered window and compare it against its control's plus 2, at both densities. A ring that
is one or two pixels tight reads as slightly wrong rather than as broken, which is exactly the
kind of thing an eye signs off on.

**Every field carries a right click menu**, and it is `ui:TextMenu`, an attached property
one style turns on for `:is(TextBox)`. Undo, Redo, then Cut, Copy, Paste, Delete, then
Select all, in the order every desktop prints them. A field that wants none sets
`(ui:TextMenu.Shows)="False"`.

It is code rather than a setter for the reason `ui:SearchBox` is a control: a menu item has
to do something and a control theme has no code behind. A setter value is also one shared
instance across every field the theme reaches, and a menu can target only one control at a
time.

It is a `ContextMenu` and not a `MenuFlyout`, which is what opens it at the cursor at its
own width. See the `kitbash-surfaces` skill under Overlays for why the two are not
interchangeable.

**Its state is filled in on `Opening`, and that fires from the `ContextRequested` path
only.** The public `Open` skips it, so a menu opened in code reads every row on. Right
click and the context key both go the right way.

Nothing in it is written here that the framework already knows. What is on comes from
`CanCut`, `CanCopy`, `CanPaste`, `CanUndo` and `CanRedo`, read when the menu opens, so a
password field cannot copy and a read only one cannot paste without either being named. The
shortcuts come from `PlatformSettings.HotkeyConfiguration`, which is the same object
`TextBox` matches a key against, so a hint and the key that works cannot disagree on either
OS. Delete is the one exception, since the platform lists no gesture for it.

**`ui:SearchBox`** is the one input control that is ours, and only for its clear button. A
mark and a radius would be a class, but emptying a field is behaviour and a theme cannot
carry it. Its button lives in inner content, which no control theme can reach, so the theme
styles it from a plain style and the control hears the click bubble. See
`.claude/avalonia.md` under control themes for why.

**`ui:PathField`** is a well holding one path with a browse button beside it. **It holds
exactly zero or one path**, so there is no list, no add button and no chip anywhere in it,
multiple selection is never turned on, and browsing again replaces what is there. The design
is `Theme Slate - Path Field`.

```xml
<ui:PathField Target="Folder" Path="{Binding Folder}" />

<ui:PathField Path="{Binding Source}" AllowsTyping="False">
    <ui:PathField.Filters>
        <ui:PathFilter Name="Data files" Extensions=".json, .csv" />
    </ui:PathField.Filters>
</ui:PathField>
```

`Target` is `File` or `Folder` and it decides which dialog opens, what the empty field says
and what a drop will take. Everything else about the two is the same, including the glyph.

**Filters belong to the control, never to the person using it.** They gate the dialog and
they gate a typed path, and the quiet line under the field says them in words without being
written twice. A folder field ignores them, and so does a set where any one filter names no
extensions, since that one takes anything. Extensions are matched ignoring case on both
platforms, because a filter describes the shape of a name.

**`FolderName` gives the last segment to the host.** Set it and browsing picks the folder
that named one goes inside: the dialog opens on the folder above, and a pick becomes that
folder joined with the name. Blank leaves browsing alone, and it is folder targets only.
This is `create_dir` from Godot's project dialog, and the new workspace dialog binds it so
a person browses to the folder they keep projects in. A drop goes the same way, since a
browse and a drop funnel through one method.

**Typing is never fixed up, only a pick is.** A path rewritten while somebody is halfway
through typing it fights the keystrokes, so the host owns the segment for a pick and the
typed text stands as written. Godot draws the same line.

**A typed path is judged when the field is left, and typing over a refusal takes it back.**
So a half typed path never flashes red and a path being fixed is not red while it is half
fixed.

**Typing is on unless it is turned off.** `AllowsTyping="False"` is for a path a hand
written answer would be meaningless for. The well drops to the chrome tone and takes no
caret, and browse and clear both still work. **That is not the disabled look**: the text
keeps its tone. Disabled flattens both halves and drops the clear button rather than greying
it.

**The clear button stays while the field has focus**, where the design takes it away. A
press inside the well focuses the field, so a button that went on focus went under its own
press and could never be clicked. Nothing is lost by keeping it: the design overlaps it with
the text and ours is docked beside it, so no caret ever runs under it.

**One message slot and two tiers**, which is the settings rule in a control. `Problem` with
`ProblemTier="Error"` means the value will not do, and `Warn` means the value is fine and
the world is wrong, such as a path that has gone. The control writes its own `Problem` for a
filter failure and leaves a host's alone.

**Nothing in it touches a disk.** Whether a path is really there is the host's answer, and
in settings it is what a probe will report. The control only ever judges the shape of a
name.

`IsCompact` is the icon only form, for an inspector row or any column too narrow for the
word Browse. **A long path shows its head rather than its tail**: the design ellipsises from
the left so the file name stays readable, and a `TextBox` scrolls rather than trims, so the
whole path is the tooltip instead. The design's other display rule, a path shown relative to
the workspace, is not built either, since the control is handed a path and not a workspace.

**A drop takes the first item and nothing else.** One acceptable item dragged over the well
turns it into a drop target. Several are judged by the first, which is the one a drop takes.
The design says a drop of several says so in a toast, and it does not: a library control has
no toast service to reach.

Its theme is in `Themes/Controls/TextBox.axaml` with the other fields, since it is a well
and it shares the clear button with the search field. The clear button lives in inner
content, so it is shown from a plain style outside the theme for the reason the search
field's is.

**The radio and the checkbox put their mark where `VerticalContentAlignment` says.** The
default is centre, which is every one line row. A row whose label runs to a title and a
description below it sets `Top`, so the mark sits against the first line instead of
floating halfway down the block. The new workspace dialog's renderer rows are the case.

**A row that wants a fill behind a radio wraps it.** Both templates hold a transparent
background of their own, so a `Background` on the control never reaches the row. Put the
tint on a `Border` around it, which is also where the row's padding and radius go.

**`ui:Segmented`** is a row of radios with a thumb behind them, and the thumb is the reason
it is a control. A border holds one child, and a fill on the chosen option would appear and
disappear where a thing that slides has to be one thing that moves. The options stay radios,
so grouping, clicking and the keyboard are all the framework's.

**No option carries a fill, in any state.** An option changes ink and nothing else, over the
design's 140ms, and the raised surface is the thumb. That covers hover and pressed as well
as the chosen one, which makes this the one control in the library that answers the pointer
without a fill. A hover fill here is a second raised surface beside the thumb, saying the
same thing in the same way, and the one that moves stops being the thing being watched.
Both the thumb's position and its width move, since Table, Grid and Cards are three
different widths.

**The thumb reads the option it is standing on** rather than working a place out from
tokens, so the padding, the 2px gap and the row height are written in the theme and nowhere
else. The motion is the theme's too, through `ThumbTransitions`, which is the shape
`ToggleSwitch.KnobTransitions` already has. It is 200ms on the knob's own curve. The two
share a curve and not a duration, because the knob crosses 13px and a thumb crosses a row.

**Only a change of answer slides.** A first placement, a layout change and coming back into
a tree are all written with the transitions off, so a row opens with the thumb already on
the chosen option instead of sliding in from the left edge, and a thumb never trails the row
while a window is being dragged. Measured over three options at 47.33, 41.33 and 50 wide: at
load the thumb is at 0 and 47.33 wide, the frame an option is picked it has not moved,
90ms later it is at 87.01 and 49.84, and it settles on 92.67 and 50. An option grown to 110
takes 110 in the frame it grew, and a row taken out of the tree and put back is exact in the
frame it returns.

### Colour

**`ui:ColorPicker` is one body and two hosts.** Floating it is a card with a shadow and a
Cancel and Apply footer. `IsInPanel="True"` drops the frame, the shadow and the buttons, the
value applies live, and the footer reads the literal back. Everything else is the same
template. The design is the Value editors section of `Theme Slate`.

**It is a property rather than a class**, unlike `ui:Popover.inPanel`, because the frame it
has to reach is a `ui:Popover` inside its own template and a class on the picker cannot be
handed down to it. The picker sets the shell's class itself.

**`ui:ColorField` is the value editor.** A 26px well holding a 14px swatch and the hex in
mono, and clicking it opens the picker in a popover. It is a `Button`, so the click, the
focus and the flyout are all Avalonia's. The edit commits on Apply, so dismissing the
popover leaves the field as it was. Six digits while the colour is opaque and eight when
there is alpha to spell out.

```xml
<ui:ColorField Color="{Binding Tint}" Header="FILL COLOUR" Swatches="{Binding Saved}" />
<ui:ColorPicker IsInPanel="True" Color="{Binding Tint}" />
```

**It is built against Godot's own picker**, `scene/gui/color_picker.cpp` and `color_mode.cpp`
in the 4.7.1 source, and the numbers are meant to agree with it. Read those before changing
any of the value handling here.

**The value is `ColorValue`, four floats, and red, green and blue may go above 1.** It is not
`Avalonia.Media.Color`, which is four bytes and cannot hold an HDR value at all. Alpha is 0 to 1 and never leaves it. The type carries hex, HSV and
Ottosson's OKHSL, all measured as exact round trips here apart from hex, which quantises to
8 bits by definition.

**The picker holds a base colour and stops, not one colour.** `Color` is what a host reads and
writes, and inside it is split into a base whose channels are all inside 0 to 1 and an
`Exposure` in stops. Every control draws the base, so the field, the wheel, the ramps, the hex
and all four modes always have a colour a screen can show, and the exposure row carries the
rest. This is Godot's `color_normalized` and `intensity` under the design's own names.

**The multiply is in light rather than in the numbers.** One stop is twice as bright, so the
exposure is applied by taking the sRGB transfer off, multiplying by two to the power of the
stops, and putting it back. Doubling 0.878 gives 1.19 and not 1.756, which is what Godot
gives. Splitting a colour the other way divides by the largest linear channel and never by
less than 1, so a colour a screen can show always reads as no stops at all.

**A channel typed past the end of its ramp moves the excess into the exposure.** Typing 400
into red leaves the colour at 1.569, the row reading 255 and the exposure at +1.49. Godot
calls this allowing greater, and only RGB and Linear do it, since hue and the two saturations
have nothing past their end.

**Avalonia's own `ColorPicker` package was read and refused**, which is a departure from the
theme what Avalonia ships rule and the reason is the value: `ColorSpectrum` and `ColorSlider`
both carry `Color`, so a channel above 1 cannot exist in them, and `ColorView` is a tabbed
spectrum and palette card with none of RAW, OKHSL or EV. Both shapes the design draws are
gradients, and Avalonia has `ConicGradientBrush`, so the wheel needs no bitmap either.

**Seven shapes, which are Godot's own set.** A saturation and value square with hue on the
bar beside it, the same square inside a hue ring, a hue circle with value beside it, the
perceptual circle with lightness beside it, two perceptual rectangles, and the rows on their
own. **The bar always carries what the shape does not**, and the ring is the one shape that
carries all three itself, so it has no bar at all.

**Hue runs clockwise from the right**, on the ring and on both circles, because Godot reads
the angle with the screen's own downward y. Measured off a render rather than reasoned:
east red, south chartreuse, west cyan, north violet.

**The three perceptual surfaces are painted a pixel at a time.** OKHSL is not a straight line
in sRGB, so no gradient can hold one and Godot uses a shader. `ui:ColorSurface` paints into a
bitmap at half the size and lets it scale up, which cannot be told apart and costs a quarter
of the conversions. Measured: 9042 pixels in 3.2 ms, so a drag of the third component repaints
inside a frame. It belongs to the picker and nothing else should place one.

**The shape is chosen from a menu rather than a segmented row.** The design draws three icons
in a row and seven do not fit one, so the header carries a dropdown of the seven names with
the current shape's mark on the button, which is what Godot does.

**Four value modes, and every one of them carries the EV row.** RGB and HSV read whole
numbers, Linear reads the colour as light, and OKHSL is the perceptual space, so a hue drag
there keeps its lightness where the same drag in HSV does not. Hue stops at 359, since 360 is
the same colour. Alpha is 0 to 255 everywhere except Linear, where it is 0 to 1. All of that
is Godot's.

**Linear is not the design's RAW.** The design page says RAW floats, which is what Godot's
third mode was called before 4.4 and what its numbers were. Godot renamed it Linear and made
it read `srgb_to_linear`, so 0.878 in the numbers a colour is written with is 0.744 there.
The mode follows Godot. The sRGB floats are still on the picker, in the literal at the foot,
which is the line a person pastes into a script.

**The exposure row is Godot's intensity row, and it is labelled I as Godot labels it.** The
design calls it EV. It reaches ten stops either way on the ramp and can be typed past that,
and its arrows step whole stops.

**Pressing a lane anywhere moves the handle there and keeps following the pointer**, which is
Godot's pattern and Avalonia's own. A slider moves to a point through its decrease and
increase buttons, so a lane template without them can only be dragged by the handle. They are
in the theme, transparent, drawing nothing.

**The text field is a row of its own under the channels**, where the design puts it beside the
value modes. An expression is far wider than a hex and it grew the header out of shape. The
literal well below it hides itself while the field is already showing the same line.

**The text field carries the hex while there is one and the expression otherwise.** A colour
with a channel above 1 or below 0 has no hex, so the field reads `Color(1.569, 0.663, 0.29)`
instead, and it takes that form back. This is Godot's own rule and its own formatting, three
decimals with alpha left out while it is 1.

**It also takes everything else Godot's field takes.** Hex of one, two, three, four, five,
six, seven or eight digits with or without the hash, all fixed up the way Godot fixes them,
and any of its 146 colour names, spaces and case ignored. The names are read out of
`core/math/color_names.inc` rather than typed here, so they cannot drift from the engine's.

**The old half of the chip puts the old colour back.** It carries an undo mark while the two
differ and nothing while they agree, and the new half carries a bolt while the colour is
brighter than a screen can show. Both are Godot's, which draws a revert icon and an overbright
indicator in the same two places.

**A channel row carries a number and a position separately.** The spinbox is the number, and
its maximum reaches 1024 in RAW. The ramp is the position, 0 to 1, and it is a separate
property so a slider cannot clamp a value that is past the end of its own ramp. **A ramp
sweeps the channel it is on with the others held**, so the red ramp runs from no red to all
red at today's green and blue, where the design draws black to the colour.

**The spinbox is the value editors' own, unchanged.** If it needs changing, change it there
so a channel row and a property panel agree.

**Every gradient is built in code**, because a hue is computed from the value being edited
rather than chosen from a palette. The two that do come from the palette, the exposure ramp
and the checkerboard, are looked up by key, so their hex still lives in `Tokens.axaml` and
the rule that nothing else writes a colour still holds.

**The add tile stands where the first swatch would.** An empty list is hidden rather than
empty, since a row that spaces its children would otherwise hold a gap for a list with nothing
in it.

**Swatches and recents start empty and the picker never seeds them.** They are `IList` in, and
the picker keeps them the way Godot keeps its presets: saving a colour that is already saved
moves it to the end rather than landing twice, a right click takes one off, and the recent row
holds nine, newest first. A colour joins the recents when an interaction ends rather than while
it runs, which is the pointer coming up, so dragging across the field leaves one entry and not
two hundred. `SwatchAdded` and `SwatchRemoved` carry the colour, so an application can persist
them. Filling them with the theme's own palette is the mock data trap this control is most
likely to fall into.

**A host can drop the alpha row and the exposure row.** `EditsAlpha="False"` takes the alpha
lane with it, and `EditsExposure="False"` puts the stops back into the colour before it goes,
so the value never changes underneath. Both are Godot's `edit_alpha` and `edit_intensity`.

**The eyedropper is the desktop's own gesture.** `ScreenColour` is an `IScreenColour` a host
hands in, and the button is drawn only when one is supplied and it says it can pick. Nothing
is drawn over the screen here: on Linux the portal draws its own magnifier and gives one
colour back, which is the only way that works on Wayland. On Windows there is no portal, so
`NoScreenColour` is registered and the button never appears. See the `kitbash-platform` skill.

**A pick that fails says so in `ui:ErrorDialog`**, with the portal's own words in a mono well
and a button that copies them. A person changing their mind is not a failure and says nothing.
That dialog is the library's, so any control here can report a failure the same way.

**Two things Godot has and this does not.** Saving and loading a palette as a file, and
dragging a colour from one swatch to another. Neither is refused, they are simply not built,
and the stage file records what each would take.

**The picker owns its own hue.** A grey has no hue and black has no saturation, so both are
held on the control rather than read back from the colour every time. Without that the field
handle jumps to the left edge as a drag passes through black.

**The pickers read the culture and nothing here writes a date or a time format.** Measured
across four: en-US reads 7/28/2026 and starts its week on Sunday, de-DE reads 28.07.2026 and
starts on Monday, ja-JP reads 2026/07/28. The design's Mo Tu We is one locale rather than a
specification.

The clock goes with it. Avalonia defaults `ClockIdentifier` to twelve hours whatever the
culture says, so `ui:Clock` asks the culture instead and the field reads through the
culture's own short time pattern rather than a format written here. Measured: en-US gives a
twelve hour clock, hours 1 to 12, a period column and a field reading 2:05 PM, while de-DE,
ja-JP and fr-FR give twenty four hours, hours 0 to 23, no period column and 14:05. Reading
the hour and the minute out of the control's own parts and joining them with a colon, which
is what this did first, drops the period and fixes an order that is not ours to fix.

**A picker's popover has to be told what the field holds.** The calendar opens on today
whatever the field says, and a two way binding from the calendar back to the field wipes the
date on the way in, because the calendar starts empty and pushes that back. So nothing is
bound to `PART_Calendar` and `ui:Picker` syncs it when the popover opens: the selected date,
or today when there is none. Emptying the field is the Clear button in the popover rather
than something that happens on the way in.

**A picker template has to carry every part the control names, even the ones it will never
draw.** The build refuses a template that leaves one out, and `TimePicker` throws a null
reference from `SetGrid` for a part that is present but not wrapped in the `Border` host it
expects. Its readout is the control's own hidden grid, read back through two element name
bindings, because that grid is a segmented row of numbers between rules where the design is
one string.

**Validation runs on `INotifyDataErrorInfo`.** A field draws an `Error` border when its
binding reports one. Measured on 12.1.1: Avalonia turns the data annotations plugin off, so
a `[Required]` attribute alone does nothing, but a source implementing
`INotifyDataErrorInfo` still reports and `DataValidationErrors.HasErrors` lands on the
control. CommunityToolkit's `ObservableValidator` implements it, so a view model gets this
by deriving from it. The `error` class is the same look for a view with no validating model
behind it.

**Fluent draws a focus ring of its own and it is switched off.** `Themes/Focus.axaml`
clears `FocusAdorner` on every control. Measured: a keyboard focus otherwise puts a black
2px border with a translucent white one inside it into the adorner layer, which is above
everything a template draws, so it lands on top of our halo. Fluent is loaded on purpose,
since it is what gives an unthemed type a template at all, and this takes back the one
thing it does that the design forbids.

### The gallery

```
dotnet run --project src/Kitbash.Gallery
```

Every control in the library, live, in one window. It is where a control theme is built
and where it is checked by hand, and it is a consumer of `Kitbash.Ui` and nothing else,
so it never references the launcher. Anything it needs from there belongs in the library.

It holds no palette and no control look of its own. Only page furniture, headings and
panels. So anything that looks wrong in the gallery is wrong in the library.

**Add every new control to it in the stage that builds the control.** A kind that is not
in the gallery is a kind nobody has looked at.

Each control shows a live sample and a row of held states, so all five can be read side
by side. A held sample carries `forceHover`, `forcePressed` or `forceFocus`, and
`GalleryWindow` turns those into pseudo classes on load. Those samples opt out of hit
testing, since a real pointer would otherwise clear the state that was pinned on them.

The gallery is also the window shell, so dragging, double clicking, resizing and the
inactive tier are all testable in it, and its title bar carries content, which is the
case that keeps the row alive when the desktop draws the frame.

### Focus and disabled

Focus is a 2px halo of the accent at 30 percent, outside the border and never in place
of one. It is a sibling with a negative margin inside a `Panel`, so focus costs no layout
and a control never changes height when it takes focus. It answers `:focus-visible`, so
tabbing rings a control and clicking one does not.

**A control that draws a halo has to turn its own clip off.** `ContentControl` clips to
its bounds by default in Avalonia 12, which cuts the halo off flush with the border and
leaves a hard ring. So the theme sets `ClipToBounds="False"` on the control and
`ClipToBounds="True"` on the frame inside the template, which keeps content in while
letting the halo out.

**Ring the thing that actually takes focus.** A `SplitButton` is not focusable and its
two halves are, so each half carries its own halo and rounds only its outer end. That is
why the frame does not clip: a clip would take both halos with it.

**A press on anything that is not a control takes the focus off the one that held it.**
`ChromelessWindow` is `Focusable`, which is the whole of it. Avalonia walks up from
whatever was pressed and focuses the first thing that can take focus, and it gives up when
there is none, so the window is the last stop. A press on a button still stops at the
button. A press on a panel, a heading, a scrollbar or the title bar reaches the window and
the field behind loses its caret. See `.claude/avalonia.md` under Input for what was
measured.

A window that does not derive from `ChromelessWindow` does not get this.

Disabled flattens the fill to `SurfaceControlOff` with a `LineControlOff` border and
`InkDisabled` text. Never opacity. A kind with no fill keeps none.

Each tool is its own executable, started by Kitbash as a separate OS process. Tools are not
loaded in process and Kitbash does not construct their windows, so every control here has to
work for a tool that shares nothing with the launcher but this library.

How a tool is found, installed and started is `.claude/plans/tool-distribution.md`.

