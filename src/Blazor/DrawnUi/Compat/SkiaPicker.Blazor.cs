using Microsoft.JSInterop;

namespace DrawnUi.Controls;

public partial class SkiaPicker
{
    private static readonly string PickerModulePath = $"./_content/DrawnUi.Blazor.Core/drawnui-picker.js?v={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    private static IJSObjectReference? _pickerModule;

    private async partial Task<int?> ShowSelectionAsyncPlatform(string title, string cancelText, IReadOnlyList<string> options, int selectedIndex)
    {
        var jsRuntime = Super.Services?.GetService(typeof(IJSRuntime)) as IJSRuntime;
        if (jsRuntime == null)
        {
            return null;
        }

        _pickerModule ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", PickerModulePath);
        return await _pickerModule.InvokeAsync<int>("showPickerPrompt", title, cancelText, options.ToArray(), selectedIndex, UsingControlStyle.ToString());
    }
}
