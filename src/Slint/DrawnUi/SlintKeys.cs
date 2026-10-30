using DrawnUi.Draw;
using DrawnUi.Views;

namespace DrawnUi.Slint;

/// <summary>
/// Slint's key events for DrawnUI: editor actions and text for a focused <c>SkiaEditor</c>, and
/// <see cref="KeyboardManager"/> events for <c>OnKeyDown</c> handlers. Slint sends special keys as the private-use
/// characters of <c>internal/common/key_codes.rs</c>.
/// </summary>
internal static class SlintKeys
{
    private const string Backspace = "\u0008", Tab = "\u0009", Return = "\u000a", Escape = "\u001b", Delete = "\u007f";
    private const string Up = "", Down = "", Left = "", Right = "";
    private const string Home = "", End = "", PageUp = "", PageDown = "";

    public static bool Pressed(Canvas canvas, string text, bool shift, bool control, bool alt)
    {
        if (ToInputKey(text) is { } key)
            KeyboardManager.KeyboardPressed(key);

        switch (text)
        {
            case Backspace: canvas.DesktopEditorBackspace(); return true;
            case Delete: canvas.DesktopEditorDelete(); return true;
            case Return: canvas.DesktopEditorEnter(alt, shift); return true;
            case Left: canvas.DesktopEditorMoveCursor(-1, shift); return true;
            case Right: canvas.DesktopEditorMoveCursor(1, shift); return true;
            case Home: canvas.DesktopEditorMoveToStart(shift); return true;
            case End: canvas.DesktopEditorMoveToEnd(shift); return true;
            // Like the OpenTK head's CanvasHost, which has no Tab navigation of its own.
            case Tab: canvas.HandleDesktopTextInput("    "); return true;
        }

        if (control)
        {
            if (string.Equals(text, "a", StringComparison.OrdinalIgnoreCase))
            {
                canvas.DesktopEditorSelectAll();
                return true;
            }
            return false;
        }

        if (IsPrintable(text))
        {
            canvas.HandleDesktopTextInput(text);
            return true;
        }
        return ToInputKey(text) != null;
    }

    public static bool Released(string text)
    {
        if (ToInputKey(text) is not { } key)
            return false;
        KeyboardManager.KeyboardReleased(key);
        return true;
    }

    private static bool IsPrintable(string text) =>
        text.Length > 0 && !char.IsControl(text[0]) && !(text[0] >= '' && text[0] <= '');

    private static InputKey? ToInputKey(string text)
    {
        switch (text)
        {
            case Backspace: return InputKey.Backspace;
            case Tab: return InputKey.Tab;
            case Return: return InputKey.Enter;
            case Escape: return InputKey.Escape;
            case Delete: return InputKey.Delete;
            case " ": return InputKey.Space;
            case Up: return InputKey.ArrowUp;
            case Down: return InputKey.ArrowDown;
            case Left: return InputKey.ArrowLeft;
            case Right: return InputKey.ArrowRight;
            case Home: return InputKey.Home;
            case End: return InputKey.End;
            case PageUp: return InputKey.PageUp;
            case PageDown: return InputKey.PageDown;
        }
        if (text.Length == 1)
        {
            var c = char.ToUpperInvariant(text[0]);
            if (c is >= 'A' and <= 'Z')
                return InputKey.KeyA + (c - 'A');
            if (c is >= '0' and <= '9')
                return InputKey.Digit0 + (c - '0');
        }
        return null;
    }
}
