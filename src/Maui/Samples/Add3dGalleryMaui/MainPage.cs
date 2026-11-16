namespace Add3dGallery;

/// <summary>The MAUI host of the shared app (src/Shared/Samples/Add3dGallery.Shared): one canvas filling the window.
/// DrawnUiBasePage hands it the Mac keyboard and the on-screen keyboard's height on phones (the sign-up form).</summary>
public class MainPage : DrawnUiBasePage
{
    readonly Canvas _canvas;
    readonly GalleryRoot _root;

    /// <summary>A PerfProbe run to start (globe, globechip, mobs...): the ADD3D_PERF environment variable on the desktop,
    /// the "perf" intent extra on Android (adb shell am start ... --es perf globechip). Results go to the console / logcat.</summary>
    public static string PerfRequest = Environment.GetEnvironmentVariable("ADD3D_PERF");

    /// <summary>Builds the page.</summary>
    public MainPage()
    {
        BackgroundColor = Theme.Page;
        Content = new Grid // MAUI root wrapper, needed for iOS safe insets
        {
            Children =
            {
                new Canvas
                {
                    BackgroundColor = Theme.Page,
                    RenderingMode = RenderingModeType.Accelerated,
                    Gestures = GesturesMode.Enabled,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new GalleryRoot("Add3D addon to DrawnUI for .NET MAUI", "src/Maui/Samples/Add3dGalleryMaui").Assign(out _root),
                }.Assign(out _canvas)
            }
        };
        if (!string.IsNullOrEmpty(PerfRequest))
            PerfProbe.Start(_root.Gallery, "perf=" + PerfRequest);
    }

}
