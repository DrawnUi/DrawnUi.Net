using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A .WhenPainted overlay is a static post-effect: hiding its control or a parent stops running post-effects,
/// but must not detach the overlay, which draws again once the control is shown.
/// </summary>
public class WhenPaintedVisibilityTests
{
    [Fact]
    public void Overlay_SurvivesParentHiddenAndShown()
    {
        using var host = new HeadlessCanvasHost(300, 200, scale: 1f, background: Colors.Black);
        var painted = 0;
        SkiaShape box = null;
        var parent = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaShape { BackgroundColor = Colors.SteelBlue, WidthRequest = 60, HeightRequest = 60 }
                    .Assign(out box)
                    .WhenPainted((ctx, _) =>
                    {
                        painted++;
                        return false;
                    }),
            },
        };
        host.Canvas.Content = parent;
        host.AdvanceFrames(3);
        Assert.True(painted > 0);

        parent.IsVisible = false;
        host.AdvanceFrames(3);
        parent.IsVisible = true;
        painted = 0;
        host.AdvanceFrames(3);

        Assert.Single(box.PostAnimators);
        Assert.True(painted > 0, "the overlay did not draw after the parent was shown again");
    }
}
