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
    public static Task Main() => Super.UseDrawnUi()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("fonts/Orbitron-SemiBold.ttf", "FontGame");
            })
            .ConfigureStyles(styles =>
            {
                styles.AddStyle(new Style()
                {
                    ApplyToDerivedTypes = true,
                    TargetType = typeof(SkiaLabel),
                    Setters =
                    {
                        new Setter()
                        {
                            Property = SkiaLabel.FontFamilyProperty,
                            Value = "FontGame"
                        }
                    }
                });
            })
            .RunAsync("drawnui-canvas", () => new RescalingCanvas
            {
                LogicalWidth = PongGame.WIDTH,
                LogicalHeight = PongGame.HEIGHT,
                BackgroundColor = Color.FromArgb("#0A0F1E"),
                RenderingMode = RenderingModeType.Accelerated, //gpu acceleration
                Gestures = GesturesMode.Lock,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Content = new SkiaLayer
                {
                    VerticalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new PongGame().CenterX(),

#if DEBUG
                        new SkiaLabelFps
                        {
                            Margin = new Thickness(0, 0, 4, 24),
                            VerticalOptions = LayoutOptions.End,
                            HorizontalOptions = LayoutOptions.End,
                            Rotation = -45,
                            BackgroundColor = Colors.DarkRed,
                            TextColor = Colors.White,
                        }
#endif
                    }
                }
            });
}
