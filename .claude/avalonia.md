# Avalonia 12 notes

Researched against the 12.1.1 source, the official v12 breaking changes page and the
12.0 and 12.1 release notes. Avalonia 12 is recent and much online material still
describes 11, so prefer this file and the sources at the bottom over memory.

Anything below marked **measured** was run on this machine against 12.1.1 rather than
read. See "How these were checked" at the end.

## Baseline

- Avalonia 12.1.1 on .NET 10. Version 12 dropped .NET Framework and netstandard.
- Skia is the only rendering backend. Direct2D was removed.
- `Avalonia.Diagnostics` was removed. The replacement is `AvaloniaUI.DiagnosticsSupport`
  with `AttachDeveloperTools()`, which is a paid tier. There is still a free overlay,
  covered under Diagnostics below.
- Text shaping is no longer tied to the renderer. An app that calls `UseSkia` by hand
  must also call `UseHarfBuzz`, or startup throws "No text shaping system configured".
  `UsePlatformDetect` from `Avalonia.Desktop` calls it first, so our launcher is fine.
- Tizen and the Blazor variant of the browser backend were removed.

## Property system

Registration lives on `AvaloniaProperty`.

- `Register<TOwner, TValue>(name, defaultValue, inherits, defaultBindingMode, validate,
  coerce, enableDataValidation)` for a styled property. Only styled properties can be
  set by a style.
- `RegisterDirect<TOwner, TValue>(name, getter, setter, unsetValue, defaultBindingMode,
  enableDataValidation)` wraps a normal CLR field. Cheaper, but a style cannot set it.
  `Window.WindowState` became direct in 12, which is why it can no longer be styled.
- `RegisterAttached<THost, TTarget, TValue>` for attached properties.

**An inherited attached property is carried down the logical tree and recarried when the
tree moves**, so it is the right way to say something about a place rather than about a
control. **Measured**: a grandchild reads a value set two ancestors up, reads 0 again the
moment it is reparented under something that never set it, and follows the new parent
when that parent's own value changes later.

**What inherits from what is not what the visual tree suggests.** A template child's
inheritance parent is the control being templated, so a value set on the control reaches
into its template. But a `ContentControl`'s content is a logical child of the control
itself and not of the presenter that draws it, so a value set on `PART_ContentPresenter`
never reaches the content. **Measured** both ways. That is why `Surface` writes the level
on the panel rather than inside its template.

Declare what a property invalidates in the static constructor, or nothing repaints:
`AffectsRender<T>` on `Visual`, `AffectsMeasure<T>` and `AffectsArrange<T>` on
`Layoutable`.

`StyleKeyOverride` picks the type a control is styled as. Return the base type when a
subclass should keep the base look. `ChromelessWindow` does this so every derived
window still matches `Window` styles.

Value writes:

- `SetValue` writes a local value, which beats every style.
- `SetCurrentValue` changes the value without writing a local value, so styles and
  animations still apply later. Use it when reacting to input inside a control.
- `ClearValue` drops the local value and lets styles resolve again. **Measured**: a
  `Border` styled to width 200, assigned 50 locally, then `SetCurrentValue(75)`, then
  cleared, reads 200 again.

Properties registered with `enableDataValidation` now report errors automatically.
Remove `UpdateDataValidation` overrides that only forwarded the error.

## Styling and theming

### Precedence, measured rather than assumed

`BindingPriority` from strongest to weakest is `Animation`, `LocalValue`,
`StyleTrigger`, `Template`, `Style`, `Inherited`. Three rules decide a winner, in this
order.

**1. A selector that varies at runtime binds at `StyleTrigger` and beats one that does
not.** `StyleInstance` chooses `StyleTrigger` when the selector produced an activator
and `Style` when it did not. Classes, pseudo classes, `[Property=Value]` and nth child
all make an activator. A plain type or name selector does not.

**Measured**: a style on `Border.specific.more` set height 10, a later style on plain
`Border` set height 20. The result was 10. This is not CSS specificity. The class
selector simply lands in a stronger bucket, and adding more classes to the loser would
not change anything.

**2. Within one priority, the setters closest to the control win.** The frame order is
plain styles, then a templated parent's theme, then the control's own `ControlTheme`.

**Measured**: a `ControlTheme` for `Border` set height 33 and a `Style` set height 44.
The result was 44. So a `ControlTheme` is a default that any style overrides, which is
what makes retheming a control safe.

**3. Within one priority and one frame type, the style declared later wins.**

**Measured**: a style on `Border.a.b.c` set height 10, a later style on `Border.a` set
height 20. Both are `StyleTrigger`. The result was 20, the later one, despite the
earlier one matching three classes.

The working rule for our own themes: write base looks and state looks with selectors of
the same shape, and order them base first, states after. Mixing a plain type selector
for the base with a class selector for the state means the state wins whatever the
order, which reads as a style that cannot be overridden.

**4. A value written on a template child is not all one priority. A constant lands at
`Template` and a binding lands at `LocalValue`.**

