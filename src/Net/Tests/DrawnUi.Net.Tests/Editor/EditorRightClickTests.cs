using AppoMobi.Gestures;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A right click in an editor leaves its caret and selection as they are, so its context menu acts on the selected
/// text (as a selectable label, drawnui-cross 6ca). Found in DrawnUI for Rust, checked here.
/// </summary>
public class EditorRightClickTests
{
    private const string Text = "Hello wonderful world";

    private static (HeadlessCanvasHost Host, SkiaEditor Editor, GestureRobot Robot) Create()
    {
        var host = new HeadlessCanvasHost(420, 200, scale: 1f, background: Colors.Black);
        var editor = new SkiaEditor { WidthRequest = 360, FontSize = 20, Text = Text, Margin = new Thickness(20) };
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { editor } };
        host.AdvanceFrames(6);
        return (host, editor, new GestureRobot(host) { Device = PointerDeviceType.Mouse });
    }

    private static (float X, float Y) Middle(SkiaEditor editor)
    {
        var r = editor.DrawingRect;
        return (r.MidX, r.MidY);
    }

    [Fact]
    public void RightClick_KeepsSelectAll()
    {
        var (host, editor, robot) = Create();
        using var _ = host;
        var mid = Middle(editor);
        robot.Tap(mid.X, mid.Y);
        host.AdvanceFrames(3);
        editor.SelectAll();
        Assert.Equal(Text, editor.GetSelectedText());

        robot.Button = MouseButton.Right;
        robot.Tap(mid.X, mid.Y);
        host.AdvanceFrames(3);
        Assert.Equal(Text, editor.GetSelectedText());
    }

    [Fact]
    public void RightClick_RightAfterALeftClick_IsNoDoubleClick()
    {
        var (host, editor, robot) = Create();
        using var _ = host;
        var mid = Middle(editor);
        robot.Tap(mid.X, mid.Y);
        host.AdvanceFrames(1);
        var caret = editor.CursorPosition;

        robot.Button = MouseButton.Right;
        robot.Tap(mid.X + 30, mid.Y);
        host.AdvanceFrames(3);
        Assert.Equal("", editor.GetSelectedText() ?? "");
        Assert.Equal(caret, editor.CursorPosition);
    }
}
