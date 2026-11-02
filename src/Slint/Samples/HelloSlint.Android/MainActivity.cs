using DrawnUi.Draw;
using DrawnUi.Slint;
using Slint.Android;
using Color = DrawnUi.Color;

namespace HelloSlint;

[Activity(Label = "DrawnUI on Slint", MainLauncher = true, Exported = true, ConfigurationChanges = SlintActivity.ConfigurationChanges)]
[MetaData(SlintActivity.LibraryNameKey, Value = SlintActivity.LibraryName)]
public class MainActivity : SlintActivity
{
    // Runs on the Slint thread, like every callback of the components.
    protected override void SlintMain()
    {
        using var window = MainWindow.Create();
        using var cards = SlintCanvasHost.Attach(window.Instance, 0);
        using var keys = SlintCanvasHost.Attach(window.Instance, 1);
        var scene = new CardsScene(window);
        cards.Canvas.BackgroundColor = Color.Parse("#F3F4F7");
        cards.Canvas.Content = scene.Root;
        keys.Canvas.BackgroundColor = Color.Parse("#11161F");
        keys.Canvas.Content = KeysScene.Create();
        window.OnReset(scene.Reset);
        cards.FrameRendered += ms => FrameLog.Add(cards, ms);
        window.Run();
    }
}

/// Logs which path the first view takes and its frame times, for adb logcat -s DrawnUiSlint.
internal static class FrameLog
{
    private static readonly List<double> Times = new();

    public static void Add(SlintCanvasHost host, double ms)
    {
        Times.Add(ms);
        if (Times.Count % 120 == 0)
        {
            var rest = Times.Skip(1).ToList();
            Android.Util.Log.Info("DrawnUiSlint", $"GPU {host.IsGpu}, scale {host.Scale}, frames {Times.Count}, average {rest.Average():F2} ms, max {rest.Max():F2} ms");
        }
    }
}
