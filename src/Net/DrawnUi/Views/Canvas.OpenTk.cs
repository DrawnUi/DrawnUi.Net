using AppoMobi.Gestures;
using System.Drawing;

namespace DrawnUi.Views;

public partial class Canvas
{
    private TouchActionEventArgs? _desktopPointerDownArgs;
    private TouchActionEventArgs? _desktopPreviousArgs;
    private const long DesktopPointerId = 1;

    public void ConnectDesktopDrawable(ISkiaDrawable drawable)
    {
        AttachCanvasView(drawable);
        ConnectedHandler();
    }

    public void HandleDesktopPointerDown(float x, float y, float clientW, float clientH)
    {
        var location = new PointF(x, y);
        var args = MakeDesktopTouchArgs(TouchActionType.Pressed, location, clientW, clientH);
        args.IsInContact = true;
        args.Distance = new TouchActionEventArgs.DistanceInfo();
        _desktopPointerDownArgs = args;
        _desktopPreviousArgs = args;
        OnGestureEvent(TouchActionType.Pressed, args, TouchActionResult.Down);
    }

    public void HandleDesktopPointerMove(float x, float y, bool isDragging, float clientW, float clientH)
    {
        var location = new PointF(x, y);
        var actionType = isDragging ? TouchActionType.Moved : TouchActionType.Pointer;
        var args = MakeDesktopTouchArgs(actionType, location, clientW, clientH);

        if (_desktopPreviousArgs != null)
            TouchActionEventArgs.FillDistanceInfo(args, _desktopPreviousArgs);

        if (actionType == TouchActionType.Pointer)
        {
            OnGestureEvent(actionType, args, TouchActionResult.Pointer);
            return;
        }

        if (args.Distance.Delta.X != 0 || args.Distance.Delta.Y != 0)
            OnGestureEvent(actionType, args, TouchActionResult.Panning);

        _desktopPreviousArgs = args;
    }

    public void HandleDesktopPointerUp(float x, float y, float clientW, float clientH)
    {
        if (_desktopPointerDownArgs == null)
            return;

        var location = new PointF(x, y);
        var args = MakeDesktopTouchArgs(TouchActionType.Released, location, clientW, clientH);
        args.IsInContact = false;

        if (_desktopPreviousArgs != null)
            TouchActionEventArgs.FillDistanceInfo(args, _desktopPreviousArgs);

        var threshold = TouchEffect.TappedCancelMoveThresholdPoints * Math.Max(0.1f, TouchEffect.Density);
        if (Math.Abs(args.Distance.Total.X) < threshold && Math.Abs(args.Distance.Total.Y) < threshold)
            OnGestureEvent(TouchActionType.Released, args, TouchActionResult.Tapped);

        OnGestureEvent(TouchActionType.Released, args, TouchActionResult.Up);
        _desktopPointerDownArgs = null;
        _desktopPreviousArgs = null;
    }

    public void HandleDesktopTextInput(string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        if (FocusedChild is SkiaEditor editor)
        {
            editor.StubTypeText(value);
            Repaint();
        }
    }

    public void DesktopEditorBackspace() => EditorAction(e => e.StubBackspace());
    public void DesktopEditorDelete() => EditorAction(e => e.StubDelete());
    public void DesktopEditorEnter() => EditorAction(e => e.StubPressEnter());
    public void DesktopEditorMoveCursor(int delta, bool select) => EditorAction(e => e.StubMoveCursor(delta, select));
    public void DesktopEditorMoveToStart(bool select) => EditorAction(e => e.StubMoveCursor(-e.CursorPosition, select));
    public void DesktopEditorMoveToEnd(bool select) => EditorAction(e => e.StubMoveCursor((e.Text?.Length ?? 0) - e.CursorPosition, select));
    public void DesktopEditorSelectAll() => EditorAction(e => e.StubSelectAll());

    private void EditorAction(Action<SkiaEditor> action)
    {
        if (FocusedChild is SkiaEditor editor)
        {
            action(editor);
            Repaint();
        }
    }

    private TouchActionEventArgs MakeDesktopTouchArgs(TouchActionType type, PointF location, float clientW, float clientH)
    {
        var args = new TouchActionEventArgs(
            DesktopPointerId,
            type,
            location,
            null,
            (float)Math.Max(0.1, RenderingScale));

        args.IsInsideView = location.X >= 0 && location.Y >= 0
            && location.X <= clientW && location.Y <= clientH;
        args.NumberOfTouches = 1;
        args.StartingLocation = _desktopPointerDownArgs?.StartingLocation ?? location;
        return args;
    }
}
