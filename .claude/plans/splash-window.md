# The splash window

`ui:SplashWindow` is the window an app shows while it starts. It is the third whole window
`Kitbash.Ui` owns, after the settings window and the welcome window. Built from the
`Splash Screen` design in the Claude Design project, with the lattice and the corner
brackets taken from `assets/splash-lozenge-tile.svg` and the four `corner-*.svg` files in
the same project.

**The host owns the lifecycle, with one exception.** The library never opens one and never
decides when it is replaced. An app builds it, shows it, reports to it and closes it. The
exception is a person dismissing it, which is described under Ending the launch below.

## It has to open before anything else does

That is the whole reason it is built the way it is, and every rule below follows from it.

A splash that waits for the theme is not a splash. `KitbashTheme.axaml` pulls in
AvaloniaEdit's own theme, twenty six resource dictionaries and thirteen style files, and
`FluentTheme` is another. All of that is parsed before a window can be templated, and none
of it is worth waiting for to draw a card with six pieces of text on it.

So the window depends on **no styles at all**. Not the Kitbash theme, not Fluent, not an
empty one. `SplashWindowTests.ItDrawsWithNoThemeLoadedAtAll` clears
`Application.Current.Styles` and renders it for real, headless with Skia, and it draws
identically.

Three things follow, and breaking any of them breaks the window rather than degrading it:

1. **It supplies its own `Template`.** A `Window` with no theme has no control template and
   draws nothing at all. `SplashWindow.Frame` is a `FuncControlTemplate` built in code, and
   it is the same shape as `WindowFrame` in `Themes/Controls/WindowChrome.axaml`: the
   gutter, the drop shadow, the card, the clip, the layer manager and the presenter.
2. **Nothing in it is a templated control.** `Border`, `Panel`, `Grid`, `StackPanel`,
   `Canvas`, `TextBlock`, `Image`, `Path` and `ContentPresenter` all draw with no theme.
   A `Button` does not, which is why the close mark is a `Border` with three pointer
   handlers rather than a button, and a `ProgressBar` does not, which is why the sweep bar
   is two borders and an animation. Adding a themed control to this window makes it
   invisible under a host that has not loaded a theme yet, and it will look fine in the
   gallery, which has.
3. **Every value is a literal.** No `StaticResource`, no `DynamicResource`, no
   `TryFindResource`. The Slate colours it needs are copied into `SplashWindow` as
   `ImmutableSolidColorBrush` fields named after the tokens they came from. **This is the
   one place in the app allowed to write a colour outside `Themes/Tokens.axaml`**, and it
   is a copy, so a token changing there means changing it here too.

Nothing is loaded from disk either. There is no SVG package here and no image decode: the
lattice, the brackets and the veil are geometry in `SplashBackdrop.Render`, and the close
glyph is the same path string as `IconX` in `Themes/Icons.axaml`, transcribed rather than
looked up.

The fonts are the one shared thing it touches, `avares://Kitbash.Ui/Assets/Fonts`, named
directly rather than through `FontFamilyUi` and `FontFamilyMono`. That folder holds Archivo
and JetBrains Mono and nothing else, and the splash uses both, so nothing is loaded that is
not drawn.

## What the host gives it

| Property | What it is | Blank or null |
|---|---|---|
| `Mark` | the app's icon, drawn at 48 | the tile collapses |
| `ShowMarkFrame` | whether the accent tile sits behind the mark | mark alone at 48 |
| `AppName` | the product name, drawn in caps whatever case it arrives in | nothing drawn |
| `AppVersion` | the version | the pill collapses |
| `Description` | one line under the name | the line collapses |
| `ShowBackdrop` | the lattice and the brackets | a plain slate card |
| `ShowClose` | the close mark | no close mark |

**Give `Mark` a small bitmap.** It is drawn at 48 or at 26, so a 64 pixel icon is the right
one to hand it and a 256 pixel one is three quarters of a megabyte decoded for nothing.

**`ShowMarkFrame` is on by default**, which is the design: a 48 tile in the accent tint with
the mark at 26 inside it. An app whose icon is already a finished mark turns it off and gets
the icon at 48 with no frame.

## Progress is off until the host says otherwise

`IsProgressVisible` starts false and the row is not drawn. `Report` fills the words and turns
it on, which is the only call a host needs.

**`Step` is optional.** It is the count on the right, such as 3 of 5, and blank drops it
without touching the layout, which is what every overload that does not take one leaves it
as. A host with no countable stages never sets it.

