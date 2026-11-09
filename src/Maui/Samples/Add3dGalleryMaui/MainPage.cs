namespace Add3dGallery;

/// <summary>The MAUI host of the shared app (src/Shared/Samples/Add3dGallery.Shared): one canvas filling the window.</summary>
public class MainPage : ContentPage
{
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
                    Content = new GalleryRoot("The Add3d add-on for .NET MAUI"),
                }
            }
        };
    }
}
