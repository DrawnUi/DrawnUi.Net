using DrawnUi.Draw;
using DrawnUi.Slint;
using HelloSlint;
using Color = DrawnUi.Color;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // The GPU path: DrawnUI draws with Skia on the OpenGL context of Slint's renderer.
        if (Environment.GetEnvironmentVariable("DRAWNUI_GPU") == "1")
            new Slint.BackendSelector().RequireOpenGL().Select();

        using var window = MainWindow.Create();
        using var cards = SlintCanvasHost.Attach(window.Instance, 0);
        using var keys = SlintCanvasHost.Attach(window.Instance, 1);
        var scene = new CardsScene(window);
        cards.Canvas.BackgroundColor = Color.Parse("#F3F4F7");
        cards.Canvas.Content = scene.Root;
        keys.Canvas.BackgroundColor = Color.Parse("#11161F");
        keys.Canvas.Content = KeysScene.Create();
        window.OnReset(scene.Reset);

        if (Environment.GetEnvironmentVariable("DRAWNUI_SELFTEST") is { Length: > 0 } outDir)
            SelfTest.Run(window, cards, keys, scene, outDir);

        window.Run();
    }
}
