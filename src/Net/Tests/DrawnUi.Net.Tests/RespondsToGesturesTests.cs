using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// RespondsToGestures = false switches a control off for the user, the same way on every control: taps and keyboard
/// do nothing, a tap on it does not reach the control under it, and screen readers report it disabled (the snapshot
/// node's CanInteract), also when it changes at runtime. Code can still change the control.
/// SkiaSwitch / SkiaCheckbox / SkiaRadioButton used to toggle on a tap with RespondsToGestures = false while screen
/// readers already called them disabled; SkiaButton had its own IsDisabled (now an obsolete alias).
/// </summary>
public class RespondsToGesturesTests
{
    sealed class Scene : IDisposable
    {
        public readonly HeadlessCanvasHost Host = new(300, 300);
        public int TapsUnder;

        public Scene(SkiaControl control)
        {
            control.HorizontalOptions = LayoutOptions.Center;
            control.VerticalOptions = LayoutOptions.Center;
            control.AccessibilityLabel = "Probe";
            Host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
            Host.Canvas.Content = new SkiaLayer
            {
                VerticalOptions = LayoutOptions.Fill,
                Children =
                {
                    new SkiaShape { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill }
                        .OnTapped(me => TapsUnder++),
                    control,
                },
            };
            Host.AdvanceFrames(3);
        }

        public void TapCenter()
        {
            new GestureRobot(Host).Tap(150, 150);
            Host.AdvanceFrames(2);
        }

        public bool? SnapshotCanInteract(string label = "Probe") =>
            Host.Canvas.AccessibilityManager.Snapshot.FirstOrDefault(n => n.Label == label)?.CanInteract;

        public void Dispose() => Host.Dispose();
    }

    static SkiaToggle Toggle(string kind) => kind switch
    {
        "switch" => new SkiaSwitch { WidthRequest = 60, HeightRequest = 32 },
        "checkbox" => new SkiaCheckbox { WidthRequest = 32, HeightRequest = 32 },
        _ => new SkiaRadioButton { WidthRequest = 120, HeightRequest = 32, Text = "Probe" },
    };

    [Theory]
    [InlineData("switch")]
    [InlineData("checkbox")]
    [InlineData("radio")]
    public void Toggle_Off_TapDoesNotToggle_ReportedDisabled(string kind)
    {
        var toggle = Toggle(kind);
        toggle.RespondsToGestures = false;
        using var scene = new Scene(toggle);
        toggle.IsToggled = false; // a lone radio button selects itself when it appears

        scene.TapCenter();

        Assert.False(toggle.IsToggled);
        Assert.Equal(0, scene.TapsUnder);
        Assert.False(toggle.AccessibilityCanInteract);
        Assert.False(toggle.CanReceiveGesture(AppoMobi.Gestures.TouchActionResult.Tapped));
        Assert.False(scene.SnapshotCanInteract());

        toggle.RespondsToGestures = true;
        scene.Host.AdvanceFrames(2);
        Assert.True(scene.SnapshotCanInteract());
        scene.TapCenter();
        Assert.True(toggle.IsToggled);
    }

    [Fact]
    public void Button_Off_NoTapNoPress_TapStopsThere_ReportedDisabled()
    {
        int tapped = 0, down = 0;
        var button = new SkiaButton("Probe") { WidthRequest = 120, HeightRequest = 40, AccessibilityRole = Aria.RoleButton };
        button.Tapped += (_, _) => tapped++;
        button.Down += (_, _) => down++;
        button.RespondsToGestures = false;
        using var scene = new Scene(button);

        var robot = new GestureRobot(scene.Host);
        robot.PointerDown(150, 150);
        Assert.False(button.IsPressed);
        robot.PointerUp();
        scene.TapCenter();

        Assert.Equal(0, tapped);
        Assert.Equal(0, down);
        Assert.Equal(0, scene.TapsUnder);
        Assert.False(button.CanReceiveGesture(AppoMobi.Gestures.TouchActionResult.Tapped));
        Assert.False(scene.SnapshotCanInteract());

        button.RespondsToGestures = true;
        scene.Host.AdvanceFrames(2);
        Assert.True(scene.SnapshotCanInteract());
        scene.TapCenter();
        Assert.Equal(1, tapped);
    }

#pragma warning disable CS0618 // IsDisabled is the obsolete alias under test
    [Fact]
    public void Button_IsDisabled_IsTheOppositeOfRespondsToGestures()
    {
        var button = new SkiaButton("Probe") { IsDisabled = true };
        Assert.False(button.RespondsToGestures);
        Assert.True(button.IsDisabled);

        button.RespondsToGestures = true;
        Assert.False(button.IsDisabled);
    }
#pragma warning restore CS0618

    [Fact]
    public void Slider_Off_ReportedDisabled_KeyboardSkipsIt()
    {
        var slider = new SkiaSlider { WidthRequest = 200, HeightRequest = 40, AccessibilityRole = Aria.RoleSlider, RespondsToGestures = false };
        using var scene = new Scene(slider);

        Assert.False(slider.CanReceiveGesture(AppoMobi.Gestures.TouchActionResult.Panning));
        Assert.False(scene.SnapshotCanInteract());
    }

    /// <summary>A scroll that the user cannot move still lets its content take taps.</summary>
    [Fact]
    public void Scroll_Off_ContentStillTakesTaps()
    {
        int taps = 0;
        var scroll = new SkiaScroll
        {
            WidthRequest = 200,
            HeightRequest = 200,
            RespondsToGestures = false,
            Content = new SkiaShape { HorizontalOptions = LayoutOptions.Fill, HeightRequest = 400 }.OnTapped(me => taps++),
        };
        using var scene = new Scene(scroll);

        scene.TapCenter();

        Assert.Equal(1, taps);
        Assert.False(scroll.CanReceiveGesture(AppoMobi.Gestures.TouchActionResult.Panning));
    }
}
