using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;
using Color = DrawnUi.Color;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaWheelPicker and SkiaSpinner raise SelectedIndexChanged once per real change, also when the change comes from
/// the user turning the wheel (drag, fling, snap), never twice for one change, and keep SelectedIndex and a two-way
/// bound model in sync. Before the fix the event was raised only for values set from code.
/// </summary>
public class WheelSelectionEventTests
{
    private readonly ITestOutputHelper _out;
    public WheelSelectionEventTests(ITestOutputHelper output) => _out = output;

    private static readonly List<string> Days =
        new() { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    private sealed class Model : INotifyPropertyChanged
    {
        private int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex == value) return;
                _selectedIndex = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private sealed class EventLog
    {
        public readonly List<int> Raised = new();
        public void On(object? sender, int index) => Raised.Add(index);
    }

    #region SkiaWheelPicker

    private static (HeadlessCanvasHost host, SkiaWheelPicker picker, GestureRobot robot, EventLog log) NewPicker(
        int selected = 3, Model? model = null)
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

        if (model != null)
        {
            picker.ObservePropertyTwoWay(model,
                nameof(Model.SelectedIndex), me => me.SelectedIndex = model.SelectedIndex,
                nameof(SkiaWheelPicker.SelectedIndex), (src, me) => src.SelectedIndex = me.SelectedIndex);
        }

        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { picker },
        };

        for (int i = 0; i < 20; i++) host.RenderFrame(16);

        var log = new EventLog();
        picker.SelectedIndexChanged += log.On;

