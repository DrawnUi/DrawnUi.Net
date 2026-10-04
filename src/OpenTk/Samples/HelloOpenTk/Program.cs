using System.Globalization;
using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Views;
using HelloOpenTk;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Color = DrawnUi.Color;
using Style = DrawnUi.Draw.Style;
using Setter = DrawnUi.Draw.Setter;

// Numbers in captions and status lines read the same on every machine and as in the React demo
// ("0.005", never "0,005" on a comma-decimal locale).
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// Startup, same shape as the React demo's main.tsx and the other .NET heads.
Super.UseDrawnUi()
    .ConfigureFonts(fonts => fonts
        .AddFont("fonts/OpenSans-Regular.ttf", "FontText")
        .AddFont("fonts/OpenSans-Semibold.ttf", "FontText", FontWeight.SemiBold) // FontAttributes=Bold / FontWeight=600 pick this face
        .AddFont("fonts/OpenSans-Semibold.ttf", "FontTextBold")
        .AddFont("fonts/Orbitron-Regular.ttf", "FontGame") // Pong score and messages
        .AddFont("fonts/NotoSansMathSymbols-Subset.ttf", "FontSymbols")
        .AddFont("fonts/NotoSansSymbols2-Subset.ttf", "FontSymbols2")
        // the web heads' Noto emoji subset flattened to COLRv0, which SkiaSharp draws on Windows (DirectWrite) and Linux
        .AddFont("fonts/NotoColorEmoji-Subset-COLRv0.ttf", "FontEmoji"))
    // Without this every control that leaves FontFamily empty draws in Skia's built-in face. A SkiaLabel style does
    // not reach button captions (SkiaButton pushes its own FontFamily onto its label), so style buttons too.
    .ConfigureStyles(styles => styles
        .AddStyle(new Style
        {
            TargetType = typeof(SkiaLabel),
            ApplyToDerivedTypes = true,
            Setters = { new Setter { Property = SkiaLabel.FontFamilyProperty, Value = "FontText" } },
        })
        .AddStyle(new Style
        {
            TargetType = typeof(SkiaButton),
            ApplyToDerivedTypes = true,
            Setters = { new Setter { Property = SkiaButton.FontFamilyProperty, Value = "FontText" } },
        }))
    .Build();

// Every label is read as text, every button is a button, as in the React demo.
SkiaLabel.DefaultAccessibilityRole = Aria.RoleText;
SkiaButton.DefaultAccessibilityRole = Aria.RoleButton;

var canvas = new Canvas
{
    BackgroundColor = Color.Parse("#212529"),
    RenderingMode = RenderingModeType.Accelerated,
    // an app: renders while something changes and sleeps when idle; Pong's DrawnGame keeps frames coming while it runs
    UpdateMode = UpdateModeType.Dynamic,
    Gestures = GesturesMode.Enabled,
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    Content = HelloApp.BuildContent(),
};

// The Images page photo (also the Shell backdrop and the Scroll header), warmed once the first screen is up, so the
// first visit to Images shows it at once instead of black tiles.
EventHandler<SkiaDrawingContext> preload = null;
preload = (_, _) =>
{
    canvas.WasDrawn -= preload;
    _ = Task.Run(() => SkiaImageManager.Instance.PreloadImages(new List<string> { "images/baboon.jpg" }));
};
canvas.WasDrawn += preload;

var nativeSettings = new NativeWindowSettings
{
    ClientSize = new Vector2i(980, 820),
    Title = "DrawnUI for OpenTK",
    API = ContextAPI.OpenGL,
    Profile = ContextProfile.Core,
    // Windows has GL 4.6; Mesa (Linux) gives 3.3 and macOS 4.1, which also needs a forward-compatible core context
    APIVersion = OperatingSystem.IsWindows() ? new Version(4, 6) : new Version(3, 3),
    Flags = OperatingSystem.IsMacOS() ? ContextFlags.ForwardCompatible : ContextFlags.Default,
    WindowState = WindowState.Normal,
    Icon = HelloApp.LoadWindowIcon(),
};

using var window = new HelloWindow(new GameWindowSettings { UpdateFrequency = 0 }, nativeSettings, canvas);
HelloApp.Window = window;
window.Run();
