# Accessibility

DrawnUI renders controls into a Skia surface instead of creating a native control tree. Accessibility therefore needs a parallel virtual representation that assistive technology can read and activate.

Drawn controls do not have accessibility turned on by default on purpose to let you cotrol which parts will be exposed and how.

## Current Support

| Framework / target | Status | Implementation | Notes |
|---|---|---|---|
| Blazor | Available | Invisible ARIA overlay positioned over the canvas | Accessible today, with one important hover limitation described below |
| OpenTK Windows | Available | UIA virtual fragment providers on the native OpenTK / GLFW window | Narrator and NVDA can read and activate drawn controls |
| .NET MAUI Windows | Available | UIA virtual fragment providers on the WinUI 3 `DesktopChildSiteBridge` | Narrator and NVDA can read and activate drawn controls; [keyboard navigation](#keyboard-navigation) with a focus ring drawn on the canvas |
| WPF | Available (preview) | WPF `AutomationPeer`s: the canvas is a pane, every snapshot node a virtual peer with Invoke / Toggle patterns | [Keyboard navigation](#keyboard-navigation) with a focus ring; verified with a UI Automation client |
| OpenTK Linux | Incoming | AT-SPI bridge on the native OpenTK window | Planned, not shipped yet |
| .NET MAUI iOS / macCatalyst | Incoming | Virtual `UIAccessibilityElement` container | Planned, not shipped yet |
| .NET MAUI Android | Incoming | Virtual nodes via `ExploreByTouchHelper` | Planned, not shipped yet |

All targets share the same C# accessibility metadata on `SkiaControl`. Platform-specific layers consume the `SkiaAccessibilityManager` snapshot and expose it through the native accessibility API for that platform.

## Shared Model

Accessibility starts in shared code. A drawn control can expose:

- role
- label
- hint
- whether it can interact
- pressed / toggle state

That metadata is collected by `SkiaAccessibilityManager`, which maintains a snapshot of accessible nodes and their bounds in UI coordinates.

Every `SkiaControl` implements `ISkiaAccessibilityNode`, so platform layers work against the interface instead of the concrete class, the same way gestures work against `ISkiaGestureListener`.

The snapshot itself is an array of:

```csharp
public record AccessibilityNode(
    string? Label, string? Hint, string? Role,
    SKRect Rect, bool CanInteract, bool? IsPressed)
```

`Rect` is in device-independent pixels, where the control is on screen now: inside cached containers and scrolled content too. The array is in reading order: rows top to bottom, each row left to right (controls whose tops are within half the smaller height share a row, so a row of vertically centered controls of different heights reads left to right). Only controls the last frame drew are in it, live or inside a cached parent that is blitted; a control a virtualized layout stopped drawing (scrolled out, a recycled cell back in the pool) leaves the snapshot until it is drawn again.

### Registration lifecycle

- `OnLayoutReady()` fires once on the first valid layout and registers the control automatically when `IsAccessibilityElement` is true.
- `NotifyAccessibility()` registers on the first call and marks the snapshot dirty afterwards. Call it manually when you change accessibility props at runtime.
- Detaching a control from the tree or disposing it unregisters it together with all its registered descendants.

The manager rebuilds its snapshot at most once per `MinUpdateIntervalMs` (default 1000 ms) at the end of a drawn frame, so it stays cheap at high frame rates and follows scrolling and animations. It raises `Changed` only when the snapshot differs, and costs nothing while no control is registered.

### Roles

Use the constants from `DrawnUi.Models.Aria` instead of raw strings: `RoleButton`, `RoleLink`, `RoleCheckbox`, `RoleRadio`, `RoleSwitch`, `RoleSlider`, `RoleTextbox`, `RoleTab`, `RoleMenuitem`, `RoleText`, `RoleHeading`, `RoleImg`, `RoleList`, `RoleProgressbar`, `RoleDialog`, `RoleAlert`, `RoleGroup`, `RoleNavigation` and more.

## Accessibility Props

Accessibility metadata is exposed directly on `SkiaControl`.

```csharp
control.AccessibilityRole = Aria.RoleButton;
control.AccessibilityLabel = "Save";
control.AccessibilityHint = "Saves the document";
control.AccessibilityCanInteract = true;
control.AccessibilityIsPressed = false;
```

- `AccessibilityRole` enables accessibility for the control
- `AccessibilityLabel` is the main spoken label
- `AccessibilityHint` gives extra context for assistive technology
- `AccessibilityCanInteract` marks the node as interactive
- `AccessibilityIsPressed` maps toggle state when applicable

`IsAccessibilityElement` is computed from `AccessibilityRole != null`. Setting the role back to `null` removes the control from the accessibility tree.

Can set them from code-behind or XAML where it is supported.

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
```

Example:

```csharp
new GameSwitch()
	.WithAccessibilityToggle(ResStrings.Sounds);
```

`WithAccessibilityToggle` keeps `AccessibilityIsPressed` in sync with toggle state, which is important for screen readers announcing switches and similar controls.

## Keyboard navigation

.NET MAUI Windows, WPF and Blazor walk the snapshot from the keyboard. The keys are the same on every head:

| Key | Action |
|---|---|
| Tab / Shift+Tab | Next / previous interactive node (`AccessibilityCanInteract`), in snapshot order. Past either end focus leaves the canvas, the next Tab starts over. |
| Enter / Space | `OnAccessibilityActivated()`: a synthesized tap on the node. Switches and checkboxes toggle, buttons fire. A `SkiaSlider` ignores it. |
| Arrows, PageUp / PageDown, Home / End | `OnAccessibilityKey(InputKey)` on the node. `SkiaSlider`: Right / Up and Left / Down step by `Step` (a hundredth of the range when `Step` is 0), PageUp / PageDown move a tenth of the range, Home / End go to `Min` / `Max`; a ranged slider moves `End`, which stops at `Start`. |
| Escape | Leaves the drawn nodes: no node is focused and the ring goes away (Windows heads). |

A node that gets keyboard focus is scrolled into view (`SkiaScroll.EnsureVisible`) inside every enclosing `SkiaScroll`.

**Focus ring.** It appears only after the keyboard was used, never at launch or after a click, like native Windows focus visuals. WPF draws it with WPF; .NET MAUI Windows draws it on the canvas on top of every frame (`DrawnView.KeyboardFocusNode`, color `DrawnView.KeyboardFocusColor`), so it follows the control while it scrolls. Pointer input hides it.

**Your own keys.** Keys the drawn nodes do not use (and Escape too) still reach `KeyboardManager.KeyDown`, for example to close a panel. On .NET MAUI enable it with `UseDesktopKeyboard = true` in `DrawnUiStartupSettings`; the manager listens to the window before the canvas, so it gets every key while the canvas has focus.

**Custom controls.** Override `OnAccessibilityKey(InputKey key)` and return true for the keys the control used; override `OnAccessibilityActivated()` when a tap in the middle is not the right activation.

**What Tab can reach.** Only nodes in the snapshot, that is controls drawn in the last frame. Content inside a cached container is drawn into its cache as a whole, so every node in it is reachable and scrolls into view. Without a cache a virtualized layout draws only what is in the viewport, and a recycled templated list realizes only the cells near it: Tab walks the visible cells and then leaves the list. For a list the keyboard must reach fully, cache its content (`UseCache = Image` on the scroll content, when the list is small), or handle arrow keys on the list yourself and call `ScrollToIndex`.

## Implementation in deep

### Blazor 

In Blazor, DrawnUI renders the canvas as usual and also renders an invisible DOM overlay for accessibility.

- The visible canvas surface is marked `aria-hidden`.
- A sibling overlay contains absolutely positioned ARIA elements that mirror the drawn controls.
- Interactive accessibility nodes can receive keyboard focus and activation.

This gives screen readers a DOM-based accessibility surface even though the real UI is drawn.

**IMPORTANT**: on Blazor accessibility overlay and canvas hover on the same control are mutually exclusive. if you add accessibility metadata to a drawn control it will stop receiving `Pointer` gestures and will not be able to react to hover, those will be catched by a corresponding accessibility DOM element. Other gestures will work as usual.

### Windows (UIA)

Both Windows targets, OpenTK and .NET MAUI, expose drawn controls through UI Automation. There is no DOM and no native control tree involved: the host window answers `WM_GETOBJECT` with a UIA fragment root, and every node of the accessibility snapshot becomes a virtual `IRawElementProviderFragment`.

What is wired:

- fragment root returned from a `WndProc` subclass on the host window
- one virtual provider per accessible control, with control type, name and runtime id
- bounding rectangles translated into screen coordinates
- tree navigation: parent, siblings, children
- `StructureChanged` raised when the snapshot is rebuilt
- `SetFocus` routed into `OnAccessibilityActivated()`, so activating a node from a screen reader triggers the drawn control
- `AutomationFocusChanged` raised when keyboard focus moves

Narrator and NVDA can read and activate drawn controls on both heads.

The COM interop types are shared between the two implementations. MAUI Windows hooks the WinUI 3 `DesktopChildSiteBridge` child window, which is the one that receives `WM_GETOBJECT` for content, instead of the top-level window, while OpenTK hooks its own native window.

## Related

- [Handling Gestures](../gestures.md)
- [Platform-Specific Styling](platform-styling.md)
- [Blazor Capabilities](../blazor/capabilities.md)