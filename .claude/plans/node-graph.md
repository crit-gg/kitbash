# The node graph

`ui:NodeGraph` is a canvas of nodes joined by wires. It is the fourth whole surface
`Kitbash.Ui` owns, after the settings window, the projects window and the splash, and it is
the one built for scale: **many hundreds of nodes in one graph, with no slowdown a person
can feel.**

Two designs are the source. `Node Graph Control.dc.html` is the control itself, a value
graph with typed pins, inline values, frames, a palette and a minimap. `Rime.dc.html` is a
second use of the same canvas as a material graph, where a node is a preview thumbnail with
pins on its outer edge and a cost strip under it. **The two disagree about what a node looks
like and agree about everything else**, which is what decides the seam: the library owns the
canvas, the wires, the interaction and the geometry, and an app owns what a node draws.

## The performance rule, and everything that follows from it

**Node geometry is data, never layout.** A node's box is worked out from its port count and
the metrics, so the graph knows where every node and every pin is without a single control
existing. Culling, fitting, the marquee, hit testing, the minimap and the wire endpoints all
read the model.

That one rule is what makes the rest possible.

**Only what can be seen is realised.** `NodeGraphPanel` asks the spatial index which nodes
meet the viewport and holds a container for those alone, recycling the rest into a pool. It
is the same answer `ui:Tree` and both grids already give, applied to two dimensions instead
of one. Measured over 2,000 nodes: 24 containers realised, and panning across the whole graph
never went above 40.

**A node is drawn, not composed.** The default `NodeCard` paints its own header, title, pins,
names and values in one `Render`, so a realised node is one control rather than thirty. Text
is laid out once per node and cached, since text is the expensive part of any graph at scale.
A tool that needs real controls in a node body sets `NodeTemplate` and gets the composed path
instead, which is what Rime's thumbnail needs.

**Panning and zooming move a transform and nothing else.** Nodes are arranged at their graph
coordinates under one `RenderTransform` on the panel, so a pan runs no layout pass at all. The
only work a pan does is realising whatever scrolled in.

That is not a nicety. Arranging cards at screen positions instead means a pan re-arranges every
visible card, and a bounds change re-runs `Render`, so every card's text is laid out again every
frame. Measured over 2,000 nodes: 5.4ms a pan frame arranging, 0.27ms with the transform.

**Wires are drawn in graph space under that same transform.** A wire's geometry is built once
and cached on the link, keyed on its endpoints, so a pan or a zoom rebuilds none of it. Only
moving one of its two nodes does. Pens are cached per colour and per width, since a pen built
per wire per frame is the allocation that shows up first.

**A wire is culled by its own cached bounds.** Links are scanned rather than indexed: a moving
segment in a spatial hash is churn, and a rectangle test over ten thousand links costs less
than keeping them sorted.

**Below a zoom the graph stops being readable, so it stops being drawn.** `GraphLod` is three
steps:

| Step | Zoom | What is drawn |
|---|---|---|
| `Full` | 0.5 and up | everything, names, values and badges |
| `Reduced` | 0.28 to 0.5 | the header, the title and the pins, no port names and no values |
| `Block` | under 0.28 | a box per node in its kind colour, no text, **no containers at all** |

At `Block` the panel realises nothing and paints the boxes itself, frames first, since a frame
is the only part of a graph still readable at that size. A person looking at two thousand nodes
at once is reading shape and colour, and a name there is a smudge that costs more than every
other thing on screen put together.

**The dot grid is one tiled bitmap.** Measured on a 1600 by 900 canvas with a software
rasteriser: a tiled `DrawingBrush` cost 17ms a frame and a bitmap tile costs 7, which is what
an alpha blend over 1.4 million pixels costs there and is one quad on a real backend.

**The minimap is drawn once into a bitmap too.** It costs the number of nodes and a pan would
pay that every frame, so only the viewport rectangle moves after the first pass.

## The parts

| Part | What it is |
|---|---|
| `GraphModel` | the nodes, links, frames and notes, with the index under them |
| `GraphIndex` | a uniform spatial hash over everything that has a box |
| `GraphMetrics` | the geometry a node and a wire both read, off the theme |
| `GraphView` | the pan and the zoom, and the two conversions |
| `WireRouter` | a link to a path, bezier or orthogonal |
| `NodeGraph` | the control: the layers, the input and the gestures |
| `GraphBackdrop` | the dot grid |
| `GraphWires` | every wire, the live one and the reroute handles |
| `NodeGraphPanel` | the realised containers, and the blocks at `Block` |
| `NodeCard` | one node, drawn |
| `NodeMinimap` | the whole graph small, with the viewport on it |
| `GraphZoomBar` | the four buttons at the foot of the canvas |

## Hit testing is model space

**The canvas hit tests the model, not the visual tree.** Avalonia captures the pointer to the
control that was pressed, so nothing a drag passes over hears a move of its own. This is the
rule the data grid already found and it applies here for the same reason. `GraphHit` answers
what is under a graph point: a pin first, then a node, then a reroute, then a wire, then a
frame edge, then nothing.

