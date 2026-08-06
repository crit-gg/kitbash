# The welcome window

The window an app opens with. One list of what has been opened before, over whatever the
app decides a project is. Built from `Projects Window.dc.html` in the design project.

Outside the thirteen stages. It is a whole window in `Kitbash.Ui`, the way the settings
window is, so a tool writes an `IProjectKind` and one line to open it.

## The split, and it is the whole design

**The library owns the store. The app owns what a project is.**

`IRecentProjects` in `Kitbash.Core/Projects` keeps a list of paths, their names, the order
they were last opened in, and whether each is still on disk. That is all it knows. It never
looks inside a folder, never decides whether one qualifies and never opens anything.

`IProjectKind` in `Kitbash.Ui/Projects` is the app's half:

| Member | What the app decides |
|---|---|
| `Words` | what it calls the things it opens, and its own name, icon and version |
| `ClosesOnOpen` | whether opening one closes this window or leaves it as the shell |
| `Pages` | anything else it puts in the rail, under the list |
| `Describe` | the mark, the tile tone, the chip and whether a row is broken |
| `CreateAsync` | the filled button. Make one from nothing |
| `BrowseAsync` | the bordered button, and the way a lost row is found again |
| `OpenAsync` | opening one, and reporting a failure, which only the app can describe |

`Describe` runs on the thread pool once per row per refresh, so it may read a disk. An app
that throws there costs that row its chip and never the window its list, since the list is
the only way back to anything.

Foundry lists Godot projects, Hoard lists asset libraries and Splice lists repositories, and
none of that reaches the window. The gallery carries all three as `HarnessProjectKind`.

## Storage

Three arrays in the app's own state file, under `projects.recent`:

```toml
[projects]
recent.paths  = ["/home/you/dev/slopworks", ...]
recent.names  = ["Slopworks", ...]
recent.opened = ["2026-08-06T14:30:00.0000000+00:00", ...]
```

**The order in the file is the recency order**, newest first, so nothing has to be sorted to
draw the list the way it is usually wanted.

Three arrays rather than an array of tables, because `ISettingsValueConverter` converts
primitives and arrays of primitives and nothing else. Writing a table array would mean
changing the settings machinery and the write that preserves a file byte for byte, which is
a bigger change than this needed. They are written in one `Apply`, so they can only be out
of step if somebody edited the file, and a short one truncates the list rather than pairing
a path with another entry's name.

The scope is `RecentProjectsOptions`, so a tool passes its own and two tools never share a
list. `Limit` defaults to 50 and the oldest fall off the end.

## What departs from the design

**The window is resizable and keeps its maximise button.** The design draws a fixed 648 by
496 window with minimise and close alone. A list of an unknown number of rows is worth
resizing, and every other Kitbash window resizes.

**Reveal in explorer is Open folder.** A Linux file manager is not called explorer, and the
launcher already says Open folder for the same action. It shows the nearest folder above the
path that exists, so a row pointing at a file, or at a folder that has gone, still lands
somewhere.

**Settings is a window, not a page.** The design's rail switches to a settings pane. Every
Kitbash app already has one settings window through `ISettingsWindows`, and two places for
settings inside one app is worse than one. The cog is drawn only when the app registered
one. A rail item is a way in rather than a place, so it never stays selected.

**The sort menu marks the chosen row with a check rather than a tint.** The menu item theme
carries no toggle, and the current answer is written on the button anyway, which is what the
design's own note says the dropdown is for.

**A row that has never been opened is a page of its own.** The design draws a window that
always has a list. The empty page carries the same two buttons the action row does, so there
is still a way in.

**The title bar is the house one.** The design draws a coloured square with the app's
initial. `ui:WindowTitleBar` owns the icon, the title, the version and the caption buttons,
and a window conforms to it rather than inventing a frame. An app hands its icon in through
`ProjectWords.Icon` as an avares uri, which becomes both the title bar mark and the task bar
icon.

**The menu button fades rather than hides.** Avalonia has no hidden that keeps its box, and a
row whose columns shifted as the pointer arrived would move the thing being aimed at. Opacity
and hit testing move together, so a button nobody can see cannot be pressed.

## Extra rail pages

An app returns `ProjectPage` values from `IProjectKind.Pages` and they follow the list down
the rail, in the order given. A page is a glyph, a word and a `Content`, which is a control
or a view model the app has a template for.

**The window knows nothing else about one.** It draws the rail item, gives the pane over and
takes it back. There is no header, no toolbar and no shell around it.

**A page is built once and kept**, since the app hands over the object rather than a
factory, so leaving a page and coming back finds it as it was left.

The rail is filled in code rather than declared, because each item carries a word only the
app knows and an `ItemContainerTheme` cannot be handed one per item. The item theme is
looked up from the window, so a miss draws Fluent's own row rather than the rail's, which is
what the 32 by 32 assertion in the tests is for.

The cog stays at the foot of the rail and is not a page. It opens a window and never stays
selected.

## What is not built

**The host picker above the window** is the design page's own harness rather than part of the
window. The gallery is that harness here.

**A row is never dragged and nothing is dropped on the window.** Dropping a folder on the
list would be a natural way to add one and the design does not draw it.

## What it is made of

The window writes no control look. The row is the library's list row with two properties
moved, since a `Style` beats a `ControlTheme` and the seven states are the ones already
built.

**`ui:Badge` gained a `tile` kind.** The square mark at the head of a row is the badge tiers
at a size the eye can run down a list on, so the tone, the fill, the edge and the ink are all
the ones a chip already uses. It takes the UI family where a badge takes mono, since a letter
standing for a name is not a value. A tone the app does not name is picked from the path with
a hash that does not move between runs, because `string.GetHashCode` is seeded per process
and a row would change colour every launch.

`HeightProjectRow` and `SizeProjectTile` are **deliberately not in the comfortable set**. The
window browses at either density and its row already carries two lines.

The chip is `ui:Badge` with a glyph in its content, which is how a row reports a problem
without the badge growing an icon slot.

## Verified here

- 17 headless tests in `tests/Kitbash.Ui.Tests/ProjectsWindowTests.cs`, drawn for real with
  Skia and no display, plus 7 for the recency wording.
- 11 tests in `tests/Kitbash.Core.Tests/Projects/RecentProjectsTests.cs`, each over a real
  state file in a temporary folder, through a provider built the way an executable builds
  one.
- The three states rendered and read by eye: the list, nothing matching a search, and a list
  that has never held anything.
- **The Windows half has not been executed**, since this machine is Linux. What differs
  there is `IPlatformServices.OpenInFileBrowser` and the path comparison, both of which were
  already built and covered.