**Measured**, the same template child against the same `:pointerover /template/` style:

```
Width="10"                                   style wins,    50
Width="{Binding $parent[X].Tag}"             template wins, 10
nothing written, style with no activator     style wins,    50
Width="10", style with no activator          template wins, 10
```

So a constant sits between `Style` and `StyleTrigger`: an activated style beats it, a
plain one does not. A binding sits at the top and **nothing a style can write will ever
beat it**, which fails silently and looks like a pseudo class that is not being set.

The rule that follows: **a template child a state has to move cannot be bound in the
template.** Put its resting value in a `/template/` style with no activator and let the
states carry one. `Themes/Controls/SplitView.axaml` is the use, and the reason its pane
opened to the wrong width until this was measured.

### A ControlTheme may not hold a descendant selector

A nested style inside a `ControlTheme` can only reach the control itself and, through
`/template/`, its template children. A child `>` or descendant selector throws
`InvalidOperationException`, "ControlTheme style may not directly contain a child or
descendent selector".

**It throws the first time the theme is resolved, not at build**, so a theme that is never
shown looks fine until something opens it.

This matters more than it sounds, because **content is not a template child**. Anything
materialised into a presenter, such as `InnerLeftContent` on a `TextBox` or the content of
a `ContentControl`, is outside the template name scope: `/template/` does not match it,
`e.NameScope.Find` does not find it, and the descendant selector that would reach it is
the one thing a control theme cannot have.

So a control theme that puts furniture in inner content has to style it from a plain
`Style` outside the theme, and wire it by listening rather than by looking it up. A click
bubbles, which is how `SearchBox` hears its clear button.
`Themes/Controls/TextBox.axaml` is the use.

### Style versus ControlTheme

A `Style` matches by selector and layers on top. A `ControlTheme` replaces a control's
whole look and is keyed by type. Use `ControlTheme` to redefine a control, `Style` to
adjust one. `BasedOn` inherits another theme.

### Selectors

The grammar supports type, `Is(Type)`, name, class, pseudo class, attached property,
`[Property=Value]`, child `>`, descendant, `/template/`, `:not()`, `:nth-child()`,
`:nth-last-child()`, comma for a list, and `^` for nesting inside a `ControlTheme`.

**An attached property in a selector is written with a pipe, and only one fits per
selector.** `[(ui|Surface.Level)=1]`, never `ui:` and never without the parentheses. Both
of the other forms are rejected at build.

**Measured**: two of them chained, `[(a)=1][(b)=True]`, fails with "Expected an
identifier, got '['". The runtime loader parses a single one fine, so this is a limit of
the compiled grammar rather than of selectors. Nest the second inside the first with `^`
instead, which a plain `Style` supports as well as a `ControlTheme` does.
`Themes/Surfaces.axaml` is the use.

A selector matches a value that arrived by inheritance, and lets go when it changes.
**Measured** on an inherited attached property set two ancestors up.

### Container queries

Styling can react to the size of an ancestor rather than the window. Mark the ancestor,
then wrap styles in a `ContainerQuery`.

```xml
xmlns:styling="clr-namespace:Avalonia.Styling;assembly=Avalonia.Base"

<Panel styling:Container.Name="shell" styling:Container.Sizing="WidthAndHeight">
```

```xml
<ContainerQuery Name="shell" Query="max-width:400">
  <Style Selector="Border.probe">
    <Setter Property="Height" Value="11" />
  </Style>
</ContainerQuery>
```

Keywords are `width`, `height`, `min-width`, `max-width`, `min-height` and
`max-height`, joined with `and` or `or`. `Container.Sizing` must name the axis being
queried, and defaults to `Normal`, which answers nothing.

**Measured**: with the query above, a window resized from 600 to 300 and back changed
the styled height on every layout pass. This is the right tool for a docked panel that
must relayout when it is narrow, rather than binding to `Bounds` in a view model.

### Resources

Styles belong in `Application.Styles` or `Control.Styles`. Resources belong in
`Application.Resources`. A `Styles` file is included with `StyleInclude` and a
`ResourceDictionary` with `ResourceInclude`. Swapping the two fails at runtime, not at
build.

Use `DynamicResource` inside control templates so a theme change is picked up.
`StaticResource` resolves once.

Theme variants are `ThemeVariant.Default`, `Light` and `Dark`, with
`RequestedThemeVariant` and the read only `ActualThemeVariant`. `Default` on the
application means follow the system. A custom variant can name another variant to
inherit from when a key is missing. `ThemeVariantScope` applies a variant to a subtree.

`ResourcesChangedEventArgs` is a struct in 12. `Empty` is gone, use
`ResourcesChangedEventArgs.Create()`.

## Window chrome

This is the area that changed most, and the area where version 11 advice is wrong.

Removed in 12: `ExtendClientAreaChromeHints`, `TitleBar`, `CaptionButtons`,
`ChromeOverlayLayer`. Renamed: `SystemDecorations` is now `WindowDecorations`, with
values `None`, `BorderOnly` and `Full`.

