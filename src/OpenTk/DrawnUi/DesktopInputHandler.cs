using DrawnUi.Views;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace DrawnUi.OpenTk;

public class DesktopInputHandler
{
    private readonly Canvas _canvas;

    public DesktopInputHandler(Canvas canvas) => _canvas = canvas;

    public void OnTextInput(TextInputEventArgs e)
    {
        var value = e.AsString;
        if (string.IsNullOrEmpty(value) || value == "\r" || value == "\n" || value == "\t") return;
        _canvas.HandleDesktopTextInput(value);
    }

    public void OnKeyDown(KeyboardKeyEventArgs e, KeyboardState keyboardState)
    {
        var shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
        var ctrl = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
        switch (e.Key)
        {
            case Keys.Backspace: _canvas.DesktopEditorBackspace(); break;
            case Keys.Delete: _canvas.DesktopEditorDelete(); break;
            case Keys.Enter: _canvas.DesktopEditorEnter(); break;
            case Keys.Left: _canvas.DesktopEditorMoveCursor(-1, shift); break;
            case Keys.Right: _canvas.DesktopEditorMoveCursor(1, shift); break;
            case Keys.Home: _canvas.DesktopEditorMoveToStart(shift); break;
            case Keys.End: _canvas.DesktopEditorMoveToEnd(shift); break;
            case Keys.A when ctrl: _canvas.DesktopEditorSelectAll(); break;
            case Keys.Tab: _canvas.HandleDesktopTextInput("    "); break;
        }
    }
}
