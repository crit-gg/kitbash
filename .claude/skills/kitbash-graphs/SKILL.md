---
name: kitbash-graphs
description: "Kitbash node graph rules. The canvas, the model that carries its own geometry, virtualising in two dimensions, the level of detail steps, wire routing and caching, model space hit testing, and the gestures. Read before changing anything under Kitbash.Ui/Graphs or building a tool that draws a graph."
---

### The node graph

`ui:NodeGraph` is a canvas of nodes joined by wires, built for many hundreds of them at
once. `.claude/plans/node-graph.md` is the long form, including what is not built yet.

**Geometry is data, never layout.** A node's box comes from its port count and the metrics,
so the graph knows where every node and every pin is with no control realised. Culling,
fitting, the marquee, hit testing, the minimap and every wire endpoint read the model. This
is the rule everything else follows from, and breaking it breaks the control.

So `GraphNode.Height` is a stored number that the node writes itself whenever its shape
changes, rather than a calculation on every read or a measurement off a container. Setting it
by hand on a node is undone the next time a port is added or it is collapsed. A node with a
body of its own says `BodyHeight` and the arithmetic follows.

**`GraphMetrics` is the one place a size is written**, and it is the density set. The
comfortable include swaps the whole object with a `Style` on `ui:NodeGraph`, since a resource
lookup per node is exactly the cost this control exists to avoid.

**Only what meets the viewport is realised.** `NodeGraphPanel` asks the index what meets the
viewport, holds a container for those alone and hides the rest into a pool. Measured over
2,000 nodes on a 1600 by 900 canvas: 72 realised, and panning the whole graph never went
above that.

**A pooled container stays in the tree, hidden.** Taking it out and putting it back runs the
whole attach and restyle path, which is what a pool exists to avoid. Measured: 7.2ms a pan
frame with the tree churning, 3.5ms without.

**The pan and the zoom ride on the panel's own `RenderTransform`.** Arranging cards at screen
positions instead means a pan re-arranges every visible card, and a bounds change re-runs
`Render`, so every card's text is laid out again every frame. Measured over 2,000 nodes:
5.4ms a pan frame arranging, 0.27ms with the transform. **Do not arrange a card in screen
space.** A card is arranged when it is realised and when its node moves, and never otherwise.

**A node is drawn, not composed.** `NodeCard` paints the whole card in one `Render`, so a
realised node is one control rather than thirty. `NodeGraph.NodeTemplate` is the way out: set
it and the card draws the frame, the header and the pins while the template fills the body,
which is what a preview thumbnail needs. **The pins stay the library's either way**, so a wire
always ends where one is drawn.

**`FooterTemplate` fills the strip `GraphNode.FooterHeight` leaves under the body.** A node
carrying a readout would otherwise have to give up body room for it. A node asking for no
footer never builds one.

**A presenter is handed its content before its template.** Told the template while it still
holds nothing, it builds that template against null, since a template set by hand is used
without being matched against the data first. It cost a null reference the first time.

**`PortLayout.Edge` spreads the pins down the node's outer edge**, evenly over the body, which
is what a node whose body is a picture needs. There is no room for names there, so **resting
on a pin is what shows its name**, in a chip beside it. Without that an edge pin says nothing
at all about what it is.

**`GraphPort.Shape` says the type a second time.** Colour alone fails for anyone who cannot
tell two of them apart, so a round pin, a diamond and a square are three readings of the same
thing. The diamond is the square turned on its corner, so both keep the same reach.

**A card's edge is drawn last and drawn inside the card.** Two rules, and both are about the
same failure. Drawn with the fill, the header's own rectangle paints over it and the ring
appears to start below the header, which is what it looked like at first. Centred on the
card's outline, half the stroke falls outside and a body filled by a template swallows the
half that is left, so the edge reads as missing along the body.

So: fill, then everything inside clipped to `Inside`, then the edge stroked on `Edged`, which
is half a pen in, so the whole line lands inside the card. A template's presenter carries the
same clip, since a visual child draws after `Render` and would otherwise paint over the edge
and square off the corners beside it. Measured on a 1px pen at zoom 1: one device pixel of
`LineControl` with the ground on one side and the body on the other.

