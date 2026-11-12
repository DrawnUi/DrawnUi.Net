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
                    Content = new GalleryRoot("The Add3d add-on for .NET MAUI").Assign(out _root),
                }.Assign(out _canvas)
            }
        };
        // a field focused while the keyboard shows (Tab, a tap on the next field) is scrolled above it too
        _canvas.FocusedItemChanged += (_, _) => FollowKeyboard();
        if (!string.IsNullOrEmpty(PerfRequest))
            PerfProbe.Start(_root.Gallery, "perf=" + PerfRequest);
    }

    /// <inheritdoc />
    public override double OnKeyboardResized(double size)
    {
        var height = base.OnKeyboardResized(size);
        FollowKeyboard(height);
        return height;
    }

    /// <summary>
    /// The focused field stays above the on-screen keyboard: the gallery's scroll adapts to it. On iOS KeyboardSize is
    /// the keyboard's raw height, down to the screen's bottom, and the page's content ends above the bottom inset (the
    /// home indicator), so the part over the content is KeyboardSize minus that inset; Android's KeyboardSize already
    /// leaves the inset out.
    /// </summary>
    void FollowKeyboard(double? height = null)
    {
        var keyboard = height ?? KeyboardSize;
#if IOS
        keyboard = Math.Max(0, keyboard - Super.Screen.BottomInset);
#endif
        _root.Gallery.OnKeyboard(keyboard, _canvas.FocusedChild as SkiaControl);
    }
}