**The bar does both forms and `Progress` is which.** Null is indeterminate, a 30 percent wide
sweep under the fading gradient crossing the track linearly over 1.3 seconds, from just off
one edge to just off the other. A value from 0 to 1 fills the track to that fraction in flat `Accent`, which is what
the library's own `ProgressBar` theme uses. A fraction outside the range is clamped rather
than refused, since a host counting its own stages should never be able to throw here.

```csharp
splash.Report("Reading the workspace registry");              // sweeps
splash.Report("Installing Godot 4.7.1", 0.62, "3 of 5");      // fills to 62 percent
```

**Indeterminate stays the default**, so a host that has nothing to measure reports nothing
and gets the right form. Switching between the two is a plain property change either way.

**A filled bar keeps moving.** A faint white sheen crosses it on a 2.6 second cycle, so a
fraction that has not changed for a while still reads as an app that is running rather than
one that has hung. It is much quieter than the indeterminate sweep on purpose: white at 20
percent over the accent fill, which lifts `#569eff` to about `(120,177,255)` at its peak.

**The sheen crosses the whole track and the fill clips it**, which is why it is a child of the
fill with `ClipToBounds` on rather than something sized to the fraction. Its journey is the
track's width, so it never has to be restarted when a report moves the fill, and it is only
ever seen over the part that is filled.

**Its passes are spaced.** The travel finishes at 60 percent of the cycle and the last keyframe
holds it off the end, so there is a pause between passes instead of a continuous crawl.

**The dot pulses in both forms**, since it says work is happening rather than how much is
left, and it keeps its own 1.6 second cycle across a report so a run of them does not make it
stutter. That is why the two animations are cancelled separately.

**No animation exists unless it is being watched.** The pulse follows the row, the sweep
follows the bar having no fraction and the sheen follows it having one, so the two bar
animations are never running at the same time. All three are cancelled on close, and a splash
that never reports never draws a frame of any of them.

**Each start is idempotent and the track's size is what forces a restart.** `ApplyMotion` runs
on every report, so a start that always restarted would reset the sweep or the sheen each time
a host said anything. Only `OnTrackSized` stops them explicitly, since both measure their
journey from the track.

**A cancelled animation leaves its last frame behind**, so stopping the sweep clears the
render transform with it. Without that a bar switched from sweeping to a fraction would start
wherever the sweep happened to stop.

**The fill glides to a fraction** over 180ms on a cubic ease out, so a report that jumps from
20 percent to 60 reads as the bar filling rather than as it flickering. It is a
`DoubleTransition` on `Width`.

**Its starting width is set before the transition exists, and that ordering is load bearing.**
A transition from the unset width, which is `NaN`, interpolates to `NaN` and the bar never
draws at all. The `Width = 0` in the object initializer sits above `Transitions` for that
reason, so the first fill grows from nothing rather than from nowhere.

**Nothing may read the fill's width back.** It transitions, so `_sweep.Width` is wherever the
transition has got to rather than what it was set to. The sweep sizes its journey from
`SweepWidth`, which is computed from the track, and reading the control instead froze the very
first indeterminate report: the width had only just started moving off zero, the journey came
out as nothing and the animation never started. It looked fine after any determinate report,
since the width had settled somewhere non zero by then.

**A test cannot read the live width either.** While the transition runs, `Width` is wherever it has
got to, and the headless clock advances on real time rather than on forced ticks, so
`ForceRenderTimerTick` does not settle it. `GetBaseValue(Layoutable.WidthProperty)` is the
value that was asked for and is what the tests assert on.

The card is 600 wide and about 98 tall quiet, 151 with progress. `SizeToContent.Height` is
what grows it, so turning progress on grows the window downward rather than reserving dead
space for a row most apps will never show.

## The frame

Same as every other Kitbash window: `WindowDecorations.None`, a transparent background, a
12px transparent gutter carrying `drop-shadow(0 0 12 #60000000)`, an 8px radius and the
`LineWindow` outer edge. The rules in `.claude/avalonia.md` under "There is no usable window
shadow API" apply here unchanged, and the shadow's zero offset and 12px blur have to move
together with the gutter.

It does not derive from `ChromelessWindow`, since that class exists to wear the frame the
theme draws and the whole point here is not to need the theme. It carries no title bar,
cannot be resized, cannot be maximised and has no resize grips.