**The header is a plain rectangle against that clip**, not a rounded one of its own. It meets
the card's curve at the top, and on a collapsed node, where the header is the whole card, it
meets it at the bottom too. Given its own radius it squared those corners off.

**The picked ring starts where the box ends.** The edge is inside the box, so the ring is
inflated by half its own pen and nothing else, and the card does not appear to grow when it is
picked.

**Text is laid out once and kept.** `GraphText` holds every laid out run by its text, its
role, its brush and its width, so a title survives a pan, a zoom and its own container being
recycled. Text is the expensive part of drawing a graph at scale and this is the whole answer
to it.

**Three levels of detail, and the last one realises nothing.**

| Step | Zoom | What is drawn |
|---|---|---|
| `Full` | 0.5 and up | everything |
| `Reduced` | 0.28 to 0.5 | the header, the title and the pins |
| `Block` | under 0.28 | a box per node in its kind colour, and no containers at all |

At `Block` the panel paints the boxes itself, frames first since a frame is the only part of
a graph still readable at that size. A name there is a smudge that costs more than everything
else together.

**A view change repaints the two drawn layers and leaves every card alone.** A card's own
drawing does not change when the view moves, only where it is put. Only crossing a level of
detail makes what a card draws stale. A hover over a pin repaints the two cards involved and
nothing else.

**A wire is routed once and kept on the link.** `GraphLink.Route` builds it, and only one of
its two nodes moving throws it away. Wires draw in graph space under one transform, so a pan
or a zoom rebuilds nothing.

**A wire is culled off its two ends before it is routed.** `GraphLink.Rough` is the box it
cannot leave, from the pins and the reroute alone. Culling on the routed path would route
every wire in the graph to find the handful on screen.

**Wires are indexed too, in `LinkIndex`.** The first cut scanned them, on the reasoning that a
moving segment in a spatial hash is churn. That was wrong twice over: a wire only moves when one
of its two nodes does, so the churn is bounded by what is being dragged, and a scan is paid on
every pointer move as well as every draw. Measured over 2,000 nodes and 1,960 wires: 0.59ms a
pointer move scanning, 0.29ms indexed, and one hit test against the model is 1.9 microseconds.

`GraphLink.Indexed` is the box a wire currently sits under and it is never smaller than the wire,
so a query answers with a superset and every caller still tests what it is handed. `Box` is the
routed path's own bounds when there is one and `Rough` otherwise, which is why culling never
routes anything.

Nodes, frames and notes go in `GraphIndex`, the same shape over `GraphItem`.

**Pens are cached by colour and width.** A pen built per wire per frame is the allocation that
shows up first.

**Hit testing reads the model, never the visual tree.** Avalonia captures the pointer to the
control that was pressed, so nothing a drag passes over hears a move of its own. That is the
rule the data grid already found. `GraphHit` answers pins first, then nodes and notes, then
reroutes, then wires, then a frame's label tab.

**A frame's body is not hit tested at all**, so a marquee started inside one works. A frame is
dragged by its label tab, which is drawn above the box and is why the card is arranged taller
than the frame's own bounds by `GraphFrameCard.Lead`.

**A box built from two loose points must be normalised.** Avalonia's `Rect(Point, Point)` takes
a top left and a bottom right and does not, so a drag that ran right to left made a rectangle
with a negative width, which intersects nothing and draws nothing. It caused two bugs at once:
a box select that only worked one way, and a wire running backwards that was culled from both
drawing and hit testing. `GraphBox.Between` is the only way to build one here.

**A node card is hit test visible and draws no background**, so a real control inside a node
body takes its own press and the canvas never sees it, while a card with nothing in it lets
the press through.

**A wire runs from an output to an input and `IPortRules` says whether it may.** The default is
equal type names and nothing else, deliberately: a graph that silently widens is a graph whose
author cannot see why. An app replaces it, usually with one widening rule.

