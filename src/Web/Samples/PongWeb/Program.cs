global using System.Runtime.InteropServices.JavaScript;
global using DrawnUi;
global using DrawnUi.Draw;
global using DrawnUi.Views;
global using DrawnUi.Controls;
global using DrawnUi.Infrastructure.Enums;
global using Pong.Game;
global using SkiaSharp;

namespace PongWeb;

public static partial class Program
{
    [JSExport]
    public static Task Main() =>
        Super.UseDrawnUi()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("fonts/NotoColorEmoji-Regular.ttf", "FontEmoji", FontWeight.Regular);
                fonts.AddFont("fonts/Orbitron-Regular.ttf", "FontGame", FontWeight.Regular);
                fonts.AddFont("fonts/Orbitron-Regular.ttf", "FontText", FontWeight.Regular);
                fonts.AddFont("fonts/Orbitron-SemiBold.ttf", "FontTextBold", FontWeight.SemiBold);
                fonts.AddFont("fonts/Orbitron-ExtraBold.ttf", "FontTextTitle");
            })
            .RunAsync("drawnui-canvas", () => new RescalingCanvas
            {
                LogicalWidth = PongGame.WIDTH,
                LogicalHeight = PongGame.HEIGHT,
                BackgroundColor = Color.FromArgb("#0A0F1E"),
                RenderingMode = RenderingModeType.Accelerated, //gpu acceleration
                UpdateMode = UpdateModeType.Constant, //more smooth anims on web
                Gestures = GesturesMode.Lock,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Content = new SkiaLayer
                {
                    VerticalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new PongGame().Center(),

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
                        }
                    }
                }
            });
}
