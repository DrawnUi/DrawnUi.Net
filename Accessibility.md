# DrawnUI Accessibility

DrawnUI renders entirely via SkiaSharp — the browser and OS see a single canvas pixel surface with no native control tree. Accessibility requires a parallel virtual element layer that mirrors drawn controls to assistive technology.

## Concept

Each platform exposes drawn controls differently:

| Platform | Mechanism |
|---|---|
| Blazor | Invisible ARIA `<div>` overlay positioned over the canvas |
| iOS / macCatalyst | `IUIAccessibilityContainer` virtual elements on the native view |
| Android | `ExploreByTouchHelper` on `SKCanvasView` |
| Windows | UIA `IRawElementProviderFragment` virtual elements |

All platforms share the same C# infrastructure in the shared project. Platform layers consume the `SkiaAccessibilityManager` snapshot and translate it into the native a11y API.

## Shared Infrastructure (done)

### `SkiaControl` — accessibility props

```csharp
// set these properties on any SkiaControl
control.AccessibilityRole        = Aria.RoleButton;  // makes IsAccessibilityElement true
control.AccessibilityLabel       = "Save";
control.AccessibilityHint        = "Saves the document";
control.AccessibilityCanInteract = true;             // enables tab-stop, click, keyboard
```

`IsAccessibilityElement` is a computed getter: `AccessibilityRole != null`.

### Fluent extension

```csharp
new SkiaButton("Save")
    .WithAccessibility(Aria.RoleButton, "Save", canInteract: true)
```

Signature: `WithAccessibility(string role, string? label = null, string? hint = null, bool canInteract = false)`

### Registration lifecycle

1. `OnLayoutReady()` — fires once on first valid layout. If `IsAccessibilityElement`, automatically calls `NotifyAccessibility()`.
2. `NotifyAccessibility()` — registers the control with `SkiaAccessibilityManager` (first call) or marks it dirty (subsequent calls). Call manually whenever label/hint/role change at runtime.
3. `OnDisposing()` — calls `UnregisterSubtree(this)` which cascade-removes the control and all its registered descendants in one pass.

### `SkiaAccessibilityManager`

Lives on `DrawnView`. Maintains a `ConcurrentDictionary` of registered controls.

- Marks dirty on any `Register` / `NotifyUpdated` call.
- `OnFrameEnd(scale)` — called at the end of `OnFinalizeRendering`. Rebuilds the sorted `Snapshot` array at most once per `MinUpdateIntervalMs` (default 1000 ms). Safe at 144 fps.
- Sorts controls by top→left reading order.
- Fires `Changed` event after every rebuild.

Snapshot is an `AccessibilityNode[]`:

```csharp
public record AccessibilityNode(
    string? Label, string? Hint, string? Role, SKRect Rect, bool CanInteract)
```

Rect is in CSS pixels (device-independent), already divided by rendering scale.

### `OnAccessibilityActivated()`

Virtual method on `SkiaControl`. Default implementation synthesises a `Tapped` gesture directly on the control via `OnSkiaGestureEvent` — no routing through the superview needed.

Override per control type for custom activation behavior.

### `Aria` constants

`DrawnUi.Models.Aria` — static class with 30 role constants and XML doc comments covering interactive widgets, structural roles, and landmarks. Use instead of raw strings.

---

## Blazor (done)

### ARIA overlay

`Canvas.razor` renders a sibling `<div class="xaml-a11y-overlay">` next to the `aria-hidden` canvas surface. For each node in the manager snapshot, one `<div>` is absolutely positioned to match the drawn control's bounds.

```html
<!-- non-interactive: read-only for screen readers, no tab stop -->
<div role="text" aria-label="Hello" class="xaml-a11y-element" style="left:…;top:…;width:…;height:…">
</div>

<!-- interactive: tab-navigable, click/Enter/Space activates the drawn control -->
<div role="button" aria-label="Save" tabindex="0"
     class="xaml-a11y-element xaml-a11y-interactive" style="…">
</div>
```

`AccessibilityCanInteract = true` enables:
- `tabindex="0"` — keyboard tab stop
- `pointer-events: auto` — mouse interaction
- `cursor: pointer`
- `:focus-visible` blue outline (3 px, rgba(0,103,244,0.85))
- `@onclick` → `OnA11yActivated` → `control.OnAccessibilityActivated()`
- `@onkeydown` Enter/Space → same

`AccessibilityCanInteract = false` — element exists in ARIA tree for reading but has no tab stop and ignores pointer events.

### Manager subscription

`Canvas.OnAfterRenderAsync` subscribes to `AccessibilityManager.Changed`. On change, `_accessibilityNodes` is updated and `StateHasChanged` is invoked asynchronously, keeping the overlay in sync after each throttled rebuild.

---

## Remaining work

### Blazor

- [ ] **Mouse hover screen reader highlight** — ChromeVox and similar readers should show a highlight rectangle when the mouse hovers an interactive a11y element. Currently under investigation (canvas pointer-event layering may intercept mouse before the overlay element receives `mouseover`).
- [ ] **ARIA state attributes** — `aria-checked`, `aria-selected`, `aria-expanded`, `aria-disabled`, `aria-pressed` for stateful controls (checkbox, switch, tab, etc.).
- [ ] **Live regions** — `aria-live="polite"` container for controls that announce dynamic text changes (status labels, counters).
- [ ] **`aria-describedby`** — link hint text to the element properly instead of using `title`.
- [ ] **Focus management** — when `FocusedChild` changes in the gesture system, move DOM focus to the matching overlay element so the screen reader cursor follows.

### MAUI — iOS / macCatalyst (not started)

- [ ] Override `VisibilityAwarePlatformView` to implement `IUIAccessibilityContainer`.
- [ ] Provide virtual `UIAccessibilityElement` objects built from `AccessibilityManager.Snapshot`.
- [ ] Wire `AccessibilityManager.Changed` to call `UIAccessibility.PostNotification(UIAccessibilityPostNotification.LayoutChanged, ...)`.

### MAUI — Android (not started)

- [ ] Attach `ExploreByTouchHelper` to the `SKCanvasView` platform view.
- [ ] Implement `GetVirtualViewAt`, `GetVisibleVirtualViews`, `OnPopulateNodeForVirtualView`, `OnPerformActionForVirtualView` using the snapshot.
- [ ] Map `OnAccessibilityActivated` to `PerformActionForVirtualView` with `AccessibilityNodeInfoCompat.ActionClick`.

### MAUI — Windows (not started)

- [ ] Implement `IRawElementProviderFragment` + `IRawElementProviderSimple` for virtual UIA elements.
- [ ] Expose `Rect`, `Name` (label), `LocalizedControlType` (role), and `IsKeyboardFocusable` (canInteract).
- [ ] Raise `UIA_AutomationFocusChangedEventId` when focus changes.

### General

- [ ] XAML bindable properties for `AccessibilityRole`, `AccessibilityLabel`, `AccessibilityHint`, `AccessibilityCanInteract`.
- [ ] Opt-in `AccessibilityManager.MinUpdateIntervalMs` exposed as a `Canvas` parameter.
- [ ] Unit tests for snapshot rebuild ordering and subtree unregistration.
