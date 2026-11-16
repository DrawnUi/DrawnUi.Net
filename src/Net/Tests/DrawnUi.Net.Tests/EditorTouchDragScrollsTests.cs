using AppoMobi.Gestures;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// On a touch screen a text field takes the focus (and the keyboard) on a tap, as native fields do: a finger drag that
/// starts on a field scrolls the page around it and leaves the field alone. The mouse keeps placing the caret on the
/// press. Found on the BV8800 (MAUI Android 3D gallery, 2026-11-16): a scroll that started on the sign-up form's Email
/// field did not move the page and opened the keyboard.
/// </summary>
public class EditorTouchDragScrollsTests
{
    private readonly ITestOutputHelper _out;
    public EditorTouchDragScrollsTests(ITestOutputHelper o) { _out = o; }

    static (HeadlessCanvasHost host, SkiaScroll scroll, SkiaEditor editor) Scene()
    {
        var host = new HeadlessCanvasHost(400, 600, scale: 1f, background: Colors.Black);
        SkiaEditor editor = null;
        var scroll = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Spacing = 0,
                Children =
                {
                    new SkiaShape { HeightRequest = 200, BackgroundColor = Colors.Gray },
                    new SkiaEditor { MaxLines = 1, HeightRequest = 50, FontSize = 18, BackgroundColor = Colors.DarkBlue }
                        .Assign(out editor),
                    new SkiaShape { HeightRequest = 1500, BackgroundColor = Colors.Gray },
                }
            }
        };
        host.Canvas.Content = scroll;
        host.AdvanceFrames(6);
        return (host, scroll, editor);
    }

    static void Settle(HeadlessCanvasHost host)
    {
        for (var i = 0; i < 30; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(15);
        }
    }

    [Fact]
    public void Touch_DragStartingOnAField_ScrollsThePage_AndLeavesTheFieldUnfocused()
    {
        var (host, scroll, editor) = Scene();
        using var _ = host;
        var robot = new GestureRobot(host) { Device = PointerDeviceType.Touch };

        robot.Pan(200, 225, 200, 25, durationMs: 300, steps: 12);
        Settle(host);

        _out.WriteLine($"offset {scroll.ViewportOffsetY}, focused {editor.IsFocused}");
        Assert.True(scroll.ViewportOffsetY < -100, $"the page did not scroll: {scroll.ViewportOffsetY}");
        Assert.False(editor.IsFocused);
    }

    [Fact]
    public void Touch_TapOnAField_FocusesIt()
    {
        var (host, scroll, editor) = Scene();
        using var _ = host;
        var robot = new GestureRobot(host) { Device = PointerDeviceType.Touch };

        robot.Tap(200, 225);
        Settle(host);

        Assert.True(editor.IsFocused);
        Assert.Equal(0, scroll.ViewportOffsetY);
    }

    [Fact]
    public void Mouse_PressOnAField_FocusesIt()
    {
        var (host, _, editor) = Scene();
        using var __ = host;
        var robot = new GestureRobot(host) { Device = PointerDeviceType.Mouse };

        robot.PointerDown(200, 225);
        Settle(host);
        robot.PointerUp();

        Assert.True(editor.IsFocused);
    }
}
