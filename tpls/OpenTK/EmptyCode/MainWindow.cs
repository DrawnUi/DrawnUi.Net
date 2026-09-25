using System.Runtime.Versioning;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;

namespace EmptyCode;

/// <summary>
/// The whole app: one OpenGL window hosting one drawing surface. <see cref="DrawnUiWindow"/> owns
/// the GPU surface, mouse and text input, F11 for fullscreen and ESC to leave it.
/// </summary>
public class MainWindow(GameWindowSettings gameSettings, NativeWindowSettings nativeSettings)
    : DrawnUiWindow(gameSettings, nativeSettings, CreateCanvas())
{
    private static Canvas CreateCanvas() => new Canvas()
    {
        Gestures = GesturesMode.Lock,
        RenderingMode = RenderingModeType.Accelerated,
        UpdateMode = UpdateModeType.Constant,
        BackgroundColor = Color.Parse("#0B0E14"),
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
        Content = CreateContent()
    };

    /// <summary>
    /// Builds what the canvas draws: your content plus the debug overlay.
    /// </summary>
    private static SkiaControl CreateContent()
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
    // whole, and the Fiddle "Export .NET" button replaces this region verbatim.
    /// <summary>
    /// Your UI starts here. Replace the body with your own tree.
    /// </summary>
    protected static SkiaControl CreateMainContent()
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

    /// <summary>Windows 11 title bar and border in the canvas color.</summary>
    [SupportedOSPlatform("windows")]
    protected override void ConfigureWindowChrome(IntPtr hwnd)
    {
        var c = Color.Parse("#0B0E14");
        var (r, g, b) = ((byte)(c.Red * 255), (byte)(c.Green * 255), (byte)(c.Blue * 255));
        WindowChrome.SetCaptionColor(hwnd, r, g, b);
        WindowChrome.SetBorderColor(hwnd, r, g, b);
    }

    // Hardware keys reach KeyboardManager, as in the fiddle (games, shortcuts).
    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (OpenTkKeyMapper.Map(e.Key) is { } key)
            KeyboardManager.KeyboardPressed(key);
    }

    protected override void OnKeyUp(KeyboardKeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (OpenTkKeyMapper.Map(e.Key) is { } key)
            KeyboardManager.KeyboardReleased(key);
    }

    /// <summary>
    /// Title-bar icon from the embedded icon.ico: 32x32 straight (unpremultiplied) RGBA, the format GLFW wants.
    /// The pixel format is requested explicitly: Skia's platform default is not the same everywhere.
    /// </summary>
    public static WindowIcon LoadIcon()
    {
        try
        {
            using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("icon.ico");
            using var bitmap = SKBitmap.Decode(stream);
            using var icon = bitmap.Resize(new SKImageInfo(32, 32, SKColorType.Rgba8888, SKAlphaType.Unpremul),
                new SKSamplingOptions(SKCubicResampler.Mitchell));
            return new WindowIcon(new Image(32, 32, icon.Bytes));
        }
        catch
        {
            return null;
        }
    }
}
