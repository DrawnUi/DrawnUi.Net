using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Add3dGallery;

/// <summary>
/// The Blazor host of the shared app (src/Shared/Samples/Add3dGallery.Shared): one drawing surface that fills the
/// browser window (see wwwroot/index.html for the full-height CSS). Add <c>?perf</c> to the address to measure the
/// gallery with 100 mobs (results in the browser console).
/// </summary>
public partial class MainPage
{
    readonly GalleryRoot _content = new("Add3D addon to DrawnUI for Blazor");

    [Inject] NavigationManager Navigation { get; set; }

    [Inject] IJSRuntime JS { get; set; }

    Canvas _canvas;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        if (new Uri(Navigation.Uri).Query.Contains("perf", StringComparison.OrdinalIgnoreCase))
            PerfProbe.Start(_content.Gallery, new Uri(Navigation.Uri).Query);
    }

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
        if (!firstRender || _canvas == null)
            return;
        // The loading screen (wwwroot/index.html) fades out with the first frame drawn.
        EventHandler<SkiaDrawingContext> drawn = null;
        drawn = (_, _) =>
        {
            _canvas.WasDrawn -= drawn;
            _ = JS.InvokeVoidAsync("add3dLoaderDone");
        };
        _canvas.WasDrawn += drawn;
    }
}
