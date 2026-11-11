using System.Numerics;
using DrawnUi.Add3d;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests.Add3d;

/// <summary>
/// The viewer API of SkiaModel3D, the same in DrawnUI for Rust: a 3D point on screen with far-side hiding
/// (ScreenPosition / screen_position), pinch and wheel zoom (PinchZoom), the view glided home (ResetView,
/// ResetOnDoubleTap), yaw-only drags that leave vertical ones to the list (LockPitch), and no frames off screen.
/// </summary>
public class ViewerApiTests
{
    static readonly Model3D Ball = new Model3D().Part("ball", Mesh3D.Sphere(1), Colors.White);

    static SkiaModel3D Model() => new(Ball)
    {
        Yaw = 0,
        Pitch = 0,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
    };

    static void Frames(HeadlessCanvasHost host, int n)
    {
        for (var i = 0; i < n; i++)
            host.RenderFrame(16.7);
    }

    [Fact]
    public void A_point_shows_where_it_projects_and_hides_on_the_far_side()
    {
        using var host = new HeadlessCanvasHost(400, 300, 1f, Colors.Black);
        var model = Model();
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { model } };
        Frames(host, 3);

        var center = model.ScreenPosition(Vector3.Zero);
        Assert.NotNull(center);
        Assert.Equal(200, center.Value.X, 1);
        Assert.Equal(150, center.Value.Y, 1);

        // Yaw 0 looks from +z: the front pole shows, the back one is behind the ball.
        var front = model.ScreenPosition(new Vector3(0, 0, 1)).Value;
        var back = model.ScreenPosition(new Vector3(0, 0, -1)).Value;
        Assert.False(front.Occluded);
        Assert.True(back.Occluded);
        // A point up the ball shows above the center (y down on screen).
        Assert.True(model.ScreenPosition(new Vector3(0, 0.9f, 0.4f)).Value.Y < 150);

        // Turned half way round, they swap.
        model.Yaw = 180;
        Frames(host, 1);
        Assert.True(model.ScreenPosition(new Vector3(0, 0, 1)).Value.Occluded);
        Assert.False(model.ScreenPosition(new Vector3(0, 0, -1)).Value.Occluded);

