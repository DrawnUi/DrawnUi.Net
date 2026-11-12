namespace Add3dGallery;

/// <summary>The MAUI host of the shared app (src/Shared/Samples/Add3dGallery.Shared): one canvas filling the window.
/// DrawnUiBasePage hands it the Mac keyboard and the on-screen keyboard's height on phones (the sign-up form).</summary>
public class MainPage : DrawnUiBasePage
{
    readonly Canvas _canvas;
    readonly GalleryRoot _root;

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
    }

    /// <inheritdoc />
    public override double OnKeyboardResized(double size)
    {
        var height = base.OnKeyboardResized(size);
        FollowKeyboard(height);
        return height;
    }

    /// <summary>The focused field stays above the on-screen keyboard: the gallery's scroll adapts to it.</summary>
    void FollowKeyboard(double? height = null) =>
        _root.Gallery.OnKeyboard(height ?? KeyboardSize, _canvas.FocusedChild as SkiaControl);
}
