global using AppoMobi.Gestures;
global using AppoMobi.Specials;
global using DrawnUi;
global using DrawnUi.Add3d;
global using DrawnUi.Controls;
global using DrawnUi.Draw;
global using DrawnUi.Models;
global using DrawnUi.Views;
global using SkiaSharp;
global using SkiaSharp.Views.Maui;
// DrawnUi and MAUI both declare these; the app means the drawn ones.
global using FontWeight = DrawnUi.Draw.FontWeight;
using Microsoft.Maui.LifecycleEvents;

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

#if WINDOWS
        // the app icon on the window's title bar and the taskbar (an unpackaged app shows none by itself)
        builder.ConfigureLifecycleEvents(events => events.AddWindows(windows => windows.OnWindowCreated(window =>
            window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "appicon.ico")))));
#endif

        return builder.Build();
    }
}