There are two supported ways to draw your own frame.

**1. Tag your own elements with roles.** Set `ExtendClientAreaToDecorationsHint="True"`,
then mark elements with the `WindowDecorationProperties.ElementRole` attached property.
Roles are `TitleBar`, `ResizeN` through `ResizeSW`, `CloseButton`, `MinimizeButton`,
`MaximizeButton`, `FullScreenButton`, `DecorationsElement` and `User`. The platform
then treats those elements as frame, so dragging, resizing, snapping and the system
window menu are native. The role has no effect unless
`ExtendClientAreaToDecorationsHint` is true.

**2. Retheme `WindowDrawnDecorations`.** A control that supplies the whole frame.
Give it a `ControlTheme` built from `WindowDrawnDecorationsTemplate` and
`WindowDrawnDecorationsContent`, which has `Underlay`, `Overlay` and `Popover` layers.
It exposes `DefaultTitleBarHeight`, `DefaultFrameThickness` and
`DefaultShadowThickness`, the parts `PART_TitleBar`, `PART_CloseButton`,
`PART_MinimizeButton` and `PART_MaximizeButton`, and the pseudo classes `:normal`,
`:maximized`, `:fullscreen`, `:has-shadow`, `:has-border`, `:has-titlebar`,
`:has-minimize` and `:has-maximize`. Avalonia's own Fluent theme for it is the best
worked example.

Other notes:

- On Windows `ExtendClientAreaToDecorationsHint` was fixed in 12. The old margin
  workarounds people applied when maximized should be removed.
- `WindowState` is now a direct property, so it cannot be set from a style. Selectors
  such as `[WindowState=Maximized]` still read it.
- Windows 12.1 added `Win32Properties.WindowCornerPreference` for Windows 11 corners.

### There is no usable window shadow API

Version 12 looks like it grew one. `WindowDrawnDecorations` carries
`DefaultShadowThickness`, `ShadowThickness` and a `:has-shadow` pseudo class, and
`Window` calls `IWindowImpl.SetShadowExtents` so the platform can tell content from
shadow. None of it is reachable from a desktop app that draws its own frame, for three
separate reasons, each of which is enough on its own.

1. `IWindowImpl.SetShadowExtents` is a default interface method with an empty body.
   **Only `Avalonia.Wayland` implements it.** Neither X11 nor Win32 overrides it, so on
   either of them the platform is never told anything.
2. Drawn decorations only exist when `IWindowImpl.NeedsManagedDecorations` is true. On
   X11 that is `_extendingClientAreaToDecorations || ForceDrawnDecorations`, and the
   first can only be set through the experimental gate described below.
3. `Window.ComputeDecorationParts` returns no parts at all, shadow included, when
   `WindowDecorations == None`. An app drawing its own frame sets exactly that.

So a window shadow is an app drawn effect, the same as it was in version 11. This is
what SourceGit does, and it is worth copying rather than deriving:

- The window is larger than its visible frame by a transparent gutter, carried as the
  window's `Padding`.
- The frame sits in a wrapper with `Margin="{TemplateBinding Padding}"` and
  `Effect="drop-shadow(0 0 12 #60000000)"`. An `Effect` blurs what is actually drawn,
  where a `BoxShadow` draws a shadow of the border's own shape, which can show an edge
  of its own.
- **The shadow has no offset and its blur equals the gutter.** That is the whole trick.
  A symmetric shadow reaches every window edge at exactly the point it fades out. Give
  it an offset and the far side runs past the window edge, where the platform clips it,
  and a clipped gaussian reads as a hard line rather than a soft edge.
- Maximized sets `Padding` to 0 and `CornerRadius` to 0, and hides the resize grips with
  `IsHitTestVisible="False"` as well as `IsVisible="False"`. The effect stays, since with
  no gutter it falls outside the window and is clipped away entirely.
- Resize grips are the same thickness as the gutter, so the whole soft edge is the
  resize target.

Measured on 12.1.1, a 940 by 700 frame with a 12px gutter:

```
normal     client=964,724   padding=12  frame=940x700
maximized  client=5120,1400 padding=0   frame=5120x1400
restored   client=964,724   padding=12  frame=940x700
```

### Roles do not work on Linux yet

Measured on 12.1.1 on this machine, with the hint set from the window constructor:

```
hint=True titleBarHint=-1 decorations=Full extended=False   backend=XID
hint=True titleBarHint=-1 decorations=Full extended=False   backend=Wayland
```

`IsExtendedIntoWindowDecorations` stays false, so the roles never take effect.

`X11Window.SetExtendClientAreaToDecorationsHint` returns immediately unless
`X11PlatformOptions.EnableDrawnDecorationsInternal` is true, which needs either
`EnableDrawnDecorations`, marked `[Experimental("AVALONIA_X11_CSD")]` with the message
"Experimental, used mostly for testing", or `ForceDrawnDecorations`, which is
experimental too and takes the choice away from the app entirely. When enabled, X11
maps the roles onto `_NET_WM_MOVERESIZE`, which is the real native move and resize
protocol, so the design is right and only the gate is closed.

