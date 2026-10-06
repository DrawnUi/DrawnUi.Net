using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The desktop canvas (WPF, OpenTK) routes a context-menu request to the control under it, like the browser heads:
/// a right click arrives as Mouse, the keyboard's Menu key carries no pointer and arrives as Keyboard.
/// </summary>
public class DesktopContextMenuTests
{
    [Fact]
    public void ContextMenuRequest_ReachesTheControlUnderIt_WithItsSource()
    {
        using var host = new HeadlessCanvasHost(300, 200);
        var sources = new List<ContextMenuSource>();
        host.Canvas.Content = new SkiaLayer
        {
            Children =
            {
                new SkiaShape
                {
                    WidthRequest = 100,
                    HeightRequest = 50,
                    BackgroundColor = Colors.Red,
                    ContextMenu = (_, e) =>
                    {
                        sources.Add(e.Source);
                        return true;
                    }
                },
            }
        };
        host.RenderFrame(16);

        Assert.True(host.Canvas.HandleDesktopContextMenu(50, 20, 300, 200, AppoMobi.Gestures.PointerDeviceType.Mouse));
        Assert.True(host.Canvas.HandleDesktopContextMenu(50, 20, 300, 200, null));
        Assert.False(host.Canvas.HandleDesktopContextMenu(250, 150, 300, 200, AppoMobi.Gestures.PointerDeviceType.Mouse));

        Assert.Equal(new[] { ContextMenuSource.Mouse, ContextMenuSource.Keyboard }, sources);
    }

    [Fact]
    public void ContextMenuRequest_ReachesAControlInsideAScroll()
    {
        using var host = new HeadlessCanvasHost(300, 200);
        var hits = 0;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Children =
                {
                    new SkiaShape
                    {
                        WidthRequest = 100,
                        HeightRequest = 50,
                        BackgroundColor = Colors.Red,
                        ContextMenu = (_, _) =>
                        {
                            hits++;
                            return true;
                        }
                    },
                    new SkiaShape { HeightRequest = 600, WidthRequest = 50 },
                }
            }
        };
        host.RenderFrame(16);

        Assert.True(host.Canvas.HandleDesktopContextMenu(50, 20, 300, 200, AppoMobi.Gestures.PointerDeviceType.Mouse));
        Assert.Equal(1, hits);
    }

    [Fact]
    public async Task ContextMenuRequest_ReachesAControlOnAPushedShellPage()
    {
        using var host = new HeadlessCanvasHost(400, 400);
        var hits = 0;
        SkiaShape target = null;
        var shell = new SkiaShell
        {
            Routes = new Dictionary<string, Func<ShellArguments, SkiaControl>>
            {
                ["page"] = _ => new SkiaScroll
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new SkiaStack
                    {
                        Padding = new Thickness(16),
                        Children =
                        {
                            // the HelloOpenTk Shapes page: a wrap row of cards (a stack: a card shape and a caption)
                            new SkiaWrap
                            {
                                Spacing = 16,
                                HorizontalOptions = LayoutOptions.Center,
                                MaximumWidthRequest = 680,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaStack
                                    {
                                        Spacing = 8,
                                        WidthRequest = 150,
                                        Children = new List<SkiaControl>
                                        {
                                            new SkiaShape
                                            {
                                                WidthRequest = 150,
                                                HeightRequest = 110,
                                                BackgroundColor = Colors.Gray,
                                                CornerRadius = 8,
                                                Children = new List<SkiaControl>
                                                {
                                                    new SkiaShape
                                                    {
                                                        WidthRequest = 120,
                                                        HeightRequest = 70,
                                                        BackgroundColor = Colors.Blue,
                                                        ContextMenu = (_, _) =>
                                                        {
                                                            hits++;
                                                            return true;
                                                        }
                                                    }.Center().Assign(out target),
                                                }
                                            },
                                            new SkiaLabel("caption") { HorizontalOptions = LayoutOptions.Center },
                                        }
                                    },
                                }
                            },
                        }
                    }
                },
            },
            RootContent = new SkiaLayer { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill },
        };
        host.Canvas.Content = shell;
        host.RenderFrame(16);
        await shell.GoToAsync("page", false);
        host.AdvanceFrames(30, 16);

        var rect = target.GetAccessibilityPixelRect();
        Assert.False(rect.IsEmpty);
        Assert.True(host.Canvas.HandleDesktopContextMenu(rect.MidX, rect.MidY, 400, 400, AppoMobi.Gestures.PointerDeviceType.Mouse));
        Assert.Equal(1, hits);
    }
}
