namespace DrawnUi.Draw;

/// <summary>
/// Marks a drawn control as an accessibility node visible to screen readers and assistive technology.
/// Analogous to <see cref="ISkiaGestureListener"/> for the gesture system — implement this interface
/// (or set <c>AccessibilityRole</c> on a <see cref="SkiaControl"/>) and the control registers itself
/// automatically with the <c>SkiaAccessibilityManager</c> on first layout and unregisters on detachment.
/// </summary>
public interface ISkiaAccessibilityNode
{
    /// <summary>ARIA/platform role string. Setting this makes <see cref="IsAccessibilityElement"/> true. Use <see cref="DrawnUi.Models.Aria"/> constants.</summary>
    string? AccessibilityRole { get; set; }

    /// <summary>Human-readable label announced by screen readers (e.g. button text, image description).</summary>
    string? AccessibilityLabel { get; set; }

    /// <summary>Additional hint announced after the label (e.g. "Double-tap to activate").</summary>
    string? AccessibilityHint { get; set; }

    /// <summary>
    /// When true the overlay element gets a tab-stop, pointer cursor, and click/keyboard handlers.
    /// Set via <c>.WithAccessibility(..., canInteract: true)</c> or directly.
    /// </summary>
    bool AccessibilityCanInteract { get; set; }

    /// <summary>
    /// Maps to <c>aria-pressed</c>. Use for toggle buttons: <c>true</c> = pressed, <c>false</c> = not pressed, <c>null</c> = not a toggle (attribute omitted).
    /// </summary>
    bool? AccessibilityIsPressed { get; set; }

    /// <summary>True when <see cref="AccessibilityRole"/> is non-null. Computed, not stored.</summary>
    bool IsAccessibilityElement { get; }

    /// <summary>
    /// Returns the control's hit rect in raw pixels, used to position the overlay element.
    /// Typically <c>VisualLayer.HitBoxWithTransforms.Pixels</c> with <c>DrawingRect</c> as fallback.
    /// </summary>
    SKRect GetAccessibilityPixelRect();

    /// <summary>
    /// Called by the platform a11y layer (e.g. Blazor overlay click/Enter) to activate the control.
    /// Default implementation synthesises a Tapped gesture directly on this control.
    /// </summary>
    void OnAccessibilityActivated();

    /// <summary>
    /// Registers or updates this node in the superview's <c>SkiaAccessibilityManager</c>.
    /// Called automatically on first layout; call manually when label/hint/state changes at runtime.
    /// </summary>
    void NotifyAccessibility();

    /// <summary>Called by <c>SkiaAccessibilityManager</c> when this node is removed from the registry.</summary>
    void OnAccessibilityUnregistered();
}