`WaylandPlatformOptions` has `ForceDrawnDecorations` as well, but it means something
different. It suppresses `zxdg_decoration_manager_v1` so the compositor never offers
server side decorations, and it exists to test the client drawn path on compositors
that would otherwise force server side ones. There is no per window opt in on Wayland.

So on Linux, custom chrome still needs `WindowDecorations="None"` with `BeginMoveDrag`
and `BeginResizeDrag`, which is what our `ChromelessWindow` does. The launcher keeps
its `ElementRole` tags anyway: they cost one attribute each, they document intent, and
they become live if the option is ever enabled or on a platform that honors the hint.

## Layout

`MeasureOverride(Size availableSize)` returns the size wanted. `ArrangeOverride(Size
finalSize)` places children and returns the size used. Call `InvalidateMeasure` or
`InvalidateArrange` when something outside the property system changes, or declare the
property with `AffectsMeasure` and `AffectsArrange` instead.

`UpdateLayout()` on any `Layoutable` runs a synchronous layout pass. In 12 the layout
root interfaces are no longer public, so reaching for a `LayoutManager` does not
compile. Use `UpdateLayout`.

`EffectiveViewportChanged` reports the part of a control actually visible through its
scroll parents. That is the hook for loading content only when it scrolls into view.

`UseLayoutRounding` is on by default and snaps layout to whole device pixels. Turn it
off on a control only when a fractional position is deliberate.

## Items controls and virtualization

`ItemsSource` is the collection, `Items` is the direct collection, and setting both
throws. `ItemsSourceView` is the wrapper the framework reads through.

Containers are observed through events rather than a generator:
`PreparingContainer`, `ContainerPrepared`, `ContainerIndexChanged` and
`ContainerClearing`, plus `ContainerFromIndex` and `IndexFromContainer`. Override
`CreateContainerForItemOverride`, `NeedsContainerOverride` and
`PrepareContainerForItemOverride` on a custom items control.

Containers are recycled, so anything set in `ContainerPrepared` must be reset there
too, never only on first use. `ContainerIndexChanged` fires when a recycled container
is reused at a different index.

`VirtualizingStackPanel` is the default panel for `ListBox`. Writing another
virtualizing panel means deriving from `VirtualizingPanel` and implementing
`GetControl` for keyboard navigation.

**Only `ListBox` virtualizes.** Checked against the 12.1.1 source. `ItemsControl`
defaults to a plain `StackPanel`, and `TreeView` does not override the panel either, so
it inherits that same one and so does every `TreeViewItem` for its children. There is no
virtualizing tree panel in the box, so a tree of ten thousand nodes realizes ten thousand
controls. Flatten the expanded nodes into a list and virtualize that instead.

**Overriding the container hooks needs `protected`, not `protected internal`.** They are
declared `protected internal` in Avalonia, which from another assembly means `protected`,
and C# refuses an override that says otherwise. `ContainerIndexChangedOverride` really is
`protected`. Measured: all five refused to compile as `protected internal`.

**A selection made inside an `OnKeyDown` override is put back if `base` then runs.**
Measured on a `ListBox` subclass: setting `SelectedIndex` in the override took, and by the
time the press returned the index was whatever the focused container had been, so left and
right appeared to do nothing at all. `e.Handled = true` is not enough on its own. Return
without calling `base` once the key has been answered.

Selection and focus travel together when a key moves the selection. Setting `SelectedIndex`
alone leaves focus on the old container, so follow it with `ContainerFromIndex(index)
?.Focus(NavigationMethod.Directional)`, after a layout pass so the container exists.

Selection in 12 changed: touch and pen select on release rather than press.
`UpdateSelection` and `UpdateSelectionFromEventSource` are obsolete. Override
`ShouldTriggerSelection` and `UpdateSelectionFromEvent`, and use the helpers in
`ItemSelectionEventTriggers`.

## ToggleSwitch names its knob parts backwards

`PART_SwitchKnob` is **the area the knob travels in** and `PART_MovingKnobs` is **the thing
that moves**. Read the other way round, which is the natural reading, the knob never moves
and nothing is logged.

Both must be a `Panel`. Give either one a `Border` and the control cannot find it, so the
switch renders and silently stays put.

**Measured** on 12.1.1, against Fluent and then against our own theme: the control writes
`Canvas.Left` on `PART_MovingKnobs`, and **the travel it writes is the width of
`PART_SwitchKnob`**. So the travel is set by sizing that element, not by the track:

```
track 30 wide, PART_SwitchKnob 13 wide, knob 11
off  moving left=0   knob at 3..14
on   moving left=13  knob at 16..27
```

`KnobTransitions` is where the movement is timed, and it transitions `Canvas.Left`. That
is the supported way and it avoids the transform animation trap above entirely.

## Border cannot draw a dashed edge

