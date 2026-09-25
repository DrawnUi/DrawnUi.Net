namespace EmptyCode;

/// <summary>
/// The whole app: one window hosting one drawing surface.
/// </summary>
public class MainWindow : System.Windows.Window
{
    private readonly DrawnUiElement _drawn;

    public MainWindow()
    {
        Title = "DrawnApp";

        // Phone-sized, so the layout matches mobile. Change freely: the canvas fills the window.
        Width = 375;
        Height = 750;

        // WPF window background, visible only before the first frame. Keep it equal to the canvas background.
        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#0B0E14");

        _drawn = new DrawnUiElement()
        {
            Gestures = GesturesMode.Lock,
            RenderingMode = RenderingModeType.Accelerated,
        };
        _drawn.Canvas.BackgroundColor = Color.Parse("#0B0E14");
        _drawn.Content = CreateContent();

        Content = _drawn;
    }

    /// <summary>
    /// Builds what the canvas draws: your content plus the debug overlay.
    /// </summary>
    private SkiaControl CreateContent()
    {
        return new SkiaLayer()
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                //YOUR MAIN CONTENT
                CreateMainContent(),

#if DEBUG
                new SkiaLabelFps()
                {
                    Margin = new Thickness(0, 0, 4, 24),
                    VerticalOptions = LayoutOptions.End,
                    HorizontalOptions = LayoutOptions.End,
                    Rotation = -45,
                    FontSize = 11,
                    BackgroundColor = Colors.DarkRed,
                    TextColor = Colors.White,
                    ZIndex = 110,
                },
#endif
            }
        };
    }

    // <fiddle:content>
    // Everything between these two markers is the exported UI: a method body that returns one
    // SkiaControl. It is the same contract as a DrawnUI Fiddle snippet, so a snippet pastes in
    // whole, and the Fiddle "Export WPF" button replaces this region verbatim.
    /// <summary>
    /// Your UI starts here. Replace the body with your own tree.
    /// </summary>
    protected SkiaControl CreateMainContent()
    {
        var taps = 0;
        SkiaLabel counter = null;

        return new SkiaStack()
        {
            UseCache = SkiaCacheType.Image,
            Spacing = 16,
            Padding = new Thickness(24),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new SkiaSvg()
                {
                    Source = "drawnui.svg",
                    TintColor = Color.Parse("#4C8DFF"),
                    WidthRequest = 64,
                    HeightRequest = 64,
                    HorizontalOptions = LayoutOptions.Center,
                },

                new SkiaLabel("DrawnUI")
                {
                    FontFamily = "FontTextTitle",
                    FontSize = 28,
                    TextColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Center,
                },

                new SkiaLabel("Everything here is drawn on one canvas.")
                {
                    FontFamily = "FontText",
                    FontSize = 14,
                    TextColor = Color.Parse("#A9B4C6"),
                    HorizontalTextAlignment = DrawTextAlignment.Center,
                    HorizontalOptions = LayoutOptions.Center,
                },

                new SkiaLabel("Nothing tapped yet")
                    {
                        FontFamily = "FontText",
                        FontSize = 14,
                        TextColor = Color.Parse("#4C8DFF"),
                        HorizontalOptions = LayoutOptions.Center,
                    }
                    .Assign(out counter),

                new SkiaButton("Tap me")
                    {
                        UseCache = SkiaCacheType.Image,
                        BackgroundColor = Color.Parse("#4C8DFF"),
                        TextColor = Colors.White,
                        CornerRadius = 10,
                        WidthRequest = 200,
                        HeightRequest = 44,
                        HorizontalOptions = LayoutOptions.Center,
                    }
                    .OnTapped(async me =>
                    {
                        taps++;
                        counter.Text = taps == 1 ? "Tapped once" : $"Tapped {taps} times";
                        await me.ScaleToAsync(0.96, 0.96, 60);
                        await me.ScaleToAsync(1, 1, 60);
                    }),
            }
        };
    }
    // </fiddle:content>

    protected override void OnClosed(EventArgs e)
    {
        _drawn.Dispose();
        base.OnClosed(e);
    }
}
