using DrawnUi;
using DrawnUi.Draw;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using SkiaSharp;

Super.UseDrawnUi()
    .ConfigureFonts(fonts =>
    {
        fonts.AddFont("fonts/Orbitron-Regular.ttf",      "FontGame");
        //in case some other platforms need this, normally on WIN it's not needed
        fonts.AddFont("fonts/NotoColorEmoji-Regular.ttf", "FontEmoji");
    })
    .Build();

OpenTK.Windowing.Common.Input.WindowIcon? LoadWindowIcon()
{
    try
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("icon.ico", StringComparison.OrdinalIgnoreCase));
        if (name == null) return null;

        using var stream = asm.GetManifestResourceStream(name);
        if (stream == null) return null;

        using var bitmap = SKBitmap.Decode(stream);
        if (bitmap == null) return null;

        var resized = bitmap.Width != 32 || bitmap.Height != 32
            ? bitmap.Resize(new SKImageInfo(32, 32), SkiaSamplingOptions.GetSamplingOptions(FilterQuality.Ultra, true))
            : bitmap;

        var pixels = resized.Bytes;
        // SKBitmap is BGRA; OpenTK wants RGBA
        for (int i = 0; i < pixels.Length; i += 4)
            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);

        var image = new OpenTK.Windowing.Common.Input.Image(32, 32, pixels);
        if (!ReferenceEquals(resized, bitmap)) resized.Dispose();
        return new OpenTK.Windowing.Common.Input.WindowIcon(image);
    }
    catch { return null; }
}

var gameSettings = new GameWindowSettings { };

var nativeSettings = new NativeWindowSettings
{
    ClientSize = new Vector2i(1280, 800),
    Title = "OpenTK 3D + DrawnUI Overlay",
    API = ContextAPI.OpenGL,
    APIVersion = OperatingSystem.IsLinux() ? new Version(3, 3) : new Version(4, 6),
    Profile = ContextProfile.Core,
    WindowState = WindowState.Normal,
    StartVisible = false,
    Icon = LoadWindowIcon(),
};

using var window = new CubeWindow(gameSettings, nativeSettings);
window.Run();