**An input holds one wire.** `GraphModel.Add(GraphLink)` takes out whatever was in the input
first. **Pressing a wired input carries that wire** rather than starting a second one, so a
link is moved by picking it up at the end a person can see.

### The add node menu

`ui:NodePalette` is the library's, because every graph needs it and because three separate
gestures open it: Tab, a right click over nothing, and a wire let go over nothing. The app
supplies `NodeGraph.Catalogue`, a list of `NodeChoice`, and the menu does the rest. **With no
catalogue nothing opens and `PaletteAsked` is raised instead**, so an app that wants its own
overlay simply does not set one. Two states, no flag.

**Getting out of it is the part to get right.** A menu a person cannot see how to leave is a
menu they close by picking something they did not want. Four ways out, and all four are real:

- **A press anywhere on the canvas closes it and does nothing else.** Trying to click away is
  the first thing anyone does. That press must not also start a box select behind the menu or
  drop what was picked, so it is consumed.
- **Escape**, from the query field or from the canvas.
- **The Cancel button in the footer**, which is the one visible affordance.
- **The footer says both**, "Enter to add" and "Esc to cancel".

**Nothing inside the menu reaches the canvas.** The palette handles `PointerPressed` and
`PointerWheelChanged` on its own root, or a press on its padding would start a marquee behind
it and a wheel would zoom the graph out from under it.

**One cursor serves the keyboard and the pointer.** Hovering a row is the same as arrowing to
it, so Enter always adds the row a person is looking at. There is no separate hover state to
disagree with a separate selection.

**A heading is a row, not a container around one**, so the list stays flat and virtualises like
every other list here. It is disabled through `ContainerPrepared` and `ContainerIndexChanged`,
told in one place and untold in the same one, and its own `Foreground` beats the disabled ink.

**Opened off a pin it lists only what that pin reaches**, filtered through `IPortRules`, with a
line saying so. Entries are left out rather than offered and refused after the fact.

**The node lands where the wire was let go.** Not its top left corner: the pin the wire will
join is placed at the drop point, so the node arrives already joined at the spot a person
aimed at. `Landing` finds that port and `Metrics.PortOffset` says where it sits.

**It is held inside the canvas on every size it takes.** The list grows and shrinks as a query
narrows it, so `Clamp` runs again on each bounds change rather than once when it opens.

The inspector, the problems strip and the zoom bar are still the app's own, ordinary Kitbash
surfaces over the model. `Overlay` is the slot they go in.

### Frames, which are the groups

A frame is a labelled box drawn behind a run of nodes. **It never owns anything.**

**Membership is where a thing is, not anything recorded.** A frame carries whatever has its
middle inside it, worked out when the drag starts. So a node dragged into a frame belongs to
it from then on, with nothing to keep in step and nothing to go stale, and resizing a frame is
how what it holds is chosen. The middle rather than the whole box, so a node overhanging an
edge still comes along.

**A smaller frame inside one is carried too**, and everything inside that one is already
inside the outer one, so nesting needs no walk. A frame never carries one as large as itself,
which is what stops a child taking its parent with it.

**Resizing is by any edge or corner**, over a band of `FrameEdgeReach` either side of the
border. It answers **after** the wires in the hit test, since a frame's border is long and a
wire crossing it is thin, so Alt clicking a wire where it leaves a frame still works. Nothing
inside moves while it is resized.

**A frame never turns inside out.** The edge being dragged is the one that stops at
`FrameSmallest`, so the opposite one stays where a person put it.

**The edge comes forward when it is picked or hovered**, with four corner marks that hold one
size on screen. Nothing else says an edge can be pulled.

**Removing a frame leaves what stood on it**, whether by Delete or by Ctrl Shift G. A group is
a box behind a run of nodes and taking the box away has never meant taking the nodes away.

**Ctrl G puts one round the selection**, with room above the box for the tab, or the label
lands on whatever it was drawn around. **The colour is cycled** off `GraphColours.Frames`, so
two frames made one after another do not come out the same, which is the whole reason a frame
carries one.