**The desktop never draws this frame, whatever `window.nativeChrome` says.** That setting
reaches a window through `ChromelessWindow.UsesNativeChrome` alone, and this window has no
such property and reads no setting, so it is already out of reach. It is enforced as well as
structural: `WindowDecorations` is put back to `None` if anything sets it otherwise, and the
`nativeChrome` class matches nothing here because the class the theme's frame selects on is
`chromeless`, which this window never carries.

The reason it is not a choice: a splash is one card with a name, a version and a close mark
on it. A desktop title bar over that repeats the name, adds a second close button and brings
a task bar entry, and the setting exists for windows a person works in rather than for one
that shows for two seconds.

The defaults it sets are `CenterScreen` and `ShowInTaskbar = false`. Both are ordinary
Avalonia properties and a host that wants otherwise sets them.

**It is not topmost.** A window that forces itself over everything else is the wrong answer
for one the person did not ask for and cannot move out of the way, and it would sit over a
dialog the same startup raised behind it. A host that wants it pinned sets `Topmost` itself.

**The whole card moves the window.** There is no title bar to grab, so the window handles
`PointerPressed` on the bubble route and calls `BeginMoveDrag`. The close mark marks its own
press handled and a handled press never reaches that handler, which is the whole of how it
is excluded. The second click of a double click is ignored, since a splash cannot be
maximised and there is nothing for one to do.

**The close mark behaves like a button even though it is not one.** It takes the pointer on
press and answers on release, and only if the release lands back on it, so a press that
wanders off is a press that changed its mind. It is a `Border` rather than a `Button` because
a `Button` has no template without a theme, so the press, the capture, the release and the
hover ink are all wired by hand. `PointerCaptureLost` puts it back to rest, which covers the
window losing the pointer while the mark is held.

## The text carries a soft shadow

Every line on the card takes `drop-shadow(0 1 4 #a6000000)`: the name, the line under it,
the status and the step count. **The version is the one that does not**, since its pill is a
solid ground already and a shadow inside a 22px box only muddies it.

It is a per element `Effect` rather than one over a container, and that is deliberate. An
`Effect` renders its whole subtree to an offscreen surface, so an effect over the progress
row would take the pulsing dot with it and repaint the row every frame of a 1.6 second
cycle. Four small text regions with no animation inside them cost one pass each, and only
the status line repaints when a report changes it.

## Ending the launch

**A person dismissing the splash before the app has a window of its own ends the app.** That
is the window's own behaviour and does not depend on the host arranging it, since a splash
nobody wants is a launch nobody wants and there is nothing else on screen to say so.

Three conditions, all of them required:

1. The close mark was used. A host closing the window itself is a replacement, not a
   dismissal, and never ends anything.
2. No other window of the app is up. Once the host has shown its own, the splash is only a
   splash and closing it closes a window.
3. There is a classic desktop lifetime to end. A host with none is left alone rather than
   crashed on the way out.

It calls `IClassicDesktopStyleApplicationLifetime.Shutdown`, which ignores `ShutdownMode`.
That is the point: a host holding the app open explicitly, the way the launcher does while it
checks for an update, must still let go of a launch nobody wants.

**This is the one place in the window that reaches for `Application.Current`.** A window has
no route to the lifetime otherwise.

## How a host shows one

The order matters, and it is the order in `Kitbash.Gallery/App.axaml.cs`:

```csharp
public override async void OnFrameworkInitializationCompleted()
{
    base.OnFrameworkInitializationCompleted();

    if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

    var splash = new SplashWindow { ... };

    desktop.MainWindow = splash;      // the temporary main window
    splash.Show();

    var window = await Start(splash); // the real startup, reporting as it goes

    desktop.MainWindow = window;      // swapped before the splash closes
    window.Show();
    splash.Close();
}
```

**The swap has to come before the close.** The lifetime ends the app when the last window
closes, so closing the splash while it is still the main window and nothing else is up would
end the launch. Swapping first is what makes the close a replacement.

**`Initialize` must not load the look.** `AvaloniaXamlLoader.Load` on an `App.axaml` carrying
`FluentTheme` and `KitbashTheme` parses all of it before any window can exist, which is the
wait the splash is there to cover. The gallery's `App.axaml` carries no styles at all and
`LoadTheme` adds all three once the splash is on screen. A window built afterwards still
wears them, which
`SplashWindowTests.TheLookCanArriveAfterTheSplashIsAlreadyUp` pins.