`Border` has `BorderBrush` and `BorderThickness` and nothing else. There is no
`BorderDashArray` on it in 12.1.1. A dashed outline is a `Rectangle` with `Stroke`,
`StrokeThickness` and `StrokeDashArray`, sized by the panel it sits in, and its corners
are `RadiusX` and `RadiusY`, which are two doubles rather than a `CornerRadius`.

## Controls added in 12

Worth knowing before hand building something that already ships.

- `TableView`, a read only tabular control built on `ListBox` with configurable
  columns. Added in 12.1. It is not an editable grid, so it does not replace
  TreeDataGrid for editing, but it covers a plain columnar list.
- `GroupBox`, a `HeaderedContentControl`.
- `CommandBar` with `CommandBarButton`, `CommandBarToggleButton`, `CommandBarSeparator`
  and overflow handling.
- `PipsPager`.
- A page navigation stack under `Page`, with `NavigationPage`, `TabbedPage`,
  `CarouselPage`, `DrawerPage`, `INavigation` and navigation lifecycle events. Aimed at
  mobile shells more than at a desktop tool.

`HyperlinkButton` is often listed with these but arrived in 11.1, so it is available in
any version this project would use.

## Popups and overlays

`Popup` either opens a real platform window or renders into the parent window's overlay
layer. `ShouldUseOverlayLayer` requests the overlay, `IsUsingOverlayLayer` reports what
actually happened, and `X11PlatformOptions.OverlayPopups` forces it for the backend.

**Measured** on X11 with defaults: `ShouldUseOverlayLayer` false and
`IsUsingOverlayLayer` false, so a popup is a separate OS window whose `TopLevel` is a
`PopupRoot`. That is why popup corners, transparency and shadows have to be solved
again inside the popup rather than inherited from the window.

The same probe confirms **`TopLevel` is not the visual root**. Walking visual ancestors
from the popup content ends at `TopLevelHost`, while `TopLevel.GetTopLevel` returns the
`PopupRoot`. Use `TopLevel.GetTopLevel(visual)`. The `GetVisualRoot()` extension is
gone, and `IPopupHostProvider` is internal now, so version 11 code that reached for the
popup host does not compile.

Placement is `Placement`, `PlacementTarget`, `PlacementAnchor`, `PlacementGravity`,
`PlacementRect`, `HorizontalOffset`, `VerticalOffset` and
`PlacementConstraintAdjustment` for what happens at a screen edge.
`IsLightDismissEnabled`, `OverlayDismissEventPassThrough` and
`OverlayInputPassThroughElement` control dismissal.

Overlay layers on a `TopLevel`: `OverlayLayer` for adorner style content,
`LightDismissOverlayLayer`, `PopupOverlayLayer` and `AdornerLayer`. A docking drop
indicator belongs in one of these rather than in the page.

## Animations and transitions

An animation targets a property through an animator chosen by the property's value
type. There is no animator for `ITransform`.

**Measured**: a keyframe animation on `Visual.RenderTransformProperty` throws
`InvalidOperationException`, "No animator registered for the property RenderTransform".
This is a startup crash, not a warning. Animate a child property of a transform
instead, such as `TranslateTransform.Y`, `ScaleTransform.ScaleX` or
`RotateTransform.Angle`, and give the control a matching `RenderTransform` instance.

Worse than the crash, the quiet case:

**Measured**: when `RenderTransform` holds a `TransformOperations` value, which is what
the CSS style string `translateY(10px)` produces, a keyframe animation on
`TranslateTransform.Y` does nothing at all. The matrix stayed at identity and the task
returned by `RunAsync` never completed. `TransformAnimator` returns an empty
subscription for that case on purpose. Nothing is logged.

**Measured**: a `TransformOperationsTransition` on the same property does work. Setting
`translateY(0px)` then `translateY(40px)` moved through 13.59 and settled on 40.

So the rule is: `TransformOperations` is for transitions, explicit `Transform` objects
are for keyframe animations, and mixing them fails silently.

Animators exist for bool, the integer types, float, double, decimal, `Color`, `IBrush`,
`BoxShadow`, `BoxShadows`, `CornerRadius`, `Point`, `Rect`, `RelativePoint`,
`RelativeScalar`, `Size`, `Thickness`, `Vector` and `IEffect`. Register another with
`Animation.RegisterCustomAnimator<T, TAnimator>`.

**`ContentControl` clips to its bounds by default, so anything a control theme draws
outside its frame is cut off.**

**Measured**, with no theme loaded at all, so these are the types' own defaults:
`ContentControl`, `Button`, `SplitButton`, `DropDownButton`, `ToggleButton`,
`ProgressBar` and `TextBox` all report `ClipToBounds` true. `Border` reports false.

This is the trap for a focus ring, a glow or any halo that hugs a control from outside.
The element arranges correctly at a negative margin and is then clipped flush with the
border, which reads as a hard ring rather than a soft one, and nothing is logged. Turn
the clip off on the control and put it on the frame inside the template instead, so
content still cannot escape while the halo can. `Themes/Controls/Button.axaml` is the
use.

