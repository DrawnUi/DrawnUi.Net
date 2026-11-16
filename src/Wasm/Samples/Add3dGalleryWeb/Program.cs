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
            var canvas = new Canvas
            {
                BackgroundColor = Theme.Page,
                RenderingMode = RenderingModeType.Accelerated,
                Gestures = GesturesMode.Enabled,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Content = root,
            };
            // The loading screen (wwwroot/index.html) fades out with the first frame drawn.
            EventHandler<SkiaDrawingContext> drawn = null;
            drawn = (_, _) =>
            {
                canvas.WasDrawn -= drawn;
                LoaderDone();
            };
            canvas.WasDrawn += drawn;
            // The sign-up form on a phone: the focused field is scrolled above the on-screen keyboard, also when the
            // focus moves to the next field while it shows (as the MAUI head does with DrawnUiBasePage.KeyboardSize).
            double keyboard = 0;
            void Follow() => root.Gallery.OnKeyboard(keyboard, canvas.FocusedChild as SkiaControl);
            canvas.FocusedItemChanged += (_, _) => Follow();
            WatchKeyboard(height =>
            {
                keyboard = height;
                Follow();
            });
            return canvas;
        });

    [JSImport("globalThis.add3dLoaderDone")]
    static partial void LoaderDone();

    [JSImport("globalThis.add3dWatchKeyboard")]
    static partial void WatchKeyboard([JSMarshalAs<JSType.Function<JSType.Number>>] Action<double> onChange);
}
