global using AppoMobi.Gestures;
global using AppoMobi.Specials;
global using DrawnUi;
global using DrawnUi.Add3d;
global using DrawnUi.Controls;
global using DrawnUi.Draw;
global using DrawnUi.Models;
global using DrawnUi.Views;
global using SkiaSharp;
// DrawnUi and MAUI both declare these; the app means the drawn ones.
global using FontWeight = DrawnUi.Draw.FontWeight;

namespace Add3dGallery;

/// <summary>Startup: the font aliases the shared pages ask for, and a desktop window.</summary>
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
                fonts.AddFont("OpenSans-Regular.ttf", "FontText", FontWeight.Regular);
                fonts.AddFont("OpenSans-Semibold.ttf", "FontTextTitle", FontWeight.SemiBold);
            });

        builder.UseDrawnUi(new()
        {
            UseDesktopKeyboard = true,
            DesktopWindow = new() { Width = 1280, Height = 860 },
        });

        return builder.Build();
    }
}
