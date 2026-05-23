using DrawnUi.Views;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using MouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;
using Vector2 = OpenTK.Mathematics.Vector2;
using Vector2i = OpenTK.Mathematics.Vector2i;

namespace DrawnUi.OpenTk;

public class DesktopGestureHandler
{
    private readonly Canvas _canvas;

    public DesktopGestureHandler(Canvas canvas) => _canvas = canvas;

    public void OnMouseDown(MouseButtonEventArgs e, Vector2 mousePos, Vector2i clientSize)
    {
        if (e.Button != MouseButton.Left) return;
        _canvas.HandleDesktopPointerDown(mousePos.X, mousePos.Y, clientSize.X, clientSize.Y);
    }

    public void OnMouseMove(MouseMoveEventArgs e, Vector2 mousePos, bool isButtonDown, Vector2i clientSize)
    {
        _canvas.HandleDesktopPointerMove(mousePos.X, mousePos.Y, isButtonDown, clientSize.X, clientSize.Y);
    }

    public void OnMouseUp(MouseButtonEventArgs e, Vector2 mousePos, Vector2i clientSize)
    {
        if (e.Button != MouseButton.Left) return;
        _canvas.HandleDesktopPointerUp(mousePos.X, mousePos.Y, clientSize.X, clientSize.Y);
    }
}
