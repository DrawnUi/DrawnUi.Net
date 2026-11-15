using System.Numerics;
using DrawnUi.Add3d;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests.Add3d;

/// <summary>A model viewer fits its model tightly: never cut, whatever the view's shape, turn or tilt.</summary>
public class FramingTests
{
    static readonly Vector3 Half = new(1.3f, 0.5f, 0.5f);

    static IEnumerable<Vector3> Corners(Vector3 half, float margin = 0)
    {
        var h = half + new Vector3(margin);
        for (var i = 0; i < 8; i++)
            yield return new Vector3((i & 1) == 0 ? -h.X : h.X, (i & 2) == 0 ? -h.Y : h.Y, (i & 4) == 0 ? -h.Z : h.Z);
    }

    static (float MinX, float MaxX, float MinY, float MaxY) Spread(SkiaModel3D model, IEnumerable<Vector3> points)
    {
        var at = points.Select(p => model.ScreenPosition(p).Value).ToList();
        return (at.Min(p => p.X), at.Max(p => p.X), at.Min(p => p.Y), at.Max(p => p.Y));
    }

    [Theory]
    [InlineData(800, 250, 0, 0, true)]
    [InlineData(800, 250, 37, 30, true)]
    [InlineData(200, 400, 0, 0, true)]
    [InlineData(200, 400, 63, 60, true)]
    [InlineData(300, 300, 125, -40, true)]
    // nobody turns it: fitted exactly as seen
    [InlineData(800, 250, 0, 32, false)]
    [InlineData(200, 400, 0, 32, false)]
    [InlineData(300, 300, 125, -40, false)]
    public void The_whole_box_stays_in_view(int width, int height, float yaw, float pitch, bool orbit)
    {
        using var host = new HeadlessCanvasHost(width, height, 1f, Colors.Black);
        var model = new SkiaModel3D(new Model3D().Part("box", Mesh3D.Cuboid(2.6f, 1, 1), Colors.White))
        {
            Yaw = yaw, Pitch = pitch, FrameMargin = 0.3f, Orbit = orbit,
            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
        };
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { model } };
        for (var i = 0; i < 3; i++)
            host.RenderFrame(16.7);

        var (minX, maxX, minY, maxY) = Spread(model, Corners(Half, 0.3f));
        Assert.True(minX >= -0.5 && maxX <= width + 0.5 && minY >= -0.5 && maxY <= height + 0.5,
            $"x {minX:0}..{maxX:0} of {width}, y {minY:0}..{maxY:0} of {height}");
        // and tight: the box spans most of the view along the side that limits it
        var fill = orbit ? 0.55 : 0.75;
        Assert.True((maxX - minX) / width > fill || (maxY - minY) / height > fill,
            $"x {minX:0}..{maxX:0} of {width}, y {minY:0}..{maxY:0} of {height}");
    }

    /// <summary>Turning a globe to a city tilts it: the globe keeps its size on screen, the camera does not move away.</summary>
    [Fact]
    public void A_ball_keeps_its_size_when_tilted()
    {
        using var host = new HeadlessCanvasHost(600, 400, 1f, Colors.Black);
        var model = new SkiaModel3D(new Model3D().Part("earth", Mesh3D.Sphere(1), Colors.White))
        {
            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
        };
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { model } };
        for (var i = 0; i < 3; i++)
            host.RenderFrame(16.7);

        float Width()
        {
            // the ball's edge across, seen from the camera: the point at a right angle to the view
            var right = Vector3.Transform(Vector3.UnitX, Quaternion.CreateFromAxisAngle(Vector3.UnitY, model.Yaw * MathF.PI / 180));
            return MathF.Abs(model.ScreenPosition(right).Value.X - model.ScreenPosition(-right).Value.X);
        }
        var level = Width();
        foreach (var pitch in new[] { 20f, 45f, 70f, -40f })
        {
            model.Pitch = pitch;
            host.RenderFrame(16.7);
            Assert.InRange(Width(), level * 0.97f, level * 1.03f);
        }
    }
}