**`RelativePoint` is the way to move something in proportion to its parent.** A
translation is measured in pixels, so anything that has to travel a share of a width
normally needs the width, which means code. `RenderTransformOrigin` is a `RelativePoint`
and it does interpolate.

**Measured**: an animation from `0%,50%` to `100%,50%` over four seconds read 0.123,
0.248, 0.373, 0.497, 0.623 and 0.748 at half second samples, so it moves smoothly
rather than stepping between keyframes.

That gives a band of fixed proportion sweeping a container with no measurement anywhere.
Stretch the element, scale it by `s`, and move the origin `o` from 0 to 1. The element
then covers `[(1-s)*o, (1-s)*o + s]` of the container, so at `s` of 0.3 a band 30 percent
wide walks from one edge to the other. `Themes/Controls/ProgressBar.axaml` is the use.

New in 12: a style applied animation stops ticking while its control is not effectively
visible. `Animation.PlaybackBehavior` is `Auto` by default, and `Always` restores the
old behavior. Animations started by hand through `RunAsync`, and animations targeting
`IsVisible`, always play.

## Input

- Gesture attached events moved from `Gestures` to `InputElement`, and `Gestures` is no
  longer public. In XAML write `Tapped`, `Pinch` and so on with no prefix.
- `GotFocus` and `LostFocus` carry `FocusChangedEventArgs`. There is also
  `FocusChangingEventArgs`, with `TryCancel` and `TrySetNewFocusedElement`, so focus
  moves can be redirected rather than only observed.
- `KeyboardNavigationHandler` is gone. `FocusManager` does it all, with `Focus`,
  `TryMoveFocus`, `FindNextElement`, `FindFirstFocusableElement` and
  `FindLastFocusableElement`. `FindNextElementOptions` adds `SearchRoot`,
  `ExclusionRect` and `FocusHintRectangle` for directional navigation.
- `NavigationDirection` covers `Next`, `Previous`, `First`, `Last`, the four arrows and
  the two page keys.
- Access keys are matched on the printed symbol rather than the virtual key, so accented
  characters and digits work. `AccessText.AccessKey` changed from `char` to `string?`.
- Clipboard and drag and drop were rewritten. `IDataObject` is gone. `DataObject`
  became `DataTransfer`, `DataFormats` became `DataFormat`, `DragDrop.DoDragDrop` became
  `DoDragDropAsync`, and `DragEventArgs.Data` became `DragEventArgs.DataTransfer`.
  `BinaryFormatter` is no longer used on Windows, so custom payloads need their own
  serialization. X11 gained XDND support in 12.1.

## Bindings

- Compiled bindings are on by default in 12, so `x:DataType` is required on views and
  on every `DataTemplate`.
- `IBinding` was removed. Everything derives from `BindingBase`. `Binding` now always
  means `ReflectionBinding`. In code prefer `CompiledBinding.Create`, which accepts a
  LINQ expression.
- `InstancedBinding` was removed. The equivalent is `BindingExpressionBase`.
- Binding plugins are no longer configurable, and the data annotations plugin is off by
  default. Validation attributes do nothing unless validation is wired another way.
- `FuncMultiValueConverter` takes `IReadOnlyList<TIn>` rather than `IEnumerable<TIn>`.

## Text

- `TextBlock.LetterSpacing` moved to `TextElement.LetterSpacing` and is an inherited
  attached property. Writing it on a `TextBlock` in XAML still works.
- `RenderOptions.TextRenderingMode` moved to `TextOptions.TextRenderingMode`, which
  also carries `TextHintingMode` and `BaselinePixelAlignment`.
- `TextBox.Watermark` is now `PlaceholderText`, with `PlaceholderForeground` for its
  colour. The old name is obsolete but still present.
- Type 1 fonts are no longer supported. TrueType and OpenType only.

### Trimming already knows about paths

`TextTrimming` has `None`, `CharacterEllipsis`, `WordEllipsis`,
`PrefixCharacterEllipsis`, `LeadingCharacterEllipsis` and `PathSegmentEllipsis`.

**Measured**, one path at shrinking widths:

```
400 -> /home/jason/Projects/godot/.../godot/scenes/machines/moldurr.tscn
220 -> /home/jason/.../machines/moldurr.tscn
140 -> /home/.../moldurr.tscn
 80 -> ...oldurr.tscn
```

It drops whole segments from the middle and keeps the last one, which is what our
`PathShortener` does by hand. The difference is that `TextTrimming` works in rendered
width rather than character count, and knows nothing about a home directory. The split
worth keeping in mind: the shortener decides what the path means, such as writing the
home directory as a tilde, and `TextTrimming` decides what fits.

The default ellipsis is the single character form. `TextPathSegmentTrimming` takes a
custom ellipsis string in its constructor, so plain dots can be used instead.

## Threading

Avalonia 12 supports one dispatcher per thread. Library and control code should use
`AvaloniaObject.Dispatcher` or `Dispatcher.CurrentDispatcher` rather than
`Dispatcher.UIThread`, which now means one particular thread rather than the only one.

