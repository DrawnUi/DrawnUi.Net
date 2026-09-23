global using DrawnUi.Draw;
global using SkiaSharp;
// DrawnUi and MAUI both declare these; the pages mean the drawn ones.
global using FontWeight = DrawnUi.Draw.FontWeight;
global using TextTransform = DrawnUi.Draw.TextTransform;
global using ScrollBarVisibility = DrawnUi.Draw.ScrollBarVisibility;

namespace HelloMaui;

/// <summary>
/// Startup, same fonts as the React demo and HelloWpf, registered through the MAUI font pipeline.
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
                fonts.AddFont("OpenSans-Regular.ttf", "FontText", FontWeight.Regular);
                fonts.AddFont("OpenSans-Semibold.ttf", "FontText", FontWeight.SemiBold); // FontAttributes=Bold / FontWeight=600 pick this face
                fonts.AddFont("OpenSans-Semibold.ttf", "FontTextBold");
                fonts.AddFont("NotoSansMathSymbols-Subset.ttf", "FontSymbols");
                fonts.AddFont("NotoSansSymbols2-Subset.ttf", "FontSymbols2");
            });

        builder.UseDrawnUi(new()
        {
            UseDesktopKeyboard = true, // KeyboardManager on Windows / MacCatalyst: Keyboard, Sprites and Editor pages
            DesktopWindow = new()
            {
                Width = 980,
                Height = 820,
            },
        });

        return builder.Build();
    }
}
