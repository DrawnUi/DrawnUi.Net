using DrawnUi;
using DrawnUi.Draw;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

Super.UseDrawnUi()
    .ConfigureFonts(fonts =>
    {
        fonts.AddFont("fonts/Orbitron-Regular.ttf",      "FontGame");
        fonts.AddFont("fonts/NotoColorEmoji-Regular.ttf", "FontEmoji");
    })
    .Build();

var gameSettings = new GameWindowSettings { };

var nativeSettings = new NativeWindowSettings
{
    ClientSize = new Vector2i(1280, 800),
    Title = "OpenTK 3D + DrawnUI Overlay",
    API = ContextAPI.OpenGL,
    APIVersion = new Version(4, 6),
    Profile = ContextProfile.Core,
};

using var window = new CubeWindow(gameSettings, nativeSettings);
window.Run();