`Dispatcher.InvokeAsync` captures the execution context, so `AsyncLocal` and culture
flow from the caller. There is also `Dispatcher.FromThread` and a `TaskScheduler`
conversion.

Priorities that matter in practice, weakest first: `SystemIdle`, `ApplicationIdle`,
`ContextIdle`, `Background`, `Input`, `Default`, `Loaded`, `Render`, `Normal`, `Send`.
`Loaded` runs after layout and render but before input, which is the right place for
work that needs a measured tree. Posting at `Loaded` from the `Opened` handler is how
the probes here read real sizes.

## Storage provider

`TopLevel.StorageProvider` gives `OpenFilePickerAsync`, `SaveFilePickerAsync`,
`OpenFolderPickerAsync` and bookmarks. 12.1 added `OpenFileWithResultAsync`.

On Linux the provider is a `FallbackStorageProvider` that tries three things in order,
which fits our rule about probing rather than naming one thing:

1. The xdg desktop portal `org.freedesktop.portal.FileChooser` over DBus, when
   `X11PlatformOptions.UseDBusFilePicker` is true, which is the default. **Folder
   picking needs portal version 3 or later**, since `CanPickFolder` is
   `version >= 3`. This is the path that works under Flatpak and Snap.
2. The GTK dialog, when GTK is present.
3. `ManagedStorageProvider`, Avalonia's own dialog drawn in managed code, which always
   works and looks like Avalonia rather than the desktop.

So a folder picker is available on any distribution, but which one appears depends on
what is installed. Do not assume the native dialog.

## Diagnostics without DevTools

`Avalonia.Diagnostics` is gone, but `TopLevel.RendererDiagnostics.DebugOverlays` is
free and takes `RendererDebugOverlays.Fps`, `DirtyRects`, `LayoutTimeGraph` and
`RenderTimeGraph`.

**Measured**: settable at runtime, no extra package.

Bind it to a debug only setting rather than a keyboard shortcut nobody remembers.

## Accessibility

`Avalonia.FreeDesktop.AtSpi` is a real AT-SPI2 backend and `AvaloniaX11Platform`
constructs it during initialization, so it needs no opt in. `AutomationProperties.Name`
on our caption buttons is read by a Linux screen reader, not just by test tooling.
Windows automation was improved in 12.1 as well.

## Platform backends

- `UsePlatformDetect` selects X11 on Linux, and loads HarfBuzz and Skia.
  `Avalonia.Wayland` 12.1.1 is a separate package enabled with `UseWayland`.
- This machine is a Wayland session, so the app runs through XWayland today.
- `X11PlatformOptions.WmClass` sets the WM_CLASS the window reports. A Linux desktop
  matches that string against the `.desktop` file name to attach the icon and group
  windows in the task bar. Packaging work will need it to match whatever the desktop
  entry is called.
- Other X11 options worth knowing: `OverlayPopups`, `UseDBusFilePicker`, `UseDBusMenu`,
  `EnableIme`, `EnableSessionManagement`, `RenderingMode` and `GlxRendererBlacklist`.
- Screens: `Screen` is abstract in 12. Get instances from `Screens.All`,
  `Screens.Primary` or `Screens.ScreenFromWindow`. **Measured** here: one screen,
  7680 by 2160, work area 60 pixels shorter, scaling 1.5.

## Hit testing

A `Border` with no `Background` is not hit tested. Anything clickable needs a
background even when it paints nothing, so give it `Transparent`. A control whose only
background comes from a `:pointerover` setter can never be hovered, because the pointer
never reaches it in the resting state.

This bit the launcher: the caption buttons only responded on the few pixels their glyph
covered, and clicks elsewhere in the button fell through to the title bar and started a
window drag. Verify with `this.InputHitTest(point)`, but run it after layout. At
`Opened` the tree is not measured yet and every hit returns null.

`ICustomHitTest` lets a control answer for itself, taking a point in global coordinates.
That is the way to make a shaped control, such as a drag handle, claim only its own
area.

## What a title bar has to do

Taken from GTK's own client drawn title bar, `gtk/gtkwindowhandle.c` and
`gtk/gtkwindowcontrols.c`, which is the reference implementation, plus the GNOME
defaults in `org.gnome.desktop.wm.preferences`.

A title bar handles exactly three configurable gestures, each mapped to an action of
`none`, `toggle-maximize`, `lower`, `minimize` or `menu`:

| Gesture | GNOME default |
|---|---|
| Primary double click | `toggle-maximize` |
| Middle click | `none` |
| Right click | `menu` |

So middle click doing nothing is correct. Right click normally opens a window menu,
which GTK asks the compositor for and otherwise builds itself. **Workbench leaves
right click unhandled on purpose**, so do not add a window menu back as a fix.

A primary press starts a move drag, and a press with more than one click cancels that
drag so it cannot fight the double click.

The caption buttons are real buttons. They carry tooltips and accessible labels, the
maximize button swaps its glyph and tooltip to restore while maximized, and it is not
created at all when the window cannot resize.

