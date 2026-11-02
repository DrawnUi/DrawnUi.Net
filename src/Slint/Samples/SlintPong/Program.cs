using DrawnUi.Draw;
using DrawnUi.Slint;
using DrawnUi.Views;
using Pong.Game;
using SlintPong;
using Color = DrawnUi.Color;
using Thickness = DrawnUi.Views.Thickness;

Super.UseDrawnUi()
    .ConfigureFonts(fonts =>
    {
        fonts.AddFont("fonts/NotoColorEmoji-Regular.ttf", "FontEmoji", FontWeight.Regular);
        fonts.AddFont("fonts/Orbitron-Regular.ttf", "FontGame", FontWeight.Regular);
        fonts.AddFont("fonts/Orbitron-Regular.ttf", "FontText", FontWeight.Regular);
        fonts.AddFont("fonts/Orbitron-SemiBold.ttf", "FontTextBold", FontWeight.SemiBold);
        fonts.AddFont("fonts/Orbitron-ExtraBold.ttf", "FontTextTitle");
    })
    .Build();

PongGame game = null!;
using var window = MainWindow.Create();
// The game's field has a fixed size, which the canvas scales to the window.
using var host = SlintCanvasHost.Attach(window.Instance, 0, new RescalingCanvas
{
    LogicalWidth = PongGame.WIDTH,
    LogicalHeight = PongGame.HEIGHT,
});
host.Canvas.BackgroundColor = Color.Parse("#0A0F1E");
host.Canvas.Content = new SkiaLayer
{
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    Children =
    {
        new SkiaImage("Images/back.jpg")
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
        }.Assign(out game),
#if DEBUG
        new SkiaLabelFps
        {
            Margin = new Thickness(0, 0, 4, 24),
            VerticalOptions = LayoutOptions.End,
            HorizontalOptions = LayoutOptions.End,
            Rotation = -45,
            BackgroundColor = Colors.DarkRed,
            TextColor = Colors.White,
            ZIndex = 110,
        },
#endif
    },
};

if (Environment.GetEnvironmentVariable("DRAWNUI_SELFTEST") is { Length: > 0 } outDir)
    SelfTest.Run(window, host, game, outDir);

window.Run();