**The startup work still holds the UI thread.** Building the gallery's window takes long
enough to stall the splash's animations while it runs. That is the host's problem to solve,
not the splash's, and the rule in the `kitbash-windows` skill about keeping the UI thread free
applies to startup like anything else.

## What it actually costs

Measured on this machine, Linux under X11, warm cache, five runs each, medians. The gallery
carries a startup probe for this: set `KITBASH_STARTUP_PROBE` to the wall clock in
microseconds taken just before the process is created and it prints the breakdown and exits
without opening the gallery.

```bash
cd src/Kitbash.Gallery/bin/Release/net10.0
T0=${EPOCHREALTIME/./}; KITBASH_STARTUP_PROBE=$T0 ./Kitbash.Gallery
```

Visible means the compositor has rendered the batch the splash is in, which is the last thing
the process can observe before the pixels belong to the desktop.

| Stage | dotnet build | published, ReadyToRun |
|---|---|---|
| process start to `Main` | 15 | 15 |
| AppBuilder configured | 6 | 2 |
| platform up and `Initialize` | 325 | 265 |
| lifetime ready | 1 | 1 |
| splash constructed | 160 | 100 |
| `Show` returned | 108 | 41 |
| first frame rendered | 160 | 93 |
| **total to visible** | **780** | **527** |

All figures milliseconds. The published column is
`dotnet publish -r linux-x64 --self-contained -p:PublishReadyToRun=true`, which is what
Velopack ships, so it is the number that matters.

**About half of it happens before any of this code runs.** Process start plus the Avalonia
platform coming up is 280ms of the shipped 527, and nothing in this window can touch it.

**The splash's own construction is first touch, not work.** Building a second `SplashWindow`
straight after the first takes 2 to 4ms against the first one's 100. The 100ms is the JIT and
type initialization of the Avalonia types it uses, which the first window of any process pays.

### What else was tried

Measured the same way, so the deltas are comparable even where the absolute figures are the
`dotnet build` ones.

| Lever | Effect | Worth doing |
|---|---|---|
| composite ReadyToRun | 527 to 430ms | yes, but it costs 31MB and hurts delta updates |
| embedded fonts dropped | none at all | no, there is nothing there |
| X11 software rendering | 780 to 650ms | no, it pays for startup with every frame after |
| NativeAOT | unknown, does not build | not without work, see below |

**The fonts cost nothing.** Replacing both families with `FontFamily.Default` changed the
first frame by less than the run to run spread, so the folder holding all eight faces when
the splash draws three is not worth splitting.

**About 100ms of the platform start is the GPU.** Forcing `X11RenderingMode.Software` takes
platform start from 320ms to 220 and the first frame from 170 to 125. It is not a trade worth
making for an app that scrolls and animates, but it says where the time goes.

**Composite ReadyToRun is the one free win**, at
`-p:PublishReadyToRunComposite=true`. It compiles the whole closure as one image, which takes
the splash's construction from 100ms to 70 and the first frame from 93 to 85. The published
folder grows from 130MB to 161MB and becomes one large image rather than many small ones,
which is the thing to weigh before putting it in `build/release.sh`, since Velopack ships
deltas.

**NativeAOT does not build**, and the blockers are all in `Kitbash.Core` rather than in
anything Avalonia does. `System.Text.Json.JsonSerializer.Deserialize` in
`WindowsWorkspaceOpenerFinder` and `JetBrainsToolbox`, `TomlSerializer` in
`TomlSettingsDocumentStore`, and `Array.CreateInstance` in `SettingsValueConverter`, all
`IL2026` or `IL3050`. Each needs a source generated context instead of reflection. It is the
largest remaining lever, since it removes the first touch cost entirely, and it is a project
rather than a flag.

**Roughly 250ms is a floor** for any managed Avalonia app here. Nothing can draw before the
platform is up, and the only way under it is a separate native binary shown before the runtime
starts, which is a large amount of complexity for one window.

**Deferring the theme is worth about 150ms.** Measured both ways on the same build: with
`FluentTheme` and `KitbashTheme` back in `App.axaml`, `Initialize` goes from 325ms to 503ms
and total to visible from 780ms to 929ms. That is the whole reason this window depends on no
styles, and it is the one number to re-check if the rule is ever relaxed.

## Departures from the design

- **The close mark's resting colour is `InkMuted`, not the design's `#6b7078`.** That value
  is not a token and it sits between `InkMuted` and `InkDisabled`. Hover is `InkCaption`
  rather than the design's `#e5eaef`, which is the same tier the caption buttons use.
