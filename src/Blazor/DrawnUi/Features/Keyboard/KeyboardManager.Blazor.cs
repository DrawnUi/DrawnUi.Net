using Microsoft.JSInterop;

namespace DrawnUi.Draw;

public partial class KeyboardManager
{
    private static IJSObjectReference? _module;
    private static bool _attached;

    private static readonly string KeyboardModulePath = $"./_content/DrawnUi.Blazor.Core/drawnui-keyboard.js?v={System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

    public static async Task AttachToKeyboardAsync(IJSRuntime jsRuntime)
    {
        if (_attached)
        {
            return;
        }

        _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", KeyboardModulePath);
        await _module.InvokeVoidAsync("attachGlobalKeyboard");
        _attached = true;
    }

    // Drops DOM focus from an external page text input (e.g. a Monaco code editor) so the
    // browser stops delivering physical keys to it once a drawn SkiaEditor is focused.
    // Fire-and-forget; no-op before the keyboard module is imported.
    public static void BlurExternalTextInput()
    {
        var module = _module;
        if (module is null)
            return;

        _ = module.InvokeVoidAsync("blurExternalTextInput").AsTask()
            .ContinueWith(static _ => { }, TaskContinuationOptions.OnlyOnFaulted);
    }

    // A drawn editor took or gave back its focus: giving it back closes the phone's on-screen keyboard (the hidden
    // textarea a tap on a text field focused, see drawnui-keyboard.js). Fire-and-forget.
    internal static void TextInputFocus(bool focus, int ownerId)
    {
        var module = _module;
        if (module is null)
            return;

        _ = module.InvokeVoidAsync("textInputFocus", focus, ownerId).AsTask()
            .ContinueWith(static _ => { }, TaskContinuationOptions.OnlyOnFaulted);
    }

    // The hidden textarea has the page focus (a tap opened the on-screen keyboard): the editor's overlay element must
    // not take it, that would close the keyboard. Synchronous: Blazor runs in WebAssembly, in the page's thread.
    internal static bool TextInputHasFocus()
    {
        try
        {
            return _module is IJSInProcessObjectReference module && module.Invoke<bool>("textInputHasFocus");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Text the on-screen keyboard or an input method typed into a focused drawn editor.</summary>
    [JSInvokable]
    public static void HandleTextInput(string? text)
    {
        if (!string.IsNullOrEmpty(text))
            KeyboardChar(text, false);
    }

    /// <summary>An editing key (DOM code: Backspace, Delete, Enter) the on-screen keyboard reported as text input.</summary>
    [JSInvokable]
    public static void HandleTextKey(string? code)
    {
        var key = MapToMaui(code);
        KeyboardPressed(key, false);
        KeyboardReleased(key, false);
    }

    /// <param name="code">DOM <c>KeyboardEvent.code</c>.</param>
    /// <param name="forOtherElement">A page element outside the canvas has the key, see <see cref="KeyboardManager.IsKeyForOtherElement"/>.</param>
    [JSInvokable]
    public static void HandleGlobalKeyDown(string? code, bool forOtherElement)
    {
        KeyboardPressed(MapToMaui(code), forOtherElement);
    }

    [JSInvokable]
    public static void HandleGlobalKeyUp(string? code, bool forOtherElement)
    {
        KeyboardReleased(MapToMaui(code), forOtherElement);
    }

    [JSInvokable]
    public static void HandleGlobalKeyChar(string? ch, bool forOtherElement)
    {
        if (!string.IsNullOrEmpty(ch))
            KeyboardChar(ch, forOtherElement);
    }

    public static InputKey MapToMaui(string? code) => MapBrowserCode(code);
}
