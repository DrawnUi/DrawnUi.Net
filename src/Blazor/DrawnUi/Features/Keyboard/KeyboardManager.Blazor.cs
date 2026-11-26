using Microsoft.AspNetCore.Components;
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
        // the page calls back through this reference: a static call cannot pick a runtime when the page runs
        // several (Blazor Server + WebAssembly)
        _bridge ??= DotNetObjectReference.Create(new KeyboardJsBridge());
        await _module.InvokeVoidAsync("attachGlobalKeyboard", _bridge);
        _attached = true;
    }

    private static DotNetObjectReference<KeyboardJsBridge>? _bridge;

    /// <summary>
    /// The object the page's keyboard script calls back into; forwards to the static handlers.
    /// </summary>
    public sealed class KeyboardJsBridge
    {
        /// <summary>See <see cref="KeyboardManager.HandleSoftKeyboard"/>.</summary>
        [JSInvokable] public void HandleSoftKeyboard(bool soft) => KeyboardManager.HandleSoftKeyboard(soft);
        /// <summary>See <see cref="KeyboardManager.HandleKeyboardTop"/>.</summary>
        [JSInvokable] public void HandleKeyboardTop(double top) => KeyboardManager.HandleKeyboardTop(top);
        /// <summary>See <see cref="KeyboardManager.HandleTextInput"/>.</summary>
        [JSInvokable] public void HandleTextInput(string? text) => KeyboardManager.HandleTextInput(text);
        /// <summary>See <see cref="KeyboardManager.HandleTextKey"/>.</summary>
        [JSInvokable] public void HandleTextKey(string? code) => KeyboardManager.HandleTextKey(code);
        /// <summary>See <see cref="KeyboardManager.HandleGlobalKeyDown"/>.</summary>
        [JSInvokable] public void HandleGlobalKeyDown(string? code, bool forOtherElement) => KeyboardManager.HandleGlobalKeyDown(code, forOtherElement);
        /// <summary>See <see cref="KeyboardManager.HandleGlobalKeyUp"/>.</summary>
        [JSInvokable] public void HandleGlobalKeyUp(string? code, bool forOtherElement) => KeyboardManager.HandleGlobalKeyUp(code, forOtherElement);
        /// <summary>See <see cref="KeyboardManager.HandleGlobalKeyChar"/>.</summary>
        [JSInvokable] public void HandleGlobalKeyChar(string? ch, bool forOtherElement) => KeyboardManager.HandleGlobalKeyChar(ch, forOtherElement);
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

    /// <summary>A tap gave the on-screen keyboard to a text field (true), or a mouse or Tab focused one (false).</summary>
    [JSInvokable]
    public static void HandleSoftKeyboard(bool soft) => SoftKeyboardInput = soft;

    /// <summary>The on-screen keyboard's top in page points, -1 when none (from the browser's visual viewport).</summary>
    [JSInvokable]
    public static void HandleKeyboardTop(double top) => Super.SetKeyboardTop(top < 0 ? double.NaN : top);

    // An element's bottom edge in page points, NaN when unknown. Synchronous: Blazor runs in WebAssembly.
    internal static double ElementBottom(ElementReference element)
    {
        try
        {
            return _module is IJSInProcessObjectReference module ? module.Invoke<double>("elementBottom", element) : double.NaN;
        }
        catch
        {
            return double.NaN;
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
