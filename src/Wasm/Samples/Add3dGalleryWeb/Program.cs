namespace Add3dGallery;

/// <summary>
/// The pure WebAssembly host of the shared app (src/Shared/Samples/Add3dGallery.Shared): one canvas filling the
/// browser window. Add <c>?perf</c> to the address to measure the gallery with 100 mobs (results in the console).
/// </summary>
public static partial class Program
{
    [JSExport]
    public static Task Main() => Super.UseDrawnUi()
        // Labels without a FontFamily draw in FontText.
        .ConfigureStyles(styles => styles.AddStyle(new Style
        {
            ApplyToDerivedTypes = true,
            TargetType = typeof(SkiaLabel),
            Setters = { new Setter { Property = SkiaLabel.FontFamilyProperty, Value = "FontText" } },
        }))
        .ConfigureFonts(fonts =>
        {
            fonts.AddSymbols();
            fonts.AddFont("fonts/OpenSans-Regular.ttf", "FontText");
            fonts.AddFont("fonts/OpenSans-Semibold.ttf", "FontTextTitle");
        })
        .RunAsync("drawnui-canvas", () =>
        {
            var root = new GalleryRoot("Add3D addon to DrawnUI for WebAssembly");
            var query = JSHost.GlobalThis.GetPropertyAsJSObject("location").GetPropertyAsString("search") ?? "";
            if (query.Contains("perf", StringComparison.OrdinalIgnoreCase))
                PerfProbe.Start(root.Gallery, query);
            return new Canvas
            {
                BackgroundColor = Theme.Page,
                RenderingMode = RenderingModeType.Accelerated,
                Gestures = GesturesMode.Enabled,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Content = root,
            };
        });
}
