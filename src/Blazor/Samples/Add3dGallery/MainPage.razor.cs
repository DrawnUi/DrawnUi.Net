using Microsoft.AspNetCore.Components;

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

    protected override void OnInitialized()
    {
        base.OnInitialized();
        if (new Uri(Navigation.Uri).Query.Contains("perf", StringComparison.OrdinalIgnoreCase))
            PerfProbe.Start(_content.Gallery, new Uri(Navigation.Uri).Query);
    }
}