- **The close glyph is stroked rather than shadowed.** The design thickens it with two 0.5px
  `drop-shadow` filters in the current colour, which is a browser trick for making a glyph
  bolder. The same weight comes from stroking the path in its own fill, which is one pen
  rather than two offscreen passes. The stroke is 1.5 in the glyph's 24 units, so it lands
  near a pixel at the 16 it is drawn at, and it is rounded at both the joins and the caps so
  the Box Icons terminals keep their shape.
- **No tooltip on the close mark.** A tooltip is a popup, a popup is a window, and the first
  one a process opens costs about 60ms of machinery. A splash is the wrong place to pay it.
- **The lattice does not drift.** The design file declares an `sp-drift` keyframe but the
  Deco Pattern component it would apply to is a static SVG, and a 56 by 56 translation does
  not loop on a 64 by 32 tile anyway. A drifting lattice would also repaint the whole window
  forever, which is the opposite of what this window is for.
- **The indeterminate sweep is linear and crosses clean off both edges**, against the
  design's `cubic-bezier(.5,.05,.4,.95)` ending at 320 percent. Two things made the design's
  read as a rush followed by a wait, and both are fixed here. The easing spends its slow ends
  off the track, so the only part a person sees is the fast middle. And ending at 320 percent
  leaves a lit sliver at the right edge that vanishes on the wrap, which is a pop rather than
  an exit. It now runs at a steady 534 pixels per second from fully off the left to fully off
  the right, so there is a bar on the track at all times bar the instant of the wrap.
  **One sweep, never two.** The pulse and the sheen keep their own timings.
- **The text carries a soft shadow, which the design does not draw.** The design sets its
  words on a veil dark enough to carry them, and a shadow reads better over the lattice at
  the sizes this ends up at.
- **The outermost line of each corner file is dropped.** The corner files carry three nested
  brackets at the top and two at the bottom, and the outer one of each is a 2px line hard
  against the card edge. That reads as a second border beside the one the frame already
  draws, so only the inner lines are kept: two at the top corners and one at the bottom.
  Dropping it also removes an overlap, since the outer legs were 94 and 80 long on a card
  about 151 tall and ran into each other on both edges.

## What was not verified here

The window was rendered headlessly with real Skia drawing at both sizes, in both bar forms
and with the backdrop on and off. **Dragging has not been driven on a real desktop**, since
`BeginMoveDrag` hands off to the platform and the headless one does nothing with it. **The transparent gutter and the drop shadow have not been seen on a
real desktop**, on either platform, and a compositor that refuses transparency will draw the
gutter as an opaque rectangle around the card. `TransparencyLevelHint` asks and does not
promise, which is the same bargain every other Kitbash window makes.

**Both apps open behind one.** The gallery's `App.axaml.cs` is the worked example of the
order above, and its SPLASH section opens a second one on demand so both bar forms can be
driven by hand.

**A feed with nothing to offer takes the row back down.** `IsProgressVisible` goes false and
the card collapses to the mark and the name for the rest of the wait, since nothing is being
waited on any more. A download that failed lands there too.

**The launcher hosts it and the update reports into it.** `Views/UpdateDialog` is deleted.
The splash is up before the feed is asked, so the check is no longer a blank screen, and it
carries the download as a fraction with the version and the size beside it. `UpdateStages` in
`Kitbash/Updates` holds the wording, so it is tested without a window. Two numbers are the
launcher's own: the splash is up for at least two seconds before the launcher replaces it, and
a full bar is held for 600ms before the swap starts, since the fill takes 180ms to travel.
The swap itself goes back to indeterminate under "Restarting Kitbash", because replacing the
copy on the machine reports nothing.

**Dismissing during a download quits.** It cancels the fetch and the window ends the app
itself, which is the rule above applied rather than an exception to it. The dialog used to
cancel and open the launcher anyway, and `.claude/skills/kitbash-updates/SKILL.md` records the
change.

**The launcher's mark wears no tile.** `ShowMarkFrame` is off, since the Kitbash badge is a
finished mark and the accent tile fights its own shape. Rendered both ways before choosing.

**The gallery's startup has not been launched here**, since this machine has no virtual
display and driving the real desktop is not how this repository verifies UI. What is verified
headlessly is the part that could quietly break: that the look arriving after the splash is up
still reaches a window built later.
