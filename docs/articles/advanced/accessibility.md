# Accessibility

DrawnUI renders controls into a Skia surface instead of creating a native control tree. Accessibility therefore needs a parallel virtual representation that assistive technology can read and activate.

Drawn controls do not have accessibility turned on by default on purpose to let you cotrol which parts will be exposed and how.

## Current Support

| Framework / target | Status | Implementation | Notes |
|---|---|---|---|
| Blazor | Available | Invisible ARIA overlay positioned over the canvas | Accessible today, with one important hover limitation described below |
| OpenTK Windows | Incoming | UIA provider bridge on the native OpenTK window | Planned, not shipped yet |
| OpenTK Linux | Incoming | AT-SPI bridge on the native OpenTK window | Planned, not shipped yet |
| .NET MAUI iOS / macCatalyst | Incoming | Virtual `UIAccessibilityElement` container | Planned, not shipped yet |
| .NET MAUI Android | Incoming | Virtual nodes via `ExploreByTouchHelper` | Planned, not shipped yet |
| .NET MAUI Windows | Incoming | Virtual UIA fragment providers | Planned, not shipped yet |

All targets share the same C# accessibility metadata on `SkiaControl`. Platform-specific layers consume the `SkiaAccessibilityManager` snapshot and expose it through the native accessibility API for that platform.

## Shared Model

Accessibility starts in shared code. A drawn control can expose:

- role
- label
- hint
- whether it can interact
- pressed / toggle state

That metadata is collected by `SkiaAccessibilityManager`, which maintains a snapshot of accessible nodes and their bounds in UI coordinates.

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

## Implementation in deep

### Blazor 

In Blazor, DrawnUI renders the canvas as usual and also renders an invisible DOM overlay for accessibility.

- The visible canvas surface is marked `aria-hidden`.
- A sibling overlay contains absolutely positioned ARIA elements that mirror the drawn controls.
- Interactive accessibility nodes can receive keyboard focus and activation.

This gives screen readers a DOM-based accessibility surface even though the real UI is drawn.

**IMPORTANT**: on Blazor accessibility overlay and canvas hover on the same control are mutually exclusive. if you add accessibility metadata to a drawn control it will stop receiving `Pointer` gestures and will not be able to react to hover, those will be catched by a corresponding accessibility DOM element. Other gestures will work as usual.

## Related

- [Handling Gestures](../gestures.md)
- [Platform-Specific Styling](platform-styling.md)
- [Blazor Capabilities](../blazor/capabilities.md)