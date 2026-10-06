# Accessibility

DrawnUI draws controls into a Skia surface. It does not create native controls, so screen readers and keyboard navigation need a parallel, virtual description of what is on screen. DrawnUI keeps that description for you and gives it to the accessibility system of each platform.

Plain drawn controls are not exposed by default, so you choose which parts of a drawn UI a screen reader sees and how. The ready-made controls that act like native ones are exposed out of the box (see [Default roles](#default-roles)).

## Current Support

| Framework / target | Screen readers | How | Keyboard |
|---|---|---|---|
| .NET MAUI Windows | Narrator, NVDA | UI Automation virtual peers on the canvas | [Keyboard navigation](#keyboard-navigation), focus ring drawn on the canvas |
| WPF | Narrator, NVDA | WPF `AutomationPeer`s: the canvas is a pane, every node a virtual peer | Keyboard navigation, focus ring on the canvas |
| OpenTK Windows | Narrator, NVDA | UI Automation virtual providers on the native window | Keyboard navigation, focus ring on the canvas |
| OpenTK Linux | Orca | AT-SPI2 over D-Bus, on the native window | Keyboard navigation, focus ring on the canvas |
| .NET MAUI Android | TalkBack | Virtual views through `ExploreByTouchHelper` on the canvas view | No |
| .NET MAUI iOS | VoiceOver | Virtual `UIAccessibilityElement` container on the canvas view | No |
| .NET MAUI Mac Catalyst | VoiceOver | Virtual `UIAccessibilityElement` container on the canvas view | Keyboard navigation, focus ring on the canvas |
| Blazor | Any browser screen reader | Invisible ARIA overlay over the canvas | Keyboard navigation through the overlay (CSS focus outline) |
| WebAssembly (`DrawnUi.Web`) | Any browser screen reader | The same ARIA overlay as Blazor | Keyboard navigation through the overlay, focus ring on the canvas |

Every target uses the same C# metadata on `SkiaControl`. A platform layer reads the `SkiaAccessibilityManager` snapshot and exposes it through that platform's accessibility API, so a control behaves the same for a screen reader on every target.

## Shared Model

Accessibility starts in shared code. A drawn control can expose:

- a role
- a label (its name)
- a hint
- whether it takes input
- a pressed / toggle state
- a value, for range controls such as sliders and progress bars
- a live region setting, for text that changes

`SkiaAccessibilityManager` collects that metadata into a snapshot of accessible nodes and where they are on screen.

Every `SkiaControl` implements `ISkiaAccessibilityNode`, so platform layers work against the interface instead of the concrete class, the same way gestures work against `ISkiaGestureListener`.

The snapshot is an array of:

```csharp
public record AccessibilityNode(
    string? Label, string? Hint, string? Role,
    SKRect Rect, bool CanInteract, bool? IsPressed, string? Live = null)
{
    public int Id { get; }                       // stable id of the control
    public AccessibilityValue? Value { get; }    // range controls only
    public bool NamedByChild { get; }            // a title text inside says the name
}

public readonly record struct AccessibilityValue(
    double Now, double Min, double Max, double Step, string? Text = null, bool Vertical = false);
```

`Rect` is in device-independent pixels, where the control is on screen now. This includes controls inside cached containers and scrolled content, and the translation, rotation and scale of the control and its parents (a panel slid in with `TranslationX` reports where it is shown).

The array is in reading order: rows top to bottom, each row left to right. Controls whose tops are within half the smaller height share a row, so a row of vertically centered controls of different heights still reads left to right.

Only controls the last frame drew are in the snapshot, live or inside a cached parent that is blitted. A control a virtualized layout stopped drawing (scrolled out, a recycled cell back in the pool) leaves the snapshot until it is drawn again.

### Names said once

A card often has its own label and a title text inside it with the same words. A screen reader would read the name twice. When a node's text or heading child repeats the node's label, the node gets no name (`NamedByChild`, `Label` is null) and the title text says it. Selectable text always keeps its name.

### Controls that take no input

A node with a control role (button, checkbox, switch, slider, text field...) that cannot take input right now reads as unavailable ("dimmed", "unavailable", "disabled") on every target. See [Only controls the pointer can use](#keyboard-navigation).

### Registration lifecycle

- `OnLayoutReady()` fires once on the first valid layout and registers the control automatically when `IsAccessibilityElement` is true.
- `NotifyAccessibility()` registers on the first call and marks the snapshot dirty afterwards. Call it when you change accessibility props at runtime.
- Detaching a control from the tree or disposing it unregisters it together with all its registered descendants.

The manager rebuilds its snapshot at most once per `MinUpdateIntervalMs` (default 1000 ms) at the end of a drawn frame. It stays cheap at high frame rates and still follows scrolling and animations. It raises `Changed` only when the snapshot differs, and costs nothing while no control is registered.

Two cases do not wait for the interval:

- **After a screen reader's action** (activate, adjust, set a value), the snapshot is rebuilt on the next frame, so the reader hears the new value or state at once.
- **A live region** (`AccessibilityLive`) reports its new text immediately.

When the node the screen reader is on leaves the snapshot (its page closed, a popup went away), the reader is moved to the first node in reading order that says something (has a name or takes input), so it never stays on an empty spot.

### Roles

Use the constants from `DrawnUi.Models.Aria` instead of raw strings: `RoleButton`, `RoleLink`, `RoleCheckbox`, `RoleRadio`, `RoleSwitch`, `RoleSlider`, `RoleSpinButton`, `RoleTextBox`, `RoleSearchBox`, `RoleComboBox`, `RoleListBox`, `RoleOption`, `RoleTab`, `RoleTabList`, `RoleTabPanel`, `RoleMenu`, `RoleMenuItem`, `RoleMenuItemCheckbox`, `RoleMenuItemRadio`, `RoleScrollBar`, `RoleText`, `RoleHeading`, `RoleImg`, `RoleList`, `RoleListItem`, `RoleSeparator`, `RoleProgressBar`, `RoleTooltip`, `RoleDialog`, `RoleAlertDialog`, `RoleStatus`, `RoleAlert`, `RoleGroup`, `RoleRegion`, `RoleNavigation`, `RoleMain`, `RolePresentation`, `RoleGrid`, `RoleToolbar`, `RoleRadioGroup`, `RoleMenuBar`.

Container roles make an [arrow-key group](#arrow-key-groups-lists): one Tab stop whose items the arrow keys walk. These are `RoleList`, `RoleListBox`, `RoleGrid`, `RoleToolbar`, `RoleRadioGroup`, `RoleTabList`, `RoleMenu` and `RoleMenuBar`.

### Default roles

These controls have a role without any setup. Each class has a static `DefaultAccessibilityRole` you can change or set to `null` app-wide:

| Control | Default role | Name, value and state |
|---|---|---|
| `SkiaSwitch` | `RoleSwitch` | pressed = `IsToggled` |
| `SkiaCheckbox` | `RoleCheckbox` | pressed = `IsToggled` |
| `SkiaRadioButton` | `RoleRadio` | name from `Text`, pressed = `IsToggled` |
| `SkiaSlider` | `RoleSlider` | value from `End` (and `Start` for a range) |
| `SkiaProgress` | `RoleProgressBar` | value from `Value`, spoken as a percentage |
| `SkiaEditor` | `RoleTextBox` | a Tab stop |
| `SkiaButton` | none | name from `Text` once you set a role |
| `SkiaLabel` | none | name from `Text` once you set a role |

To give every button and label a role in one place, set it at startup:

```csharp
SkiaButton.DefaultAccessibilityRole = Aria.RoleButton;
SkiaLabel.DefaultAccessibilityRole = Aria.RoleText;
```

Give toggles, sliders and progress bars a name by purpose (`AccessibilityLabel = "Volume"`). The value is not the name: a slider without a label reads as "slider, 65" with no idea what it changes.

## Accessibility Props

Accessibility metadata is exposed directly on `SkiaControl`.

```csharp
control.AccessibilityRole = Aria.RoleButton;
control.AccessibilityLabel = "Save";
control.AccessibilityHint = "Saves the document";
control.AccessibilityCanInteract = true;
control.AccessibilityIsPressed = false;
control.AccessibilityLive = Aria.LivePolite;
```

- `AccessibilityRole` enables accessibility for the control.
- `AccessibilityLabel` is the name a screen reader says.
- `AccessibilityHint` gives extra context, read after the name.
- `AccessibilityCanInteract` marks the node as interactive, while the pointer could use the control (see [Only controls the pointer can use](#keyboard-navigation)).
- `AccessibilityIsPressed` is the toggle state when the control has one: switches and checkboxes read as checked / not checked, a button with a pressed state as a toggle button.
- `AccessibilityLive` (`Aria.LivePolite` or `Aria.LiveAssertive`) makes the control a live region: screen readers announce its new text when it changes, for example a status line or a counter.

`SkiaLabel.AccessibilityTextSelectable` lets people select and copy a label's text with the mouse, touch and Ctrl+C, see [Selectable text](../controls/text.md#selectable-text).

`IsAccessibilityElement` is true when the control has a role. Setting the role back to `null` removes the control from the accessibility tree.

You can set them from code-behind, or from XAML where it is supported.

## Fluent Code-Behind Methods

The same metadata can be attached with fluent helpers.

```csharp
// General
.WithAccessibility(string role, string? label = null, string? hint = null, bool canInteract = false)
.WithAccessibility(string role, string? label = null, bool canInteract = false)

// Common shortcuts
.WithAccessibilityButton(string label, string? hint = null)
.WithAccessibilityButton(string label)
.WithAccessibilityButton()
.WithAccessibilityText(string text)
.WithAccessibilityText()

// Toggle state
.WithAccessibilityPressed(bool? pressed)
.WithAccessibilityToggle(string label, string? hint = null)

// Live region
.WithAccessibilityLive(string live = "polite")
```

Example:

```csharp
new GameSwitch()
	.WithAccessibilityToggle(ResStrings.Sounds);
```

`WithAccessibilityToggle` keeps `AccessibilityIsPressed` in sync with the toggle state, which is important for screen readers announcing switches and similar controls.

## Range controls (sliders, progress bars)

A range control gives screen readers a **value**, not a name: the current value, the minimum, the maximum, the step, and a spoken text where the number alone is not right ("65%" for a progress bar, "20 – 80" for a range slider). Its name stays the label you set.

`SkiaSlider` and `SkiaProgress` do this already. For your own control, override two members of `SkiaControl`:

```csharp
public class RatingControl : SkiaLayout
{
    public double Rating { get; set; }

    // what a screen reader reads: now, min, max, step, optional spoken text, orientation
    public override AccessibilityValue? GetAccessibilityValue() =>
        new(Rating, 0, 5, 1, $"{Rating} stars of 5");

    // a screen reader sets a value (UI Automation SetValue, TalkBack set progress, AT-SPI Value)
    public override bool OnAccessibilitySetValue(double value)
    {
        Rating = Math.Clamp(Math.Round(value), 0, 5);
        return true;
    }
}
```

A screen reader's **increment / decrement** (VoiceOver swipe up / down, TalkBack adjust, UI Automation) is one arrow-key step, so it goes through `OnAccessibilityKey` with `ArrowUp` / `ArrowDown`. A control that already handles the arrow keys needs nothing more. Adjusting and setting the value work only while the control takes a pan (see [Only controls the pointer can use](#keyboard-navigation)).

A value with `Step` 0 (a progress bar) is read only. A range control reports a horizontal or vertical orientation, which some screen readers need to read it as a normal slider.

## Screen reader actions

Screen readers can do more than read. These actions go through `SkiaAccessibilityManager`, so they follow the same rules on every target:

| Action | What it does | Where |
|---|---|---|
| Activate | `Activate(node)`: a tap in the middle of the node (`OnAccessibilityActivated`), only while a tap could reach it | UIA Invoke / Toggle, TalkBack double tap, VoiceOver double tap, AT-SPI action "click", Enter / Space and click on the web overlay |
| Increment / decrement | `Adjust(node, up)`: one arrow-key step of a range control | UIA RangeValue, TalkBack adjust, VoiceOver swipe up / down |
| Set a value | `SetValue(node, value)`: `OnAccessibilitySetValue`, snapped to the step | UIA RangeValue.SetValue, TalkBack set progress, AT-SPI Value |
| Bring into view | `ScrollIntoView(node)`: every `SkiaScroll` around the node scrolls it into view | UIA ScrollItem, TalkBack show on screen, VoiceOver focus, AT-SPI ScrollTo, web overlay focus |
| Page | `Page(node, vertical, forward)`: the nearest `SkiaScroll` around the node that can move pages by its viewport less a tenth (`SkiaScroll.AccessibilityPage`) | TalkBack scroll forward / back, VoiceOver three-finger swipe |

Paging reports false when no scroll can move that way, so the screen reader gives its own "no more pages" feedback. TalkBack is offered scrolling only while the scroll can move.

## Keyboard navigation

.NET MAUI Windows, WPF, OpenTK (Windows and Linux), .NET MAUI Mac Catalyst, Blazor and WebAssembly walk the snapshot from the keyboard. The keys are the same on every target:

| Key | Action |
|---|---|
| Tab / Shift+Tab | Next / previous interactive node (`AccessibilityCanInteract`), in snapshot order. Past either end focus leaves the canvas, and the next Tab starts over. |
| Enter / Space | `OnAccessibilityActivated()`: a tap on the node. Switches and checkboxes toggle, buttons fire. A `SkiaSlider` ignores it. |
| Arrows, PageUp / PageDown, Home / End | `OnAccessibilityKey(InputKey)` on the node first. `SkiaSlider`: Right / Up and Left / Down step by `Step` (a hundredth of the range when `Step` is 0), PageUp / PageDown move a tenth of the range, Home / End go to `Min` / `Max`. A range slider moves `End`, which stops at `Start`. Keys the node does not use move focus inside its [arrow-key group](#arrow-key-groups-lists). |
| Escape | Leaves the drawn nodes: no node is focused and the ring goes away (desktop targets). |

A node that gets keyboard focus is scrolled into view (`SkiaScroll.EnsureVisible`) inside every enclosing `SkiaScroll`.

On an OpenTK window, Enter, Space, Escape and the arrow keys go to the focused node only while the keyboard is in use (after Tab moved the focus). Until then, they reach your window as usual, so a game keeps its own keys.

**Focus ring.** It appears only after the keyboard was used, never at launch or after a click, like native Windows focus visuals. Desktop targets and WebAssembly draw it on the canvas on top of every frame (`DrawnView.KeyboardFocusNode`, color `DrawnView.KeyboardFocusColor`), so it follows the control while it scrolls. Blazor uses the CSS outline of its overlay element. Pointer input hides it. Every `SkiaScroll` around the focused control keeps its auto-hiding scroll bars visible while the focus is there.

**Your own keys.** Keys the drawn nodes do not use (and Escape too) still reach `KeyboardManager.KeyDown`, for example to close a panel. On .NET MAUI enable it with `UseDesktopKeyboard = true` in `DrawnUiStartupSettings`. The manager listens to the window before the canvas, so it gets every key while the canvas has focus.

**Only controls the pointer can use.** Keyboard navigation follows the pointer's rules (`SkiaControl.CanReceiveGesture`). A node is a Tab stop, gets activated and takes keys only while a tap (a pan, for the arrow keys) would reach it. That means:

- the control and every ancestor draw;
- none of them is `InputTransparent`;
- no ancestor's `LockChildrenGestures` holds that gesture back;
- the control accepts input (a disabled `SkiaButton` does not).

`AccessibilityCanInteract` reports it, even when you set it to true yourself. Opacity does not count, just as for the pointer: to take a control out of input, use `InputTransparent` or `IsVisible`. A dimmed locked group with `InputTransparent = true` locks its sliders for the keyboard too. Changes at runtime (a recycled cell rebound, a lock lifted) apply at the next key press.

**Text fields.** A `SkiaEditor` is a Tab stop by default, like a native text box:

- Tab into it and it takes the caret, so typing goes into it (`FocusedChild` is the editor and `IsFocused` is true, the same as after a click).
- Tab and Shift+Tab leave the field and move on from it, also when a click put the caret there, single-line or multi-line, and insert no tab character.
- While it is being edited, the arrows, Home / End and Enter belong to the text and never move a surrounding group.
- Enter or Space on a field that has keyboard focus but no caret starts editing.
- Tab out of a field keeps the keyboard on the canvas: the next control has keyboard focus, and Enter or Space presses it right away.

**Custom controls.** Override `OnAccessibilityKey(InputKey key)` and return true for the keys the control used. Override `OnAccessibilityActivated()` when a tap in the middle is not the right activation.

### Arrow-key groups (lists)

Mark a container as a group of items with a composite role and keyboard navigation treats it like a native list:

```csharp
new SkiaScroll
{
    Content = new SkiaStack
    {
        AccessibilityRole = Aria.RoleList,          // the group
        ItemsSource = Presets,
        ItemTemplate = new DataTemplate(() => new PresetCell()), // each cell: a role and a label
        RecyclingTemplate = RecyclingTemplate.Enabled,
    }
}
```

- The group roles are `Aria.RoleList`, `RoleListBox`, `RoleGrid`, `RoleToolbar`, `RoleRadioGroup`, `RoleTabList`, `RoleMenu` and `RoleMenuBar`. They work on any layout: `SkiaStack`, `SkiaRow`, `SkiaWrap`, `SkiaGrid`, a templated layout or plain children.
- **One Tab stop.** Tab enters the group on the item that had focus last there (the first item on the first visit), plus the controls inside that item (a remove button on a card). The next Tab leaves the group. Shift+Tab works the same way back.
- **Arrows move by item index**, not by screen position: Down / Up in a `Column`, Right / Left in a `Row`, all four in a `Wrap`, a `Grid` or a `Split` layout (Up / Down by one row, counted on the first row). Home / End go to the first / last item, PageDown / PageUp move by one viewport of the scroll around the group. There is no wrap at the ends.
- **The control first.** A focused control that uses the key keeps it: a slider inside an item steps its value, and the group does not move.
- **Recycled cells.** An item whose cell is not realized is scrolled in (`ScrollToIndex`, when the group is the scroll's `Content`) and focused once drawn, so the arrows walk the whole list, a large windowed `ItemsSource` included. Focus lands on the item itself when it is a node, else on its first interactive node.
- Items the pointer cannot use are skipped (see above). A container without a group role leaves the arrow keys alone.

`SkiaAccessibilityManager.TryFindGroup(control, ...)` and `IsTabStop(node)` expose the rules to custom heads.

**What Tab can reach.** Only nodes in the snapshot, that is controls drawn in the last frame. Content inside a cached container is drawn into its cache as a whole, so every node in it is reachable and scrolls into view. Without a cache, a virtualized layout draws only what is in the viewport, and a recycled templated list realizes only the cells near it. Give such a list a group role: the arrow keys then reach every item (see [Arrow-key groups](#arrow-key-groups-lists)).

## Implementation in depth

### Blazor and WebAssembly (ARIA overlay)

Blazor and the pure WebAssembly head (`DrawnUi.Web`) render the canvas as usual plus an invisible DOM overlay for accessibility.

- The visible canvas surface is marked `aria-hidden`.
- A sibling overlay holds one absolutely positioned element per node, in reading order, with its `role`, `aria-label` and the hint as `title`.
- A pressed state goes on the attribute its role is read from: `aria-checked` for switches, checkboxes and radios, `aria-selected` for options and tabs, `aria-pressed` for toggle buttons.
- Range controls carry `aria-valuenow`, `aria-valuemin`, `aria-valuemax`, `aria-valuetext` and `aria-orientation`.
- A control role that takes no input carries `aria-disabled="true"`. Live regions carry `aria-live`.
- Interactive nodes are focusable (`tabindex`). Inside an arrow-key group only the current item has `tabindex="0"`. Other nodes have `tabindex="-1"`: not Tab stops, but a screen reader can still be moved to them.
- The focused element is the canvas keyboard focus and the screen reader's node. Enter / Space activate it, the arrow keys go to the node and then to its group, and DrawnUI moves DOM focus to the next item.

On WebAssembly the overlay is created by `drawnui-web.js` right after your `<canvas>` element, with no app code. Overlay elements never take the pointer (`pointer-events: none`): every gesture and hover stays with the canvas. The canvas draws the focus ring.

**IMPORTANT (Blazor)**: the accessibility overlay and canvas hover on the same control are mutually exclusive. If you add accessibility metadata to a drawn control, it stops receiving `Pointer` gestures and cannot react to hover. The matching accessibility DOM element catches them instead. Other gestures work as usual.

### Windows (UI Automation)

.NET MAUI Windows, WPF and OpenTK on Windows expose drawn controls through UI Automation. Every node of the snapshot becomes a virtual element under the canvas:

- control type, name, help text (the hint), bounding rectangle in screen coordinates, parent / sibling / child navigation;
- `IsEnabled` false for a control role that takes no input;
- **Invoke** (activate) and **Toggle** (pressed state) for nodes that take input;
- **RangeValue** for range controls (read only for a progress bar and a slider that takes no input; `SetValue` snaps to the step), **Value** with the spoken text ("65%", "20 – 80"), and the orientation;
- **ScrollItem** on every node;
- `StructureChanged` when the snapshot is rebuilt, focus-changed events when keyboard focus moves, live-region events on .NET MAUI Windows and WPF.

UIA `SetFocus` moves the screen reader's cursor onto the node, it does not activate it.

The hosts differ only in where the tree hangs. .NET MAUI Windows uses WinUI automation peers on the canvas element. WPF uses WPF automation peers. OpenTK answers `WM_GETOBJECT` on its own window with a UIA fragment root. The COM interface types are shared between .NET MAUI Windows and OpenTK.

### Android (TalkBack)

The canvas view gets an AndroidX `ExploreByTouchHelper`. Every node is a virtual view whose id is the node's stable id, so TalkBack keeps its place when the snapshot is rebuilt.

- Explore by touch, swipe navigation and double tap (activate) work as on a native screen.
- Role to Android class name (button, check box, switch, seek bar, progress bar, edit text...), text and headings as text, the rest as content description.
- Range controls have range info and a state description. An adjustable slider takes scroll forward / backward (TalkBack adjust) and set progress.
- Every node offers "show on screen". The canvas offers scroll forward / backward for the scroll around TalkBack's node, only while it can move.
- Live regions, disabled control roles, checkable state.

Nothing runs while no accessibility service with touch exploration is on.

### iOS and Mac Catalyst (VoiceOver)

The canvas view becomes an accessibility container with one `UIAccessibilityElement` per node:

- traits from the role (button, link, image, static text, header, adjustable, search field), not enabled for a control role without input, updates frequently for progress bars and live regions;
- a toggle's state with the toggle-button trait (iOS 17 and later);
- the label and value are read from the control when VoiceOver asks, so a slider says its new value right after an adjust;
- double tap activates, swipe up / down adjusts a slider, a three-finger swipe pages the scroll around the node (no when nothing can move);
- a node VoiceOver moves to is scrolled into view;
- when VoiceOver's node goes away, the layout-changed notification moves it to the next node.

On iOS the elements exist only while VoiceOver or Switch Control runs. On Mac Catalyst they are always kept, for VoiceOver, Full Keyboard Access and Voice Control.

### Linux (AT-SPI2, Orca)

An OpenTK window on Linux publishes the snapshot on the AT-SPI bus over D-Bus (package `Tmds.DBus.Protocol`). The tree is the application, one frame for the window, and the nodes in reading order.

- **Accessible:** role, name, description (the hint), state set (focusable, focused, enabled, checkable, checked, pressed, orientation).
- **Component:** extents and focus.
- **Action:** "click" activates.
- **Value:** writing it sets the value.
- **Events:** focus, value, name and state changes, nodes added and removed, window activation, and live-region announcements.

It costs nothing until assistive technology turns the accessibility bus on (Orca does at start). It also connects later, when a screen reader starts after the app.

To check it, start Orca and listen, or read the tree with `accerciser` or Python `pyatspi`.

## Related

- [Handling Gestures](../gestures.md)
- [Platform-Specific Styling](platform-styling.md)
- [Blazor Capabilities](../blazor/capabilities.md)
