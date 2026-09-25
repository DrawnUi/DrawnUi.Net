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
/// only wants the primary button filters on <c>args.Event.Pointer.Button</c>; a right click also
/// reaches <c>SkiaControl.ContextMenu</c> the usual way. One button owns the pointer at a time: a
/// second button pressed while the first is held is ignored until the first is released.
/// </summary>
public class DesktopGestureHandler
{
    private readonly Canvas _canvas;
    private MouseButton? _pressed;

    public DesktopGestureHandler(Canvas canvas) => _canvas = canvas;

    public void OnMouseDown(MouseButtonEventArgs e, Vector2 mousePos, Vector2i clientSize, MouseState? mouseState = null)
    {
        if (_pressed != null) return;
        _pressed = e.Button;
        _canvas.HandleDesktopPointerDown(mousePos.X, mousePos.Y, clientSize.X, clientSize.Y,
            DescribePointer(e.Button, AppoMobi.Gestures.MouseButtonState.Pressed, mouseState));
    }

    public void OnMouseMove(MouseMoveEventArgs e, Vector2 mousePos, bool isButtonDown, Vector2i clientSize, MouseState? mouseState = null)
    {
        var pointer = _pressed is { } held
            ? DescribePointer(held, AppoMobi.Gestures.MouseButtonState.Pressed, mouseState)
            : null;
        _canvas.HandleDesktopPointerMove(mousePos.X, mousePos.Y, isButtonDown && _pressed != null, clientSize.X, clientSize.Y, pointer);
    }

    public void OnMouseUp(MouseButtonEventArgs e, Vector2 mousePos, Vector2i clientSize, MouseState? mouseState = null)
    {
        if (_pressed != e.Button) return;
        _pressed = null;
        _canvas.HandleDesktopPointerUp(mousePos.X, mousePos.Y, clientSize.X, clientSize.Y,
            DescribePointer(e.Button, AppoMobi.Gestures.MouseButtonState.Released, mouseState));
    }

    /// <summary>
    /// GLFW reports the wheel in notches (+1 away from the user, fractions on precision touchpads);
    /// the canvas takes the Windows convention of 120 per notch.
    /// </summary>
    public void OnMouseWheel(MouseWheelEventArgs e, Vector2 mousePos, Vector2i clientSize)
    {
        _canvas.HandleDesktopWheel(mousePos.X, mousePos.Y, (int)MathF.Round(e.OffsetY * 120), clientSize.X, clientSize.Y);
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
