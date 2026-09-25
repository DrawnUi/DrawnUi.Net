namespace MauiPong;

/// <summary>
/// The Pong host page for the MAUI head: a <see cref="RescalingCanvas"/> fits the game's fixed logical viewport
/// (<see cref="PongGame.WIDTH"/> x <see cref="PongGame.HEIGHT"/>) into the window, keeping its aspect ratio,
/// exactly like the OpenTK, WPF and WASM hosts. The game itself is the shared Pong.Shared project.
/// </summary>
public class MainPage : ContentPage
{
    /// <summary>Builds the page.</summary>
    public MainPage()
    {
        BackgroundColor = Color.FromArgb("#0A0F1E");

        // caching plan: the background photo is a static Image cache blitted under the game, the game
        // repaints every frame (UpdateMode Constant) and stays uncached, the FPS badge is a small GPU layer
        Content = new Grid // MAUI root wrapper, needed for iOS safe insets
        {
            Children =
            {
                new RescalingCanvas
                {
                    LogicalWidth = PongGame.WIDTH,
                    LogicalHeight = PongGame.HEIGHT,
                    BackgroundColor = Color.FromArgb("#0A0F1E"),
                    RenderingMode = RenderingModeType.Accelerated,
                    Gestures = GesturesMode.Lock, // the whole input stream belongs to the game
                    UpdateMode = UpdateModeType.Constant,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new SkiaLayer
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        Children =
                        {
                            new SkiaImage("images/back.jpg")
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
                }
            }
        };
    }
}