A node card is still hit test visible, so a real control inside a node body works the way any
control does. It marks the press handled and the canvas never sees it.

## The gestures

Taken from the design's own shortcut sheet, which is Blueprint's and Blender's between them.

| Gesture | What it does |
|---|---|
| wheel | zoom at the pointer |
| middle drag, or Alt drag | pan |
| drag empty space | box select, Shift adds |
| drag a node | move the selection |
| drag a pin | draw a wire, release on empty space to open the palette wired to it |
| drag a wired input | detach it and carry it |
| Alt click a wire | break it |
| double click a wire | add a point to route it through, on the length that was clicked |
| double click a point | take that one off |
| double click a wire | add a reroute |
| Tab | the add node menu |
| Ctrl G | put a frame round the selection |
| Ctrl Shift G | take the picked frames off, leaving their contents |
| F2 | rename what is picked |
| drag a frame's edge | resize it |
| double click a frame's tab | rename it |
| Ctrl D | duplicate, wires between the copies included |
| Ctrl A | pick everything |
| F | fit what is picked, or the whole graph when nothing is |
| Q | straighten the wires between the picked nodes |
| Ctrl L | pick everything the selection reaches |
| arrows | walk from node to node, Shift adds, Control nudges |
| Ctrl drag | draw a stroke that cuts every wire it crosses |
| drop a node on a wire | put it into that wire |
| Ctrl D | duplicate |
| Del | delete the selection |
| B | bypass |
| Esc | close the menu, then cancel a drag, then drop the selection |
| drag a frame's label | move it with everything standing on it |

**A wire only ever joins an output to an input**, and `IPortRules` says whether two types may
meet. The default rule is equal types, plus a widening every graph wants, and a tool
replaces it.

## What the library does not decide

- **What a node is.** `GraphNode` carries an id, a title, a kind, a width and its ports. A
  kind is a name, and the app maps it to an icon and a colour.
- **What a graph means.** Nothing here evaluates, validates or orders anything. The problems
  list in the design is the app's own reading of its model.
- **Undo.** `GraphModel.Changed` says what moved, and a tool builds its own stack on it. A
  command stack in the library would decide what an edit is, and only a tool knows that.
- **What nodes exist.** `NodeGraph.Catalogue` is a list of `NodeChoice`, and the menu that
  lists them is the library's. With no catalogue nothing opens and `PaletteAsked` is raised
  instead, so an app that wants its own overlay simply does not set one.
- **The inspector and the problems strip.** Both are ordinary Kitbash surfaces over the model.

## Frames own nothing

A frame carries whatever has its middle inside it, worked out when the drag starts, so
membership is where a thing is rather than anything recorded. Nothing has to be kept in step,
nothing goes stale, a node dragged in belongs from then on, and resizing a frame is how what it
holds is chosen. Removing one leaves its contents, whether by Delete or by Ctrl Shift G.

## Getting out of the add node menu

Worth its own heading, because it is the part that was wrong first time and the part a person
notices. A menu somebody cannot see how to leave is a menu they close by picking something
they did not want.

Four ways out and all four are real: a press anywhere on the canvas, which closes it and does
nothing else at all, Escape from the field or from the canvas, the Cancel button in the
footer, and the footer saying so in words. Nothing inside the menu reaches the canvas, so a
press on its padding cannot start a box select behind it and a wheel cannot zoom the graph out
from under it.

## Not built yet

- A frame has no colour picker of its own. New ones are cycled off the theme and an app sets
  `GraphFrame.Colour` for anything else.
- A frame does not grow to fit what is put in it, and there is no auto arrange inside one.
- No layout algorithm beyond align and distribute. A tidy pass over a whole graph is a real
  algorithm and it needs a real graph to be judged on.
- Nothing has been run on Windows or on macOS. Everything above was measured on Linux with a
  software rasteriser in a Debug build, so the drawing numbers are a ceiling rather than what a
  real backend costs.

## The two shapes a node takes

The drawn node is the default and the fast path. The composed node is `NodeTemplate` plus, if
the node asks for a footer, `FooterTemplate`, and the card still draws the frame, the header
and the pins around whatever they put there. Rime's material graph is the second shape and the
gallery's A MATERIAL GRAPH page is it, over the design's own rusted plate graph.

**That page also demonstrates the other half of the seam.** The library evaluates nothing, so
the page walks the model itself, cooks every node into a small texture and hands the result
back through the node body. Rewiring anything or moving any parameter changes what everything
downstream of it shows. `GraphItem.Tag` is where a node's own meaning hangs, which is what it
is for.

## What it was measured with

`tests/Kitbash.Ui.Tests/NodeGraphScaleTests.cs` builds fifty by forty nodes with a wire between
each pair and reports what it found. `NodeGraphTests` holds the rules and `NodeGraphGestureTests`
drives every gesture through real pointer events, since the canvas hit tests the model and a
captured pointer is the reason it has to.