### Renaming

**One field renames everything**, `PART_Rename` in the graph's own template, because only one
thing is ever being renamed. `GraphItem.Label` is what it reads and writes: a node answers with
its title, a frame with its name, a note with its prose. `LabelBox` says where it opens and
`LabelIsProse` says whether it takes several lines.

**F2 renames what is picked, and a double click on a frame's tab or on a note renames that.**
A double click on a node is left alone, since `Activated` is the gesture for opening one.

**The field is kept legible rather than scaled with the graph.** A person zoomed out is
renaming a thing they can see, not reading it at its drawn size.

**Enter keeps it, Escape puts the old name back, clicking away keeps it**, which is what every
field in the app does. Prose takes Enter as a line and is committed with the modifier instead.
**An empty name is refused** and the old one stands, since a name nobody can see is not a
name. Prose may be emptied.

**Escape and Enter are marked handled in the field**, or they would bubble to the canvas and
clear the selection.

### The minimap

**A press goes there and holding on carries the view**, since a map a person can only tap is a
map they have to tap over and over. **A press inside the viewport takes hold of it where it
was grabbed** so it does not jump out from under the pointer, and a press outside goes there
first, which is what a press on a map is for.

### Working on a graph rather than on one node

Everything here came out of reading what Blender, Unreal and Substance actually offer, and
each is a thing a person reaches for the moment a graph stops fitting on one screen.

**A node dropped across a wire goes into it.** Doing it by hand is three gestures: break,
wire, wire. Only one node splices, since a run of them dropped on a wire has no order anybody
could have meant, and only when it has a free input and an output the rules allow. The points
the wire was routed through stay on the length before the node, since that is the length they
were on.

**Control and a drag draws a stroke that cuts every wire it crosses.** Tidying means cutting a
run of them at once. The stroke is drawn while it is held, so a person sees what it will take,
and it picks nothing, since a cut is not a box select. `Crosses` walks the stroke in short
steps and asks the path at each, which needs no curve intersection and is exact enough at this
size.

**Alignment reads the order things were picked in.** `GraphSelection.Items` is a list for that
reason and `Anchor` is the last one: everything lines up with the node just clicked, which is
Unreal's rule and the one a person expects. `SpreadSelection` leaves the two on the ends where
they are and evens the gaps between the rest, so the run keeps the width it was given.

**Straightening moves the node a wire goes into**, so a chain straightens out along the way it
flows rather than dragging its source about.

**F fits what is picked**, and the whole graph only when nothing is.

### Reading a graph without a pointer

The canvas had none of this and the grids set the standard, so:

**The arrows walk from node to node.** Plain arrows move the selection in that direction,
Shift takes the next one as well, Control nudges what is picked by the snap step. The scoring
is directional navigation's usual: distance along the way plus a heavy penalty for distance
off to the side, so a node straight ahead beats a nearer one well off the line.

**A walk only moves the view when it has to.** `Reveal(item, keep: true)` centres on the item
only when it is not already comfortably on screen, or stepping along a row swings the canvas
about on every press.

**The canvas and its cards say what they are holding.** `GraphAutomationPeers.cs`, and as with
the grids it is a name and a help text and no more, since Avalonia ships no graph pattern. The
canvas says how many nodes and wires it holds and what was picked last, and a card says its
title, its kind, its port counts and whether it is collapsed, bypassed or wrong.

### The rest of the gestures

**Escape gives back the most recent thing first**: the menu, then a drag under way, then the
selection. One key, one step back each time.

**A drag can always be taken back.** `Cancel` puts back node positions, a reroute, the pan
offset and a wire that was picked up off an input and never landed. A drag that cannot be
taken back is a drag a person is afraid to start. `OnPointerCaptureLost` runs the same path,
since something taking the pointer away mid drag would otherwise leave the gesture running
with nothing driving it.

**Read what a release needs before dropping the capture.** Releasing the pointer raises a
capture lost, which is the cancel path, so state read after `Capture(null)` has already been
cleared. Measured: the wire drag stopped connecting anything at all until the reads moved
above it, and `Cancel` returns early when nothing is being dragged for the same reason.

