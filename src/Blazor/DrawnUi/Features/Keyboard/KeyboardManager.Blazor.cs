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

    [JSInvokable]
    public static void HandleGlobalKeyDown(string? code)
    {
        KeyboardPressed(MapToMaui(code));
    }

    [JSInvokable]
    public static void HandleGlobalKeyUp(string? code)
    {
        KeyboardReleased(MapToMaui(code));
    }

    [JSInvokable]
    public static void HandleGlobalKeyChar(string? ch)
    {
        if (!string.IsNullOrEmpty(ch))
            KeyboardChar(ch);
    }

    public static InputKey MapToMaui(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return InputKey.Unknown;
        }

        return Enum.TryParse<InputKey>(code, ignoreCase: false, out var mapped)
            ? mapped
            : InputKey.Unknown;
    }
}
