using AppoMobi.Gestures;
using DrawnUi.Views;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using MouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;
using Vector2 = OpenTK.Mathematics.Vector2;
using Vector2i = OpenTK.Mathematics.Vector2i;

namespace DrawnUi.OpenTk;

/// <summary>
/// Routes GLFW mouse events into the canvas. Every button presses, drags and taps — a control that
/// only wants the primary button filters on <c>args.Event.Pointer.Button</c>; releasing the right button then also
/// raises <c>SkiaControl.ContextMenu</c>, as on WPF and the browser heads (the keyboard's Menu key goes through
/// <see cref="OnContextMenuKey"/>). One button owns the pointer at a time: a second button pressed while the first is
/// held is ignored until the first is released.
/// </summary>
public class DesktopGestureHandler
{
    private readonly Canvas _canvas;
    private MouseButton? _pressed;
    private Vector2 _lastPointer;

    public DesktopGestureHandler(Canvas canvas) => _canvas = canvas;

    public void OnMouseDown(MouseButtonEventArgs e, Vector2 mousePos, Vector2i clientSize, MouseState? mouseState = null)
    {
        _lastPointer = mousePos;
        if (_pressed != null) return;
        _pressed = e.Button;
        _canvas.HandleDesktopPointerDown(mousePos.X, mousePos.Y, clientSize.X, clientSize.Y,
            DescribePointer(e.Button, AppoMobi.Gestures.MouseButtonState.Pressed, mouseState));
    }

    public void OnMouseMove(MouseMoveEventArgs e, Vector2 mousePos, bool isButtonDown, Vector2i clientSize, MouseState? mouseState = null)
    {
        _lastPointer = mousePos;
        var pointer = _pressed is { } held
            ? DescribePointer(held, AppoMobi.Gestures.MouseButtonState.Pressed, mouseState)
            : null;
        _canvas.HandleDesktopPointerMove(mousePos.X, mousePos.Y, isButtonDown && _pressed != null, clientSize.X, clientSize.Y, pointer);
    }

    /// <summary>The cursor left the window: hover and pointer-over end.</summary>
    public void OnMouseLeave() => _canvas.HandleDesktopPointerLeave();

    public void OnMouseUp(MouseButtonEventArgs e, Vector2 mousePos, Vector2i clientSize, MouseState? mouseState = null)
    {
        _lastPointer = mousePos;
        if (_pressed != e.Button) return;
        _pressed = null;
        _canvas.HandleDesktopPointerUp(mousePos.X, mousePos.Y, clientSize.X, clientSize.Y,
            DescribePointer(e.Button, AppoMobi.Gestures.MouseButtonState.Released, mouseState));

        // the right button's release is the context-menu request, after its tap, as WPF's ContextMenuOpening
        if (e.Button == MouseButton.Right)
            _canvas.HandleDesktopContextMenu(mousePos.X, mousePos.Y, clientSize.X, clientSize.Y, PointerDeviceType.Mouse);
    }

    /// <summary>
    /// The keyboard's Menu key or Shift+F10: a context-menu request at the last pointer position, carrying no pointer
    /// (that is how <c>SkiaControl.ContextMenuSource</c> tells the keyboard). True when a control took it.
    /// </summary>
    public bool OnContextMenuKey(Vector2i clientSize) =>
        _canvas.HandleDesktopContextMenu(_lastPointer.X, _lastPointer.Y, clientSize.X, clientSize.Y, null);

    /// <summary>
    /// GLFW reports the wheel in notches (+1 away from the user, fractions on precision touchpads);
    /// the canvas takes the Windows convention of 120 per notch. A touchpad swipe reports both axes: the
    /// dominant one goes on, X as a horizontal wheel event (GLFW gives it positive to the left, the same
    /// "toward the start" sign as Y).
    /// </summary>
    public void OnMouseWheel(MouseWheelEventArgs e, Vector2 mousePos, Vector2i clientSize)
    {
        var horizontal = MathF.Abs(e.OffsetX) > MathF.Abs(e.OffsetY);
        var offset = horizontal ? e.OffsetX : e.OffsetY;
        _canvas.HandleDesktopWheel(mousePos.X, mousePos.Y, (int)MathF.Round(offset * 120), clientSize.X, clientSize.Y, horizontal);
    }

    /// <summary>
    /// GLFW button → gesture pointer. Button numbers follow the DOM convention the browser heads use
    /// (0 left, 1 middle, 2 right, 3/4 the side buttons), so app code filters the same way everywhere.
    /// </summary>
    public static PointerData DescribePointer(MouseButton button, AppoMobi.Gestures.MouseButtonState state, MouseState? mouseState)
    {
        var (mapped, number) = button switch
        {
            MouseButton.Left => (AppoMobi.Gestures.MouseButton.Left, 0),
            MouseButton.Middle => (AppoMobi.Gestures.MouseButton.Middle, 1),
            MouseButton.Right => (AppoMobi.Gestures.MouseButton.Right, 2),
            MouseButton.Button4 => (AppoMobi.Gestures.MouseButton.XButton1, 3),
            MouseButton.Button5 => (AppoMobi.Gestures.MouseButton.XButton2, 4),
            _ => (AppoMobi.Gestures.MouseButton.Extended, (int)button),
        };

        var pressed = MouseButtons.None;
        if (mouseState != null)
        {
            if (mouseState.IsButtonDown(MouseButton.Left)) pressed |= MouseButtons.Left;
            if (mouseState.IsButtonDown(MouseButton.Right)) pressed |= MouseButtons.Right;
            if (mouseState.IsButtonDown(MouseButton.Middle)) pressed |= MouseButtons.Middle;
            if (mouseState.IsButtonDown(MouseButton.Button4)) pressed |= MouseButtons.XButton1;
            if (mouseState.IsButtonDown(MouseButton.Button5)) pressed |= MouseButtons.XButton2;
        }

        return new PointerData
        {
            Button = mapped,
            ButtonNumber = number,
            State = state,
            PressedButtons = pressed,
            DeviceType = PointerDeviceType.Mouse,
        };
    }
}
