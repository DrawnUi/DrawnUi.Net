using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

Super.UseDrawnUi()
    .ConfigureFonts(fonts =>
    {
        // Same aliases the DrawnUI Fiddle registers, so exported snippets
        // find the fonts they were written against.
        fonts.AddFont("OpenSans-Regular.ttf", "FontText");
        fonts.AddFont("OpenSans-Semibold.ttf", "FontTextTitle");
    })
    .Build();

var nativeSettings = new NativeWindowSettings
{
    // Phone-sized, so the layout matches mobile. Change freely: the canvas fills the window.
    ClientSize = new Vector2i(375, 750),
    Title = "DrawnApp",
    API = ContextAPI.OpenGL,
    Profile = ContextProfile.Core,
    // Mesa drivers on Linux (and WSLg) do not offer 4.6: ask for 3.3 there, Skia needs no more.
    APIVersion = OperatingSystem.IsWindows() ? new Version(4, 6) : new Version(3, 3),
    Flags = OperatingSystem.IsWindows() ? ContextFlags.Default : ContextFlags.ForwardCompatible,
    WindowState = WindowState.Normal,
    Icon = EmptyCode.MainWindow.LoadIcon(),
};

using var window = new EmptyCode.MainWindow(new GameWindowSettings(), nativeSettings);
window.Run();
