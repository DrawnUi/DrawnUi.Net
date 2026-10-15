using AppoMobi.Gestures;
using DrawnUi.Controls;
using DrawnUi.Draw;
using Xunit;

namespace UnitTests;

/// <summary>
/// MAUI IsEnabled on drawn controls (issue #368: a SkiaButton with IsEnabled = false still raised Tapped). False makes
/// the control and its children take no input: their gesture handling never runs, a press or tap stops at them
/// (nothing under reacts), moves pass on, keyboard eligibility and the accessibility state say disabled.
/// </summary>
public class IsEnabledInputTests
{
    class Probe : SkiaLayout, ISkiaGestureListener
    {
        public int Processed;

        public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
        {
            Processed++;
            return this;
        }
    }

    static SkiaGesturesParameters Gesture(TouchActionResult type) =>
        new() { Type = type, Event = new TouchActionEventArgs(0, TouchActionType.Pressed, new(10, 10), null, 1f) };

    [Fact]
    public void Disabled_GestureHandlingNeverRuns_PressAndTapStopHere()
    {
        var probe = new Probe { IsEnabled = false };

        Assert.Same(probe, probe.OnSkiaGestureEvent(Gesture(TouchActionResult.Down), GestureEventProcessingInfo.Empty));
        Assert.Same(probe, probe.OnSkiaGestureEvent(Gesture(TouchActionResult.Tapped), GestureEventProcessingInfo.Empty));
        Assert.Null(probe.OnSkiaGestureEvent(Gesture(TouchActionResult.Panning), GestureEventProcessingInfo.Empty));
        Assert.Equal(0, probe.Processed);

        probe.IsEnabled = true;
        probe.OnSkiaGestureEvent(Gesture(TouchActionResult.Tapped), GestureEventProcessingInfo.Empty);
        Assert.Equal(1, probe.Processed);
    }

    /// <summary>The issue's case: a disabled SkiaButton raises no Tapped.</summary>
    [Fact]
    public void DisabledButton_NoTapped()
    {
        int tapped = 0;
        var button = new SkiaButton("Probe") { IsEnabled = false };
        button.Tapped += (_, _) => tapped++;

        button.OnSkiaGestureEvent(Gesture(TouchActionResult.Down), GestureEventProcessingInfo.Empty);
        button.OnSkiaGestureEvent(Gesture(TouchActionResult.Tapped), GestureEventProcessingInfo.Empty);

        Assert.Equal(0, tapped);
        Assert.False(button.IsPressed);
    }

    [Fact]
    public void DisabledParent_ChildIsKeyboardIneligible_AndReportedDisabled()
    {
        var child = new Probe { AccessibilityCanInteract = true };
        var parent = new SkiaLayout { Children = { child } };
        Assert.True(child.CanReceiveGesture(TouchActionResult.Tapped));
        Assert.True(child.AccessibilityCanInteract);

        parent.IsEnabled = false;

        Assert.False(child.CanReceiveGesture(TouchActionResult.Tapped));
        Assert.False(child.AccessibilityCanInteract);
        Assert.False(parent.CanReceiveGesture(TouchActionResult.Tapped));

        parent.IsEnabled = true;
        Assert.True(child.AccessibilityCanInteract);
    }
}