        return (host, picker, new GestureRobot(host), log);
    }

    private static void Settle(HeadlessCanvasHost host, SkiaWheelPicker picker, GestureRobot robot)
    {
        robot.SettleFling(picker.Scroller, maxFrames: 600);
        for (int i = 0; i < 60; i++) host.RenderFrame(16);
    }

    [Fact]
    public void Picker_SlowDrag_RaisesOnce_WhenWheelSettles()
    {
        var (host, picker, robot, log) = NewPicker(selected: 3);
        using var _ = host;
        int before = picker.SelectedIndex;

        // drag up about two rows, hold still, release: no fling, the wheel snaps to the nearest row
        robot.Pan(new PointF(180, 220), new PointF(180, 150), durationMs: 200, steps: 10, holdMs: 300);
        Settle(host, picker, robot);

        _out.WriteLine($"{before} -> {picker.SelectedIndex}, events [{string.Join(",", log.Raised)}]");

        Assert.NotEqual(before, picker.SelectedIndex);
        Assert.Equal(picker.Scroller.SelectedIndex, picker.SelectedIndex);
        Assert.Equal(new[] { picker.SelectedIndex }, log.Raised);
    }

    [Fact]
    public void Picker_Fling_RaisesOnce_WithTheIndexItLandsOn()
    {
        var (host, picker, robot, log) = NewPicker(selected: 0);
        using var _ = host;
        int before = picker.SelectedIndex;

        robot.Pan(180, 250, 180, 140, durationMs: 60, steps: 4);
        Settle(host, picker, robot);

        _out.WriteLine($"{before} -> {picker.SelectedIndex}, events [{string.Join(",", log.Raised)}]");

        Assert.NotEqual(before, picker.SelectedIndex);
        Assert.Equal(picker.Scroller.SelectedIndex, picker.SelectedIndex);
        Assert.Equal(new[] { picker.SelectedIndex }, log.Raised);
    }

    [Fact]
    public void Picker_SetFromCode_RaisesOnce_AndScrollsTheWheel()
    {
        var (host, picker, robot, log) = NewPicker(selected: 1);
        using var _ = host;

        picker.SelectedIndex = 5;
        for (int i = 0; i < 60; i++) host.RenderFrame(16);

        Assert.Equal(5, picker.SelectedIndex);
        Assert.Equal(5, picker.Scroller.SelectedIndex);
        Assert.Equal(new[] { 5 }, log.Raised);
    }

    [Fact]
    public void Picker_TwoWayModel_FollowsTheUser_AndDrivesTheWheel()
    {
        var model = new Model { SelectedIndex = 2 };
        var (host, picker, robot, log) = NewPicker(selected: -1, model: model);
        using var _ = host;
        Assert.Equal(2, picker.SelectedIndex);

        robot.Pan(new PointF(180, 220), new PointF(180, 150), durationMs: 200, steps: 10, holdMs: 300);
        Settle(host, picker, robot);

        Assert.NotEqual(2, picker.SelectedIndex);
        Assert.Equal(picker.SelectedIndex, model.SelectedIndex);
        Assert.Equal(new[] { picker.SelectedIndex }, log.Raised);

        model.SelectedIndex = 0;
        for (int i = 0; i < 60; i++) host.RenderFrame(16);

        Assert.Equal(0, picker.SelectedIndex);
        Assert.Equal(0, picker.Scroller.SelectedIndex);
        Assert.Equal(2, log.Raised.Count);
        Assert.Equal(0, log.Raised[^1]);
    }

    #endregion

    #region SkiaSpinner

    private const float Cx = 180, Cy = 180;

    private static (HeadlessCanvasHost host, SkiaSpinner spinner, GestureRobot robot, EventLog log) NewSpinner()
    {
        var host = new HeadlessCanvasHost(360, 360, scale: 1f, background: Colors.Black);

        var spinner = new SkiaSpinner
        {
            WidthRequest = 240,
            HeightRequest = 240,
            ItemsSource = new List<string> { "A", "B", "C", "D", "E", "F", "G", "H" }, // 45 degrees each
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };

        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { spinner },
        };

        for (int i = 0; i < 20; i++) host.RenderFrame(16);

        var log = new EventLog();
        spinner.SelectedIndexChanged += log.On;

        return (host, spinner, new GestureRobot(host), log);
    }

    private static PointF OnCircle(double degrees, float radius = 80)
    {
        var rad = degrees * Math.PI / 180.0;
        return new PointF(Cx + radius * (float)Math.Cos(rad), Cy + radius * (float)Math.Sin(rad));
    }

    private static void SettleSpinner(HeadlessCanvasHost host, SkiaSpinner spinner, int maxFrames = 1200)
    {
        var stable = 0;
        var prev = spinner.WheelRotation;
        for (int i = 0; i < maxFrames && stable < 30; i++)
        {
            host.RenderFrame(16);
            stable = Math.Abs(spinner.WheelRotation - prev) < 0.001 ? stable + 1 : 0;
            prev = spinner.WheelRotation;
        }
    }

    [Fact]
    public void Spinner_Drag_RaisesNothingWhileHeld_ThenOnceAtRest()
    {
        var (host, spinner, robot, log) = NewSpinner();
        using var _ = host;
        int before = spinner.SelectedIndex;
        Assert.InRange(before, 0, 7);

        // turn the wheel by 100 degrees along an arc, stay still, release
        var start = OnCircle(0);
        robot.PointerDown(start.X, start.Y);
        for (int step = 1; step <= 10; step++)
        {
            var p = OnCircle(step * 10);
            robot.PointerMoveTo(p.X, p.Y);
        }
        robot.PointerHold(20);

        int whileHeld = spinner.SelectedIndex;
        Assert.NotEqual(before, whileHeld); // SelectedIndex follows the wheel live
        Assert.Empty(log.Raised);          // the event waits for the wheel to stop

        robot.PointerUp();
        SettleSpinner(host, spinner);

        _out.WriteLine($"{before} -> {spinner.SelectedIndex}, events [{string.Join(",", log.Raised)}]");

        Assert.NotEqual(before, spinner.SelectedIndex);
        Assert.Equal(new[] { spinner.SelectedIndex }, log.Raised);
    }

    [Fact]
    public void Spinner_Fling_RaisesAtMostOnce_WithTheIndexItLandsOn()
    {
        var (host, spinner, robot, log) = NewSpinner();
        using var _ = host;
        spinner.Deceleration = 0.003; // the default friction coasts for ~20 s
        int before = spinner.SelectedIndex;

        robot.Pan(OnCircle(-20), OnCircle(40), durationMs: 60, steps: 4);
        SettleSpinner(host, spinner);

        _out.WriteLine($"{before} -> {spinner.SelectedIndex}, rotation {spinner.WheelRotation:0.0}, " +
                       $"events [{string.Join(",", log.Raised)}]");

        if (spinner.SelectedIndex == before)
            Assert.Empty(log.Raised);
        else
            Assert.Equal(new[] { spinner.SelectedIndex }, log.Raised);

        // snapped exactly onto the item it reports
        Assert.True(Math.Abs(spinner.GetShortestRotationDistance(spinner.WheelRotation,
            spinner.GetRotationForIndex(spinner.SelectedIndex))) < 0.5);
    }

    [Fact]
    public void Spinner_SetFromCode_RaisesOnce()
    {
        var (host, spinner, robot, log) = NewSpinner();
        using var _ = host;
        var target = (spinner.SelectedIndex + 3) % 8;

        spinner.SelectedIndex = target;
        for (int i = 0; i < 60; i++) host.RenderFrame(16);

        Assert.Equal(target, spinner.SelectedIndex);
        Assert.Equal(new[] { target }, log.Raised);
    }

    [Fact]
    public void Spinner_AnimatedSpin_RaisesOnceWhenItStops()
    {
        var (host, spinner, robot, log) = NewSpinner();
        using var _ = host;
        var target = (spinner.SelectedIndex + 5) % 8;

        spinner.SpinToIndex(target, spins: 2, speed: 1000);
        for (int i = 0; i < 20; i++) host.RenderFrame(16);
        Assert.Empty(log.Raised); // passing items is not a change yet

        SettleSpinner(host, spinner);

        Assert.Equal(target, spinner.SelectedIndex);
        Assert.Equal(new[] { target }, log.Raised);
    }

    #endregion
}
