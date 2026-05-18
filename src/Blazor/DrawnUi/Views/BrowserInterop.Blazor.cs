using System.Runtime.InteropServices.JavaScript;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DrawnUi.Views;

public static partial class BrowserApi
{
    private static readonly string ResizeModulePath = $"./_content/DrawnUi.Blazor.Core/drawnui-canvas-resize.js?v={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    private static IJSObjectReference? _resizeModule;

    public static void ReloadPage(bool forceReload = true)
    {
        try
        {
            var navigation = Super.Services?.GetService(typeof(NavigationManager)) as NavigationManager;
            navigation?.Refresh(forceReload);
        }
        catch
        {
        }
    }

    public static bool IsMobileBrowser()
    {
        try
        {
            return BrowserRuntimeInterop.IsMobileBrowser();
        }
        catch
        {
            return false;
        }
    }

    public static async ValueTask<bool> IsFullscreenEnabledAsync(ElementReference hostElement)
    {
        if (!HasElementReferenceContext(hostElement))
        {
            return false;
        }

        var module = await GetResizeModuleAsync();
        if (module == null)
        {
            return false;
        }

        try
        {
            return await module.InvokeAsync<bool>("isCanvasFullscreen", hostElement);
        }
        catch
        {
            return false;
        }
    }

    public static async ValueTask<bool> SetFullscreenEnabledAsync(ElementReference hostElement, bool enabled)
    {
        if (!HasElementReferenceContext(hostElement))
        {
            return false;
        }

        var module = await GetResizeModuleAsync();
        if (module == null)
        {
            return false;
        }

        try
        {
            return await module.InvokeAsync<bool>("setCanvasFullscreen", hostElement, enabled);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasElementReferenceContext(ElementReference hostElement)
        => !string.IsNullOrWhiteSpace(hostElement.Context?.ToString());

    private static async ValueTask<IJSObjectReference?> GetResizeModuleAsync()
    {
        if (_resizeModule != null)
        {
            return _resizeModule;
        }

        var jsRuntime = Super.Services?.GetService(typeof(IJSRuntime)) as IJSRuntime;
        if (jsRuntime == null)
        {
            return null;
        }

        _resizeModule = await jsRuntime.InvokeAsync<IJSObjectReference>("import", ResizeModulePath);
        return _resizeModule;
    }
}

internal static partial class BrowserRuntimeInterop
{
    [JSImport("globalThis.DrawnUiBrowser.isMobileBrowser")]
    internal static partial bool IsMobileBrowser();
}