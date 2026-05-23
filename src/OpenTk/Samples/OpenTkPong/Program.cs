using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Gaming;
using DrawnUi.Infrastructure.Enums;
using DrawnUi.OpenTk;
using DrawnUi.Views;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTkPong;
using Pong.Game;
using SkiaSharp;
using Color = DrawnUi.Color;

DrawnGame.FrameInterpolatorDisabled = true;

DrawnExtensions.RegisterFont("FontEmojis", FontWeight.Regular, "fonts/NotoColorEmoji-Regular.ttf");
DrawnExtensions.RegisterFont("FontGame", FontWeight.Regular, "fonts/amstrad_cpc464.ttf");
DrawnExtensions.RegisterFont("FontText", FontWeight.Regular, "fonts/Orbitron-Regular.ttf");
DrawnExtensions.RegisterFont("FontTextBold", FontWeight.SemiBold, "fonts/Orbitron-SemiBold.ttf");
DrawnExtensions.RegisterFont("FontTextTitle", "fonts/Orbitron-ExtraBold.ttf");

SkiaFontManager.Instance.Initialize();

var gameSettings = new GameWindowSettings
{

};

var nativeSettings = new NativeWindowSettings
{
    ClientSize = new Vector2i((int)(PongGame.WIDTH*1.33), (int)(PongGame.HEIGHT*1.33)),
    Title = "Pong – DrawnUI OpenTK",
    API = ContextAPI.OpenGL,
    APIVersion = new Version(3, 3),
    Profile = ContextProfile.Core,
    Icon = LoadWindowIcon("icon.png"),
};

var canvas = new Canvas
{
    BackgroundColor = Color.FromArgb("#0A0F1E"),
    RenderingMode = RenderingModeType.Accelerated,
    UpdateMode = UpdateModeType.Constant,
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    Content = new List<SkiaControl>
    {
        new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new AspectLayer
                {
                    LogicalWidth = PongGame.WIDTH,
                    LogicalHeight = PongGame.HEIGHT,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new PongGame
                        {
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center,
                        }
                    }
                },
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
    }
};

using var window = new PongGameWindow(gameSettings, nativeSettings, canvas);
window.Run();

OpenTK.Windowing.Common.Input.WindowIcon? LoadWindowIcon(string relativePath)
{
    try
    {
        var path = Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(AppContext.BaseDirectory, relativePath);

        if (!File.Exists(path)) return null;

        using var bitmap = SKBitmap.Decode(path);
        if (bitmap == null) return null;

        var resized = bitmap.Width != 32 || bitmap.Height != 32
            ? bitmap.Resize(new SKImageInfo(32, 32), SKFilterQuality.High)
            : bitmap;

        var pixels = resized.Bytes;
        // SKBitmap is BGRA on most platforms; OpenTK wants RGBA
        for (int i = 0; i < pixels.Length; i += 4)
            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);

        var image = new OpenTK.Windowing.Common.Input.Image(32, 32, pixels);
        if (!ReferenceEquals(resized, bitmap)) resized.Dispose();
        return new OpenTK.Windowing.Common.Input.WindowIcon(image);
    }
    catch { return null; }
}

class PongGameWindow(GameWindowSettings gs, NativeWindowSettings ns, Canvas canvas)
    : DrawnUiGameWindow(gs, ns, canvas)
{
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
}
