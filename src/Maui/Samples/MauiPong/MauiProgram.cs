global using DrawnUi.Draw;
global using DrawnUi.Views;
global using Pong.Game;
// DrawnUi and MAUI both declare these; the app means the drawn ones.
global using FontWeight = DrawnUi.Draw.FontWeight;

namespace MauiPong;

/// <summary>
/// Startup: the same font aliases as the OpenTK, WPF and WASM Pong hosts (the game asks for "FontGame"),
/// registered through the MAUI font pipeline, and a desktop window sized to the game.
/// </summary>
public static class MauiProgram
{
    /// <summary>Builds the MAUI app.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("NotoColorEmoji-Regular.ttf", "FontEmoji", FontWeight.Regular);
                fonts.AddFont("Orbitron-Regular.ttf", "FontGame", FontWeight.Regular);
                fonts.AddFont("Orbitron-Regular.ttf", "FontText", FontWeight.Regular);
                fonts.AddFont("Orbitron-SemiBold.ttf", "FontTextBold", FontWeight.SemiBold);
                fonts.AddFont("Orbitron-ExtraBold.ttf", "FontTextTitle");
            });

        builder.UseDrawnUi(new()
        {
            UseDesktopKeyboard = true, // KeyboardManager on Windows / MacCatalyst: arrow keys move the paddle
            DesktopWindow = new()
            {
                Width = (int)(PongGame.WIDTH * 1.33),
                Height = (int)(PongGame.HEIGHT * 1.33),
            },
        });

        return builder.Build();
    }
}