**A right click does one thing, not two.** Over nothing it asks for a node, over anything it
raises `ContextAsked`, and it never fires both.

**A wire let go over a pin that refuses it is dropped.** The design opens the menu there and
it should not: a person aimed at a pin, and answering a refusal with a menu they did not ask
for reads as the pin having been accepted.

**A wire under the pointer lights and offers nothing.** Alt and a click is what takes one off.
A mark drawn on the hovered wire was tried and taken out again: a button that appears wherever
a pointer happens to rest acts on a wire nobody asked about, which is the rule the diff reader
already keeps for its own actions.

**A wire takes as many reroute points as a person adds.** `GraphLink.Reroutes` is the run it
passes through, in order, so a long wire can be routed round whatever is in its way. A double
click **inserts on the length of wire it landed on** rather than at the end, or a point added
before an existing one doubles the wire back on itself. `NodeGraph.Length` is what works out
which length. A double click on a point takes off that one and leaves the rest.

**A collapsed node drops its body and its strip.** Left in place the strip is laid out against
the collapsed height and covers the very header the node collapsed to.

**A card is told about its node again whenever the model changes**, through
`NodeGraphPanel.Reslot`. A node that collapses or grows a body while it is on screen changes
which slots it should have, and it is already realised, so nothing else would ask. It runs on a
model change and never on a pan.

**Invalidating a card does not invalidate the layer over it.** `NodeCard.Redraw` is what every
repaint goes through for that reason: invalidating the card alone left the pin layer holding
what it drew last, so a pin name never appeared under the pointer.

**A frame carries whatever is standing on it.** Moving the box and leaving the nodes behind
would point the label at nothing. Those nodes move without being picked, since the frame is
what was grabbed.

**A reroute comes off the way it went on**, by a double click.

**Duplicate copies the wires between what is picked and no others.** A copy that quietly reads
someone else's output is a surprise.

**The pointer says what a press would do.** Cross over a pin, a hand over the break mark, the
move cursor over a reroute and a frame's label. It is the only thing that tells a person a pin
starts a wire before they have tried it.

**A drawn card takes itself out of the tree's hit testing.** It holds nothing that could want a
press and the canvas tests the model, so leaving it hit test visible only pays for pointer over
bookkeeping on every card a pointer crosses. A card with a `NodeTemplate` stays hit test
visible, since something inside it may want the press.

**The library decides nothing about what a node means.** A kind is a name and `GraphKinds` maps
it to an icon and a colour. A port type is a name and `IPortPalette` maps it to a colour, with
a stable fallback off the name so a graph draws before an app has described anything in it.
Nothing here evaluates, validates or orders a graph.

**Anything an app hands over is its own.** A theme change rebuilds the brushes, the pens, the
glyphs and the text, and it must not take back a `Kinds` or a `Ports` the app set, since those
hold the app's own colours.

**The minimap is drawn once into a bitmap.** It costs the number of nodes to draw and a pan
would pay that every frame, so only the viewport rectangle moves after the first pass. The
bitmap goes when the graph changes, when the map is resized or when the theme is.

**The dot grid is one tiled bitmap.** Measured on a 1600 by 900 canvas with a software
rasteriser: a tiled `DrawingBrush` cost 17ms a frame and a bitmap tile costs 7, which is what
an alpha blend over 1.4 million pixels costs there and is one quad on a real backend. The step
is rounded to whole pixels, which also stops a dot landing between two and reading as a
smudge, and it steps up by four rather than crowding when the zoom drops.

**`Panel.Render` is sealed in Avalonia**, so anything that draws and holds children derives
from `Control` and parents its own children through `VisualChildren` and `LogicalChildren`.
`NodeCard`, `NodeGraphPanel` and every layer do.

The gallery's THE NODE GRAPH page is the harness, over the design's own sample and over a
graph of two thousand nodes, which is the only way to read the first claim the control makes.
