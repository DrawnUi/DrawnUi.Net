global using DrawnUi.Draw;
global using SkiaSharp;
// DrawnUi and MAUI both declare these; the pages mean the drawn ones.
global using FontWeight = DrawnUi.Draw.FontWeight;
global using TextTransform = DrawnUi.Draw.TextTransform;
global using ScrollBarVisibility = DrawnUi.Draw.ScrollBarVisibility;
using System.Globalization;

namespace HelloMaui;

/// <summary>
/// Startup, same fonts as the React demo and HelloWpf, registered through the MAUI font pipeline.
/// </summary>
public static class MauiProgram
{
    /// <summary>Builds the MAUI app.</summary>
    public static MauiApp CreateMauiApp()
    {
        // Numbers in captions and status lines read the same on every device and as in the React demo
        // ("0.005", never "0,005" on a comma-decimal locale).
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "FontText", FontWeight.Regular);
                fonts.AddFont("OpenSans-Semibold.ttf", "FontText", FontWeight.SemiBold); // FontAttributes=Bold / FontWeight=600 pick this face
                fonts.AddFont("OpenSans-Semibold.ttf", "FontTextBold");
                fonts.AddFont("Orbitron-Regular.ttf", "FontGame"); // Pong score and messages, as in the .NET Pong samples
                fonts.AddFont("NotoSansMathSymbols-Subset.ttf", "FontSymbols");
                fonts.AddFont("NotoSansSymbols2-Subset.ttf", "FontSymbols2");
#if WINDOWS
                // As HelloWpf: the web heads' Noto emoji subset flattened to COLRv0 (dev/fonts/colrv0_emoji.py),
                // since SkiaSharp on Windows draws the COLRv1 original as nothing (measured 2026-10-02).
                // Other platforms have no FontEmoji yet (not measured): emoji come from the system font.
                fonts.AddFont("NotoColorEmoji-Subset-COLRv0.ttf", "FontEmoji");
#endif
            });

        // Every label is read as text, every button is a button, as in the React demo.
        SkiaLabel.DefaultAccessibilityRole = DrawnUi.Models.Aria.RoleText;
        SkiaButton.DefaultAccessibilityRole = DrawnUi.Models.Aria.RoleButton;

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