        // A hidden material hides nothing.
        model.SetMaterialVisible("ball", false);
        Assert.False(model.ScreenPosition(new Vector3(0, 0, 1)).Value.Occluded);
    }

    [Fact]
    public void No_size_yet_means_no_position()
    {
        var model = Model();
        Assert.Null(model.ScreenPosition(Vector3.Zero));
    }

    [Fact]
    public void The_wheel_zooms_only_with_PinchZoom_and_stays_in_range()
    {
        using var host = new HeadlessCanvasHost(400, 300, 1f, Colors.Black);
        var model = Model();
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { model } };
        Frames(host, 3);
        var robot = new GestureRobot(host);

        robot.WheelScroll(200, 150, 100);
        Frames(host, 2);
        Assert.Equal(1, model.Zoom, 3);

        model.PinchZoom = true;
        robot.WheelScroll(200, 150, 100);
        Frames(host, 2);
        Assert.True(model.Zoom < 0.95f, $"closer after a notch up: {model.Zoom}");
        for (var i = 0; i < 20; i++)
            robot.WheelScroll(200, 150, 100);
        Frames(host, 2);
        Assert.Equal(model.MinZoom, model.Zoom, 3);
        for (var i = 0; i < 40; i++)
            robot.WheelScroll(200, 150, -100);
        Frames(host, 2);
        Assert.Equal(model.MaxZoom, model.Zoom, 3);
    }

    [Fact]
    public void ResetView_glides_back_to_the_view_the_app_set()
    {
        using var host = new HeadlessCanvasHost(400, 300, 1f, Colors.Black);
        var model = Model();
        model.Yaw = 30;
        model.Pitch = 10;
        model.Zoom = 1.2f;
        model.PinchZoom = true;
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { model } };
        Frames(host, 3);
        var robot = new GestureRobot(host);

        robot.Pan(150, 100, 300, 200, 200, 12);
        robot.WheelScroll(200, 150, 100);
        Frames(host, 2);
        Assert.NotEqual(30, model.Yaw, 1);
        Assert.NotEqual(10, model.Pitch, 1);
        Assert.NotEqual(1.2f, model.Zoom, 3);

        model.ResetView();
        Frames(host, 30);
        Assert.Equal(30, model.Yaw, 1);
        Assert.Equal(10, model.Pitch, 1);
        Assert.Equal(1.2f, model.Zoom, 3);
        Assert.False(model.IsAnimating);
    }

    [Fact]
    public void Two_taps_reset_the_view_when_asked()
    {
        using var host = new HeadlessCanvasHost(400, 300, 1f, Colors.Black);
        var model = Model();
        model.Yaw = 30;
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { model } };
        Frames(host, 3);
        var robot = new GestureRobot(host);
        robot.Pan(150, 150, 300, 150, 200, 12);
        Frames(host, 2);
        var turned = model.Yaw;
        Assert.NotEqual(30, turned, 1);

        robot.Tap(200, 150);
        robot.Tap(200, 150);
        Frames(host, 30);
        Assert.Equal(turned, model.Yaw, 1);

        model.ResetOnDoubleTap = true;
        robot.Tap(200, 150);
        robot.Tap(200, 150);
        Frames(host, 30);
        Assert.Equal(30, model.Yaw, 1);
    }

    /// <summary>A thumbnail in a list: sideways drags turn it, vertical ones scroll the list, never both.</summary>
    [Fact]
    public void LockPitch_turns_on_sideways_drags_and_leaves_vertical_ones_to_the_list()
    {
        using var host = new HeadlessCanvasHost(400, 600, 1f, Colors.Black);
        SkiaModel3D model;
        SkiaScroll scroll;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Children =
                {
                    new SkiaModel3D(Ball) { Yaw = 0, Pitch = 10, LockPitch = true, HeightRequest = 200, HorizontalOptions = LayoutOptions.Fill }
                        .Assign(out model),
                    new SkiaControl { HeightRequest = 2000 },
                }
            }
        }.Assign(out scroll);
        Frames(host, 3);
        var robot = new GestureRobot(host);

        robot.Pan(200, 150, 200, 60, 200, 12);
        robot.SettleFling(scroll);
        Assert.True(scroll.ViewportOffsetY < -40, $"the list scrolled: {scroll.ViewportOffsetY}");
        Assert.Equal(0, model.Yaw, 1);
        Assert.Equal(10, model.Pitch, 1);

        scroll.ScrollTo(0, 0, 0, true);
        Frames(host, 3);
        robot.Pan(100, 100, 300, 110, 200, 12);
        robot.SettleFling(scroll);
        Assert.True(MathF.Abs(model.Yaw) > 20, $"turned: {model.Yaw}");
        Assert.Equal(10, model.Pitch, 1);
        Assert.Equal(0, scroll.ViewportOffsetY, 1);
    }

    /// <summary>A turning model scrolled off screen asks for no frames and its time stands still; back on screen it goes on.</summary>
    [Fact]
    public void A_model_off_screen_stops_moving_and_goes_on_when_back()
    {
        using var host = new HeadlessCanvasHost(400, 600, 1f, Colors.Black);
        SkiaModel3D model;
        SkiaScroll scroll;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Children =
                {
                    new SkiaModel3D(Ball) { Yaw = 0, AutoRotate = 90, HeightRequest = 200, HorizontalOptions = LayoutOptions.Fill }
                        .Assign(out model),
                    new SkiaControl { HeightRequest = 3000 },
                }
            }
        }.Assign(out scroll);
        Frames(host, 10);
        var moving = model.Yaw;
        Assert.True(moving > 5, $"turns on screen: {moving}");

        scroll.ScrollTo(0, -1500, 0, true);
        Frames(host, 3);
        var parked = model.Yaw;
        Frames(host, 30);
        Assert.Equal(parked, model.Yaw, 3);

        scroll.ScrollTo(0, 0, 0, true);
        Frames(host, 10);
        Assert.True(model.Yaw > parked + 5, $"goes on: {parked} -> {model.Yaw}");
    }

    [Fact]
    public void A_scene_layer_off_screen_stops_its_animations()
    {
        using var host = new HeadlessCanvasHost(400, 600, 1f, Colors.Black);
        SkiaScene3D layer;
        SkiaScroll scroll;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Children =
                {
                    new SkiaScene3D { HeightRequest = 200, HorizontalOptions = LayoutOptions.Fill }
                        .Model("dragon", Add3dAssets.Dragon)
                        .Assign(out layer),
                    new SkiaControl { HeightRequest = 3000 },
                }
            }
        }.Assign(out scroll);
        layer.Add("a", "dragon", 100, 150, 100);
        layer.Play("a", Add3dAssets.Idle);
        Frames(host, 10);
        Assert.True(layer.IsAnimating);

        scroll.ScrollTo(0, -1500, 0, true);
        Frames(host, 5);
        // Off screen: the next tick finds it was not drawn and stops the loop.
        Assert.False(host.NeedsFrame, "nothing asks for frames off screen");
    }

    /// <summary>Rays through the box tree meet exactly what testing every triangle meets.</summary>
    [Fact]
    public void The_ray_tree_finds_the_same_hits_as_every_triangle()
    {
        var scene = new Model3D()
            .Part("ring", Mesh3D.Torus(1, 0.35f), Colors.White).Turned(30, 0, 10)
            .Part("ball", Mesh3D.Sphere(0.5f), Colors.White).At(1.2f, 0.4f, 0.3f)
            .ToScene();
        var locals = new NodeTransform[scene.Nodes.Length];
        var pose = new Matrix4x4[scene.Nodes.Length];
        new Player3D(scene).Locals(scene, locals);
        scene.World(locals, pose, Matrix4x4.Identity);
        var materials = new MaterialOverride[scene.Materials.Length];
        var random = new Random(5);
        var hits = 0;
        for (var r = 0; r < 300; r++)
        {
            var from = new Vector3(random.NextSingle() * 3 - 1.2f, random.NextSingle() * 3 - 1.5f, 4);
            var dir = Vector3.Normalize(new Vector3((random.NextSingle() - 0.5f) * 0.3f, (random.NextSingle() - 0.5f) * 0.3f, -1));
            var tree = scene.RayHit(pose, Array.Empty<bool>(), materials, from, dir, out _, out _, out var distance);
            var best = float.MaxValue;
            for (var i = 0; i < scene.Nodes.Length; i++)
            {
                if (scene.Nodes[i].Mesh < 0)
                    continue;
                foreach (var primitive in scene.Meshes[scene.Nodes[i].Mesh])
                {
                    var at = new Vector3[primitive.Positions.Length];
                    scene.PosedPositions(i, primitive, pose, at);
                    for (var k = 0; k + 2 < primitive.Indices.Length; k += 3)
                        if (Scene3D.RayTriangle(from, dir, at[primitive.Indices[k]], at[primitive.Indices[k + 1]], at[primitive.Indices[k + 2]], out var t) && t < best)
                            best = t;
                }
            }
            Assert.Equal(best < float.MaxValue, tree);
            if (!tree)
                continue;
            hits++;
            Assert.Equal(best, distance, 3);
            Assert.True(scene.RayBlocked(pose, Array.Empty<bool>(), materials, from, dir, best + 0.01f));
            Assert.False(scene.RayBlocked(pose, Array.Empty<bool>(), materials, from, dir, best - 0.01f));
        }
        Assert.True(hits > 50, $"enough rays met the shapes: {hits}");
    }
}