Avalonia has no API for `lower` or for the system window menu, so the menu is built in
the app, which is the same fallback path GTK takes.

## Wayland backend maturity

`Avalonia.Wayland` 12.1.1 inflates window height. Measured with the same build, a
window asking for 940 by 700:

```
X11       clientSize=940, 700
Wayland   clientSize=940, 728
```

X11 is exact. The backend was tried and removed from this project. Treat it as young
if it is ever revisited.

## Testing

- `Avalonia.Headless` 12.1.1, with `Avalonia.Headless.XUnit` or `Avalonia.Headless.NUnit`.
  Version 12 moved to xUnit v3 and NUnit 4.
- `Avalonia.Headless.Vnc` runs a headless app that can be viewed over VNC.
- Headless runs with no display, so it is the right way to check UI behavior here.
  Screenshots of a live desktop are not, because they capture whatever else is open.

**Headless can draw, and that is how a page is looked at.** `UseSkia()` with
`UseHeadlessDrawing = false` renders for real, and `window.CaptureRenderedFrame()` hands
back a bitmap to save. Nothing appears on a display and nothing is captured that was not
asked for.

Input goes with it. `MouseMove`, `MouseDown`, `MouseUp` and `KeyPress` on the window
drive the app with a pointer of its own, so hover, press and a full drag can be checked
without touching the machine's real pointer. **Never drive a live window with xdotool.**
It moves the person's cursor, it clicks whatever is actually under it, and it reads
positions in device pixels while the app works in the scaled ones.

A scroll offset is set rather than scrolled to: find the `ScrollViewer`, assign
`Offset`, call `UpdateLayout`, capture. That is how the gallery pages in this project are
read.

Three things about that, all measured while shooting a long page:

- **One jump lands short.** The scroller clamps to the extent it knows about, and the
  part of the page below has not been measured yet, so the offset asked for is cut down.
  Set it, lay out, read where the target actually is and set it again until it arrives.
- **Measure against the viewport, not the page.** Translating a point into the scrolled
  content gives a number in a space that moves with the offset. Translating into the
  `ScrollViewer` gives the distance from the top of what is on screen, which is the thing
  worth adding to the offset already set.
- **The first captured frame is the layout before the first scroll.** Every later capture
  is current, so throw the first picture away rather than trusting it. It cost an hour
  reading a screenshot of the wrong part of a page as a bug in the part being built.

**Headless input carries no click count**, so a double tap cannot be produced by clicking
twice. Raise the gesture instead:
`control.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!) { Source =
control })`. The last pointer arguments may be null when nothing reads them.

## Resources and fonts

- Assets are addressed as `avares://AssemblyName/Path`.
- Embedded fonts are `AvaloniaResource` items, referenced as
  `avares://AssemblyName/Folder#Family Name`.
- When embedding separate weight files, check that every file reports the same family
  name, otherwise weight selection silently fails. `fc-scan` shows the family a file
  reports.

## Traps already hit in this project

- `ExtendClientAreaChromeHints` does not exist. Build error, easy to spot.
- `TextOptions.TextRenderingMode` is not settable the way version 11 set it.
- A runtime identifier cannot be passed to a solution, only to a project.
- An undecorated window was once sized to the whole screen by the compositor. Verify
  window geometry by logging `ClientSize` rather than trusting a screenshot.
- Animating `RenderTransform` crashes at startup. See Animations above.
- A `Border` with no background is invisible to the pointer. See Hit testing above.
- `PathIcon`'s stock template stretches geometry to fill, which discards the viewBox and
  scales an icon by whatever its ink happens to measure. Wrap the `Path` in a fixed size
  `Canvas` inside a `Viewbox` in a `ControlTheme`.
- `ClipToBounds` on the same `Border` that draws the stroke clips children to the outer
  rounded rectangle, so the corner reads as two colours. Use two borders, one for the
  stroke and one for the clip, the way the window frame does.

## How these were checked

The measured claims came from a throwaway Avalonia 12.1.1 app that opens a window,
posts its checks at `DispatcherPriority.Loaded` so layout has run, writes one line per
result to stdout and shuts itself down. Reading the source says what should happen.
Running it says what does.

Worth repeating that shape for anything uncertain. It costs a few minutes, it needs no
test framework, and it never captures anything else on the desktop.

## Sources

- Breaking changes: https://docs.avaloniaui.net/docs/avalonia12-breaking-changes
- Release notes: https://github.com/AvaloniaUI/Avalonia/releases
- Source at tag 12.1.1, which is the authority when the docs are thin. The parts read
  for this file were `Avalonia.Base/PropertyStore`, `Avalonia.Base/Styling`,
  `Avalonia.Base/Animation`, `Avalonia.Controls/Primitives/Popup.cs`,
  `Avalonia.X11` and `Avalonia.FreeDesktop`.
- Fluent decorations theme: `src/Avalonia.Themes.Fluent/Controls/WindowDrawnDecorations.xaml`
- SourceGit, a well built Avalonia app, though its chrome targets version 11:
  https://github.com/sourcegit-scm/sourcegit
