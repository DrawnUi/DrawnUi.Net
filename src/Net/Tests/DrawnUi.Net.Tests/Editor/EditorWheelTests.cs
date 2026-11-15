using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>A focused editor keeps the keyboard, not the mouse wheel: the page still scrolls under the wheel.</summary>
public class EditorWheelTests
{
    static void Frames(HeadlessCanvasHost host, int n)
    {
        for (var i = 0; i < n; i++)
            host.RenderFrame(16.7);
    }

    [Theory]
    [InlineData(150, 250)] // the wheel away from the field
    [InlineData(150, 40)]  // the wheel over the field itself
    public void The_page_scrolls_with_the_wheel_while_a_field_is_focused(double wheelX, double wheelY)
    {
        using var host = new HeadlessCanvasHost(300, 300, 1f, Colors.Black);
        SkiaScroll scroll = null;
        SkiaEditor editor = null;
        host.Canvas.Content = new SkiaScroll
        {
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Children =
                {
                    // as in a form: the field inside a cached panel, a card around it
                    new SkiaShape
                    {
                        UseCache = SkiaCacheType.None,
                        CornerRadius = 16,
                        Children =
                        {
                            new SkiaShape
                            {
                                UseCache = SkiaCacheType.Image,
                                Children =
                                {
                                    new SkiaStack
                                    {
                                        Children =
                                        {
                                            new SkiaShape
                                            {
                                                HeightRequest = 40, WidthRequest = 280, BackgroundColor = Colors.Gray,
                                                Children = { new SkiaEditor { HeightRequest = 40, WidthRequest = 280, MaxLines = 1 }.Assign(out editor) },
                                            },
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new SkiaLayer { HeightRequest = 2000, BackgroundColor = Colors.DarkBlue },
                }
            }
        }.Assign(out scroll);
        Frames(host, 3);
        var robot = new GestureRobot(host);

        robot.Tap(100, 20);
        Frames(host, 3);
        Assert.True(editor.IsFocused, "the field is focused");
        // the mouse rests on the field, then moves to where it wheels
        robot.Hover(100, 20);
        robot.Hover(wheelX, wheelY);

        for (var i = 0; i < 5; i++)
            robot.WheelScroll(wheelX, wheelY, -120);
        Frames(host, 30);
        Assert.True(scroll.ViewportOffsetY < -50, $"scrolled: {scroll.ViewportOffsetY}");
    }
}
