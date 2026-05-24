# DrawnUI Accessibility

DrawnUI renders entirely via SkiaSharp — the browser and OS see a single canvas pixel surface with no native control tree. Accessibility requires a parallel virtual element layer that mirrors drawn controls to assistive technology.

## Concept

Each platform exposes drawn controls differently:

| Platform | Mechanism |
|---|---|
| Blazor | Invisible ARIA `<div>` overlay positioned over the canvas |
| OpenTK Windows | UIA `IRawElementProviderFragment` virtual elements attached to the native OpenTK / GLFW host window |
| OpenTK Linux | AT-SPI virtual accessibles attached to the native OpenTK / GLFW host window |
| MAUI iOS / macCatalyst | `IUIAccessibilityContainer` virtual elements on the native view |
| MAUI Android | `ExploreByTouchHelper` on `SKCanvasView` |
| MAUI Windows | UIA `IRawElementProviderFragment` virtual elements |

All platforms share the same C# infrastructure in the shared project. Platform layers consume the `SkiaAccessibilityManager` snapshot and translate it into the native a11y API.

---

## Shared Infrastructure (done)

### `SkiaControl` — accessibility props

```csharp
control.AccessibilityRole        = Aria.RoleButton;  // makes IsAccessibilityElement true
control.AccessibilityLabel       = "Save";
control.AccessibilityHint        = "Saves the document";
control.AccessibilityCanInteract = true;             // enables tab-stop, click, keyboard
control.AccessibilityIsPressed   = false;            // aria-pressed: null=absent, true/false=toggle state
```

`IsAccessibilityElement` is a computed getter: `AccessibilityRole != null`.

Setting `AccessibilityRole` back to `null` automatically unregisters the control from the manager.

### Fluent extensions

```csharp
// General
.WithAccessibility(string role, string? label = null, string? hint = null, bool canInteract = false)
.WithAccessibility(string role, string? label = null, bool canInteract = false)

// Shortcuts
.WithAccessibilityButton(string label, string? hint = null)   // role=button, canInteract=true
.WithAccessibilityButton(string label)                        // role=button, canInteract=true
.WithAccessibilityButton()                                    // role=button, label from .Text (SkiaButton)
.WithAccessibilityText(string text)                           // role=text
.WithAccessibilityText()                                      // role=text, label from .Text (SkiaLabel)

// Toggle state (aria-pressed)
.WithAccessibilityPressed(bool? pressed)                      // set aria-pressed manually, null = absent
.WithAccessibilityToggle(string label, string? hint = null)   // SkiaToggle: role=switch, aria-pressed auto-synced
```

`WithAccessibilityToggle` (available on any `SkiaToggle` subclass) sets the initial `AccessibilityIsPressed` from `IsToggled` and subscribes to `IsToggled` changes via `ObserveProperty` — automatically unsubscribes on control disposal through `ExecuteUponDisposal`.

```csharp
new GameSwitch()
    .WithAccessibilityToggle(ResStrings.Sounds)
```

### `ISkiaAccessibilityNode` interface

Implemented by all `SkiaControl` instances. Parallel to `ISkiaGestureListener` — platform layers work against the interface, not the concrete class.

Key members:
- `AccessibilityRole / Label / Hint / CanInteract / IsPressed` — props
- `IsAccessibilityElement` — computed: `Role != null`
- `GetAccessibilityPixelRect()` — returns `VisualLayer?.HitBoxWithTransforms.Pixels ?? DrawingRect`
- `OnAccessibilityActivated()` — synthesises a `Tapped` gesture via `OnSkiaGestureEvent`
- `NotifyAccessibility()` — register or mark dirty in the manager
- `OnAccessibilityUnregistered()` — called by manager on removal

### Registration lifecycle

1. `OnLayoutReady()` — fires once on first valid layout; if `IsAccessibilityElement`, automatically calls `NotifyAccessibility()`.
2. `NotifyAccessibility()` — registers (first call) or marks dirty (subsequent). Call manually when any a11y prop changes at runtime.
3. `SetParent(null)` — when a control is detached from the tree, calls `UnregisterSubtree(this)` on the old superview's manager, cascade-removing the control and all registered descendants — same pattern as gesture listener cleanup.
4. `OnDisposing()` — calls `UnregisterSubtree(this)`.

### `SkiaAccessibilityManager`

Lives on `DrawnView`. Maintains a `ConcurrentDictionary<ISkiaAccessibilityNode, byte>`.

- Marks dirty on any `Register` / `NotifyUpdated` call.
- `OnFrameEnd(scale)` — called at end of `OnFinalizeRendering`. Rebuilds sorted `Snapshot` at most once per `MinUpdateIntervalMs` (default 1000 ms). Safe at 144 fps.
- Sorts controls top→left reading order.
- Fires `Changed` event after each rebuild.

Snapshot is an `AccessibilityNode[]`:

```csharp
public record AccessibilityNode(
    string? Label, string? Hint, string? Role,
    SKRect Rect, bool CanInteract, bool? IsPressed)
```

`Rect` is in CSS pixels (device-independent), divided by rendering scale.

### `OnAccessibilityActivated()`

Virtual method on `SkiaControl`. Default synthesises a `Tapped` gesture directly via `OnSkiaGestureEvent`. Override per control type for custom activation.

### `Aria` constants

`DrawnUi.Models.Aria` — static class with 30 role string constants and XML doc comments. Use instead of raw strings.

Interactive widgets: `RoleButton`, `RoleLink`, `RoleCheckbox`, `RoleRadio`, `RoleSwitch`, `RoleSlider`, `RoleSpinbutton`, `RoleTextbox`, `RoleSearchbox`, `RoleCombobox`, `RoleListbox`, `RoleOption`, `RoleTab`, `RoleTabpanel`, `RoleTablist`, `RoleMenu`, `RoleMenuitem`, `RoleMenuitemcheckbox`, `RoleMenuitemradio`, `RoleScrollbar`

