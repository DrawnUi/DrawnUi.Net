using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Infrastructure.Enums;
using DrawnUi.OpenTk;
using DrawnUi.Views;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

Super.UseDrawnUi().Build();

// UpdateFrequency = 0: no continuous game loop timer.
// DrawnUiWindow renders only when canvas is dirty and wakes via GLFW.PostEmptyEvent.
var gameSettings = new GameWindowSettings { UpdateFrequency = 0 };

var nativeSettings = new NativeWindowSettings
{
    ClientSize = new Vector2i(1280, 800),
    Title = "DrawnUi.Net OpenTK GPU Host",
    API = ContextAPI.OpenGL,
    APIVersion = new Version(3, 3),
    Profile = ContextProfile.Core
};

using var window = new DrawnUiWindow(gameSettings, nativeSettings,
    new Canvas
    {
        BackgroundColor = Color.FromArgb("#000066"),
        RenderingMode = RenderingModeType.Accelerated,
        UpdateMode = UpdateModeType.Dynamic,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
        Content = new List<SkiaControl>
        {
            new SkiaLayer()
            {
                VerticalOptions = LayoutOptions.Fill,
                Children =
                {
                    new DemoScene(),
#if DEBUG
                    new SkiaLabelFps()
                    {
                        UseCache = SkiaCacheType.GPU,
                        Margin = new(0, 0, 4, 24),
                        VerticalOptions = LayoutOptions.End,
                        HorizontalOptions = LayoutOptions.End,
                        Rotation = -45,
                        BackgroundColor = Colors.DarkRed,
                        TextColor = Colors.White,
                        ZIndex = 110,
                    }
#endif
                }
            }
        }
    });

window.Run();
