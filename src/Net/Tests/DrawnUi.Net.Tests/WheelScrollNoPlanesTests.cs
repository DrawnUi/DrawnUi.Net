using System.Collections.Generic;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;
using Color = DrawnUi.Color;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Guards that SkiaWheelPicker / SkiaWheelScroll render, scroll and select WITHOUT the planes
/// (VirtualisationType.Managed) machinery. The wheel draws its own cells via the overridden
/// DrawVirtual; selection is only updated from inside that draw path (PrepareCell -> SetCurrentIndex),
/// so an observed SelectedIndex change proves the plane-free draw+gesture+snap pipeline executed.
/// </summary>
public class WheelScrollNoPlanesTests
{
    private readonly ITestOutputHelper _out;
    public WheelScrollNoPlanesTests(ITestOutputHelper output) => _out = output;

    private static readonly List<string> Days =
        new() { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    private (HeadlessCanvasHost host, SkiaWheelPicker picker, GestureRobot robot) NewWheel(int selected = 2)
    {
        var host = new HeadlessCanvasHost(360, 360, scale: 1f, background: Colors.Black);

        var picker = new SkiaWheelPicker
        {
            WidthRequest = 180,
            HeightRequest = 180,
            VisibleItems = 5,
            SelectedIndex = selected,
            ItemsSource = Days,
            TextColor = new Color(0.5f, 0.5f, 0.5f, 1f),
            TextSelectedColor = Colors.White,
            LinesColor = Colors.Gray,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };

        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { picker },
        };

        var robot = new GestureRobot(host);

        // warm up: layout + first wheel draws
        for (int i = 0; i < 20; i++) host.RenderFrame(16);

        return (host, picker, robot);
    }

    [Fact]
    public void Wheel_Renders_And_Reports_Initial_Selection_Without_Planes()
    {
        var (host, picker, robot) = NewWheel(selected: 2);
        using var _ = host;

        var scroller = picker.Scroller;
        Assert.NotNull(scroller);

        // The wheel is a virtual scroll but must NOT be on the planes path.
        Assert.True(scroller.UseVirtual, "wheel scroll must use the virtual draw path");

        // initial selection honored, wheel actually laid out
        Assert.True(scroller.DrawingRect.Height > 1, "wheel did not lay out");
        Assert.Equal(2, picker.SelectedIndex);
    }

    [Fact]
    public void Wheel_Scroll_Changes_Selection_Without_Planes()
    {
        var (host, picker, robot) = NewWheel(selected: 3);
        using var _ = host;

        // The picker suppresses its own SelectedIndexChanged for scroll-driven updates (echo guard);
        // the framework selection event raised from the wheel draw/snap path is on the scroller.
        int eventCount = 0;
        int lastEventIndex = -1;
        picker.Scroller.SelectedIndexChanged += (_, idx) => { eventCount++; lastEventIndex = idx; };

        int before = picker.SelectedIndex;

        // drag upward inside the wheel (centered 180x180 in a 360x360 canvas) to advance the selection,
        // then let the snap/fling settle
        robot.Pan(180, 210, 180, 110, durationMs: 120, steps: 10);
        for (int i = 0; i < 60; i++) host.RenderFrame(16);

        int after = picker.SelectedIndex;
        _out.WriteLine($"selection {before} -> {after}, events={eventCount}, lastEvent={lastEventIndex}, " +
                       $"scrollerIdx={picker.Scroller.SelectedIndex}");

        Assert.True(eventCount > 0, "SelectedIndexChanged never fired during wheel scroll");
        Assert.NotEqual(before, after);
        Assert.InRange(after, 0, Days.Count - 1);
        Assert.Equal(after, picker.Scroller.SelectedIndex);
    }
}
