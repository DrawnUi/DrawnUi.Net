using System.Windows;
using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Wpf;
using Pong.Game;
using Color = DrawnUi.Color;
using Thickness = DrawnUi.Views.Thickness;

namespace WpfPong;

/// <summary>
/// Hosts the shared Pong game (src/Shared/Samples/Pong.Shared) in a WPF window. The canvas is a
/// <see cref="RescalingCanvas"/>, so the fixed logical game viewport scales with the window while
/// keeping its aspect ratio, exactly as in the OpenTK and WebAssembly hosts.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Builds the drawn tree. Window size and keyboard come from the startup settings in App.</summary>
    public MainWindow()
    {
        InitializeComponent();

        Content = new DrawnUiElement(() => new RescalingCanvas
        {
            LogicalWidth = PongGame.WIDTH,
            LogicalHeight = PongGame.HEIGHT,
            BackgroundColor = Color.FromArgb("#0A0F1E"),
            UpdateMode = UpdateModeType.Constant,
        })
        {
            RenderingMode = RenderingModeType.Accelerated,
            Gestures = GesturesMode.Lock,
            Content = new SkiaLayer
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Children =
                {
                    new SkiaImage(@"Images/back.jpg")
                    {
                        UseCache = SkiaCacheType.Image,
                        AddEffect = SkiaImageEffect.Darken,
                        Darken = 0.2,
                    }.Fill(),
                    new PongGame
                    {
                        WidthRequest = PongGame.WIDTH,
                        HeightRequest = PongGame.HEIGHT,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                    },
#if DEBUG
                    new SkiaLabelFps
                    {
                        UseCache = SkiaCacheType.GPU,
                        Margin = new Thickness(0, 0, 4, 24),
                        VerticalOptions = LayoutOptions.End,
                        HorizontalOptions = LayoutOptions.End,
                        Rotation = -45,
                        BackgroundColor = Colors.DarkRed,
                        TextColor = Colors.White,
                        ZIndex = 110,
                    },
#endif
                }
            }
        };
    }
}
