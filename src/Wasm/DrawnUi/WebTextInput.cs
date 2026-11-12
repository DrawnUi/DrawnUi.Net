using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DrawnUi.Draw;

/// <summary>
/// The page's text input for a focused <see cref="SkiaEditor"/> (drawnui-web.js): an invisible textarea takes the page
/// focus while an editor is focused, so a phone shows its on-screen keyboard, input methods compose into it and held
/// keys repeat. Its text and editing keys come back here and go out through <see cref="KeyboardManager"/>.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class WebTextInput
{
    [JSImport("textInputAttach", "drawnui-web")]
    private static partial void Attach(
        [JSMarshalAs<JSType.Function<JSType.String>>] Action<string> onText,
        [JSMarshalAs<JSType.Function<JSType.String>>] Action<string> onKey);

    [JSImport("textInputFocus", "drawnui-web")]
    private static partial void FocusTextInput(bool focus, int ownerId);

    static object _owner;

    /// <summary>
    /// <paramref name="editor"/> takes (true) or gives back (false) the keyboard. Giving it back does nothing when another
    /// editor took it meanwhile (a tap from one field to the next). Tab and Shift+Tab move on from the editor's overlay
    /// element, in the overlay's order.
    /// </summary>
    public static void Focus(object editor, bool focus)
    {
        if (focus)
            _owner = editor;
        else if (_owner == editor)
            _owner = null;
        else
            return;
        FocusTextInput(focus, editor is ISkiaAccessibilityNode node ? node.AccessibilityId : -1);
    }

    /// <summary>
    /// A tap at (<paramref name="x"/>, <paramref name="y"/>), in points over the canvas, on an editor: the text input takes
    /// the page focus now, inside the tap's own browser event, which is when iOS Safari raises the on-screen keyboard
    /// (the editor itself takes its focus with the tap's gesture, on a later frame). A tap the editor does not take as
    /// focus gives the page focus back.
    /// </summary>
    internal static void FocusOnTap(Canvas canvas, double x, double y)
    {
        var manager = canvas?.AccessibilityManager;
        if (manager == null)
            return;

        var snapshot = manager.Snapshot;
        for (var i = snapshot.Length - 1; i >= 0; i--)
        {
            var node = snapshot[i];
            if (node.Source is not SkiaEditor editor || !node.CanInteract || manager.IsOutsideModal(editor)
                || !node.Rect.Contains((float)x, (float)y))
                continue;

            if (!editor.IsFocused || !ReferenceEquals(_owner, editor))
            {
                _owner = editor;
                FocusTextInput(true, editor.AccessibilityId);
                _ = GiveBackIfNotTaken(editor);
            }
            return;
        }
    }

    static async Task GiveBackIfNotTaken(SkiaEditor editor)
    {
        await Task.Delay(500);
        if (ReferenceEquals(_owner, editor) && !editor.IsFocused)
            Focus(editor, false);
    }

    internal static void Init() => Attach(OnText, OnKey);

    static void OnText(string text)
    {
        if (!string.IsNullOrEmpty(text))
            KeyboardManager.KeyboardChar(text, false);
    }

    // an editing key a phone keyboard or an input method reports as text input, not as a key press
    static void OnKey(string code)
    {
        var key = KeyboardManager.MapBrowserCode(code);
        KeyboardManager.KeyboardPressed(key, false);
        KeyboardManager.KeyboardReleased(key, false);
    }
}