Structural / landmark: `RoleText`, `RoleHeading`, `RoleImg`, `RoleList`, `RoleListitem`, `RoleSeparator`, `RoleProgressbar`, `RoleTooltip`, `RoleDialog`, `RoleAlertdialog`, `RoleStatus`, `RoleAlert`, `RoleGroup`, `RoleRegion`, `RoleNavigation`, `RoleMain`, `RolePresentation`

---

## Blazor (done)

### ARIA overlay

`Canvas.razor` renders a sibling `<div class="xaml-a11y-overlay">` next to the `aria-hidden` canvas surface. For each node in the manager snapshot, one `<div>` is absolutely positioned to match the drawn control's CSS-pixel bounds.

```html
<!-- non-interactive: read-only, no tab stop -->
<div role="text" aria-label="Hello"
     class="xaml-a11y-element" style="left:…;top:…;width:…;height:…">
</div>

<!-- interactive: tab-navigable, click/Enter/Space activates the drawn control -->
<div role="button" aria-label="Save" tabindex="0"
     class="xaml-a11y-element xaml-a11y-interactive" style="…">
</div>

<!-- toggle: aria-pressed reflects current state -->
<div role="switch" aria-label="Sounds" aria-pressed="true" tabindex="0"
     class="xaml-a11y-element xaml-a11y-interactive" style="…">
</div>
```

`AccessibilityCanInteract = true` enables:
- `tabindex="0"` — keyboard tab stop
- `cursor: pointer`
- `:focus-visible` blue outline (3 px, rgba(0,103,244,0.85))
- `@onclick` → `OnA11yActivated` → `control.OnAccessibilityActivated()`
- `@onkeydown` Enter/Space → same

`AccessibilityIsPressed` — when non-null, renders `aria-pressed="true"` or `aria-pressed="false"` on the overlay element. `null` omits the attribute (Blazor null-attribute behavior).

`AccessibilityCanInteract = false` — element present in ARIA tree for reading only; no tab stop, ignores pointer events.

### Manager subscription

`Canvas.OnAfterRenderAsync` subscribes to `AccessibilityManager.Changed`. On change, `_accessibilityNodes` is updated and `StateHasChanged` is invoked asynchronously, keeping the overlay in sync after each throttled rebuild.

---

## Remaining work

### Blazor

- [ ] **Mouse hover screen reader highlight** — ChromeVox and similar readers should show a highlight rectangle on mouse hover. Currently under investigation (canvas pointer-event layering may intercept mouse before the overlay `mouseover`).
- [ ] **`aria-checked`** — for checkboxes / radio buttons (distinct from `aria-pressed`).
- [ ] **`aria-selected`** — for tabs, list options.
- [ ] **`aria-expanded`** — for collapsible regions, comboboxes.
- [ ] **`aria-disabled`** — propagate disabled state.
- [ ] **Live regions** — `aria-live="polite"` container for dynamic text announcements (status labels, counters).
- [ ] **`aria-describedby`** — link hint text properly instead of using `title`.
- [ ] **Focus management** — when `FocusedChild` changes in the gesture system, move DOM focus to the matching overlay element so the screen reader cursor follows.

### MAUI — iOS / macCatalyst (not started)

- [ ] Override `VisibilityAwarePlatformView` to implement `IUIAccessibilityContainer`.
- [ ] Provide virtual `UIAccessibilityElement` objects from `AccessibilityManager.Snapshot`.
- [ ] Wire `AccessibilityManager.Changed` to `UIAccessibility.PostNotification(UIAccessibilityPostNotification.LayoutChanged, ...)`.

### MAUI — Android (not started)

- [ ] Attach `ExploreByTouchHelper` to the `SKCanvasView` platform view.
- [ ] Implement `GetVirtualViewAt`, `GetVisibleVirtualViews`, `OnPopulateNodeForVirtualView`, `OnPerformActionForVirtualView` using the snapshot.
- [ ] Map `OnAccessibilityActivated` to `PerformActionForVirtualView` with `AccessibilityNodeInfoCompat.ActionClick`.

### MAUI — Windows (not started)

- [ ] Implement `IRawElementProviderFragment` + `IRawElementProviderSimple` for virtual UIA elements.
- [ ] Expose `Rect`, `Name` (label), `LocalizedControlType` (role), `IsKeyboardFocusable` (canInteract).
- [ ] Raise `UIA_AutomationFocusChangedEventId` when focus changes.

### OpenTK — Windows (not started)

- [ ] Hook accessibility to the native OpenTK / GLFW host window handle on Windows.
- [ ] Expose `SkiaAccessibilityManager.Snapshot` as UIA fragment providers, similar to the planned MAUI Windows bridge.
- [ ] Route accessibility activation back to `OnAccessibilityActivated()` and raise focus-changed events when keyboard focus moves.

### OpenTK — Linux (not started)

- [ ] Hook accessibility to the native OpenTK / GLFW host window on Linux.
- [ ] Mirror `SkiaAccessibilityManager.Snapshot` into an AT-SPI accessible tree for desktop assistive technology.
- [ ] Route accessibility activation and focus changes back into the shared DrawnUI accessibility pipeline.

### General

- [ ] XAML bindable properties for all accessibility props (`AccessibilityRole`, `AccessibilityLabel`, `AccessibilityHint`, `AccessibilityCanInteract`, `AccessibilityIsPressed`).
- [ ] `AccessibilityManager.MinUpdateIntervalMs` exposed as a `Canvas` parameter.
- [ ] Unit tests for snapshot rebuild ordering and subtree unregistration.
