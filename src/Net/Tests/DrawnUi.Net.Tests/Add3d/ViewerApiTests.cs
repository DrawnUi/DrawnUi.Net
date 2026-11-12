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
[Collection("GlobalKeyboard")] // holds Ctrl in the process-wide KeyboardManager (ScrollZoomWheelTests.cs)
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
    public void Ctrl_wheel_zooms_only_with_PinchZoom_and_stays_in_range()
    {
        using var host = new HeadlessCanvasHost(400, 300, 1f, Colors.Black);
        var model = Model();
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { model } };
        Frames(host, 3);
        var robot = new GestureRobot(host);
        KeyboardManager.KeyboardPressed(InputKey.ControlLeft);
        try
        {
            CtrlWheel(host, robot, model);
        }
        finally
        {
            KeyboardManager.KeyboardReleased(InputKey.ControlLeft);
        }
    }

    /// <summary>
    /// A pinch zooms by the ratio of the finger distances; spreading past the nearest zoom and pinching back the same
    /// amount in one gesture returns to the start (the limit applies to the result, not to each step).
    /// </summary>
    [Fact]
    public void A_pinch_in_and_back_out_returns_to_the_start()
    {
        using var host = new HeadlessCanvasHost(400, 300, 1f, Colors.Black);
        var model = Model();
        model.PinchZoom = true;
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { model } };
        Frames(host, 3);
        var robot = new GestureRobot(host);

        robot.Pinch(200, 150, new[] { 1.5f });
        Frames(host, 2);
        Assert.Equal(1 / 1.5f, model.Zoom, 3);

        robot.Pinch(200, 150, new[] { 1 / 1.5f });
        Frames(host, 2);
        Assert.Equal(1, model.Zoom, 3);

        // past the limit (MinZoom 0.5) and back in the same gesture
        robot.Pinch(200, 150, new[] { 4f, 0.25f });
        Frames(host, 2);
        Assert.Equal(1, model.Zoom, 3);

        robot.Pinch(200, 150, new[] { 4f });
        Frames(host, 2);
        Assert.Equal(model.MinZoom, model.Zoom, 3);
    }

    /// <summary>
    /// A two-finger gesture never turns the model: not its fingers' own moves, not the jump between the two fingers'
    /// events, and not the last finger left after the other lifted. Turning is a one-finger drag.
    /// </summary>
    [Fact]
    public void A_pinch_does_not_turn_the_model()
    {
        using var host = new HeadlessCanvasHost(400, 300, 1f, Colors.Black);
        var model = Model();
        model.PinchZoom = true;
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { model } };
        Frames(host, 3);
        var robot = new GestureRobot(host);

        robot.Pinch(200, 150, new[] { 1 / 1.5f });
        Frames(host, 2);
        Assert.Equal(1.5f, model.Zoom, 3);
        Assert.Equal(0, model.Yaw, 1);
        Assert.Equal(0, model.Pitch, 1);

        // and a one-finger drag after it still turns
        robot.Pan(150, 150, 300, 150, 200, 12);
        Frames(host, 2);
        Assert.True(MathF.Abs(model.Yaw) > 20, $"turned: {model.Yaw}");
    }

    /// <summary>A touchpad pinch reaches a browser as a Ctrl wheel with no key held (IsZoom): it zooms, a plain wheel does not.</summary>
    [Fact]
    public void A_touchpad_pinch_zooms_without_a_key()
    {
        using var host = new HeadlessCanvasHost(400, 300, 1f, Colors.Black);
        var model = Model();
        model.PinchZoom = true;
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { model } };
        Frames(host, 3);
        var robot = new GestureRobot(host);

        robot.WheelScroll(200, 150, 100);
        Frames(host, 2);
        Assert.Equal(1, model.Zoom, 3);

        robot.WheelScroll(200, 150, 100, zoom: true);
        Frames(host, 2);
        Assert.True(model.Zoom < 0.95f, $"closer after a pinch: {model.Zoom}");
    }

    static void CtrlWheel(HeadlessCanvasHost host, GestureRobot robot, SkiaModel3D model)
    {
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
        KeyboardManager.KeyboardPressed(InputKey.ControlLeft);
        robot.WheelScroll(200, 150, 100);
        KeyboardManager.KeyboardReleased(InputKey.ControlLeft);
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

    /// <summary>
    /// A viewer on a scrolling page (no LockPitch) never traps the page: a vertical drag scrolls it and leaves the model
    /// alone, a sideways one turns the model (its vertical part tilts it) and does not scroll. Tilt cards the same.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void On_a_scrolling_page_vertical_drags_scroll_and_sideways_ones_turn(bool tiltCard)
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
                    new SkiaModel3D(Ball) { Yaw = 0, Pitch = 10, PinchZoom = true, HeightRequest = 300, HorizontalOptions = LayoutOptions.Fill }
                        .Adapt(m => { if (tiltCard) { m.Orbit = false; m.TiltToPointer = 20; } })
                        .Assign(out model),
                    new SkiaControl { HeightRequest = 2000 },
                }
            }
        }.Assign(out scroll);
        Frames(host, 3);
        var robot = new GestureRobot(host);

        robot.Pan(200, 250, 210, 80, 200, 12);
        robot.SettleFling(scroll);
        Assert.True(scroll.ViewportOffsetY < -60, $"the page scrolled: {scroll.ViewportOffsetY}");
        Assert.Equal(0, model.Yaw, 1);
        Assert.Equal(10, model.Pitch, 1);

        // A plain wheel over the viewer scrolls the page too; the view keeps its zoom.
        var offset = scroll.ViewportOffsetY;
        for (var i = 0; i < 3; i++)
            robot.WheelScroll(200, 100, -120);
        robot.SettleFling(scroll);
        Assert.True(scroll.ViewportOffsetY < offset - 40, $"the wheel scrolled: {offset} -> {scroll.ViewportOffsetY}");
        Assert.Equal(1, model.Zoom, 3);

        // Ctrl + wheel, then a touchpad pinch (IsZoom, no key), over the viewer zoom it; the page stays.
        scroll.ScrollTo(0, 0, 0, true);
        Frames(host, 3);
        KeyboardManager.KeyboardPressed(InputKey.ControlLeft);
        try
        {
            robot.WheelScroll(200, 100, 120);
        }
        finally
        {
            KeyboardManager.KeyboardReleased(InputKey.ControlLeft);
        }
        Frames(host, 3);
        var zoomed = model.Zoom;
        Assert.True(zoomed < 0.95f, $"Ctrl + wheel zoomed: {zoomed}");
        robot.WheelScroll(200, 100, 120, zoom: true);
        Frames(host, 3);
        Assert.True(model.Zoom < zoomed - 0.02f, $"a pinch zoomed: {zoomed} -> {model.Zoom}");
        Assert.Equal(0, scroll.ViewportOffsetY, 1);

        if (tiltCard)
            return;
        scroll.ScrollTo(0, 0, 0, true);
        Frames(host, 3);
        robot.Pan(100, 100, 300, 160, 200, 12);
        robot.SettleFling(scroll);
        Assert.True(MathF.Abs(model.Yaw) > 20, $"turned: {model.Yaw}");
        Assert.NotEqual(10, model.Pitch, 1);
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

    /// <summary>The surface follows the finger: a drag right moves the near side right, a drag down moves it down.</summary>
    [Fact]
    public void A_drag_moves_the_near_side_with_the_finger()
    {
        using var host = new HeadlessCanvasHost(400, 300, 1f, Colors.Black);
        var model = Model();
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { model } };
        Frames(host, 3);
        var near = new Vector3(0, 0, 1);
        var before = model.ScreenPosition(near).Value;
        var robot = new GestureRobot(host);
        robot.Pan(200, 150, 240, 150, 200, 8);
        Frames(host, 2);
        var after = model.ScreenPosition(near).Value;
        Assert.True(after.X > before.X + 5, $"right: {before.X} -> {after.X}");
        robot.Pan(200, 150, 200, 190, 200, 8);
        Frames(host, 2);
        var down = model.ScreenPosition(near).Value;
        Assert.True(down.Y > after.Y + 5, $"down: {after.Y} -> {down.Y}");
    }

    static void Until(HeadlessCanvasHost host, Func<bool> done)
    {
        for (var i = 0; i < 400 && !done(); i++)
        {
            Thread.Sleep(5);
            host.RenderFrame(16.7);
        }
    }

    [Fact]
    public void Source_loads_a_file_and_reports_its_states()
    {
        using var host = new HeadlessCanvasHost(400, 300, 1f, Colors.Black);
        var states = new List<LoadState3D>();
        var model = new SkiaModel3D { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
        model.LoadStateChanged += (_, state) => states.Add(state);
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { model } };
        Frames(host, 2);
        model.Source = "Assets/Add3d/dragon.glb";
        Until(host, () => model.LoadState != LoadState3D.Loading);
        Assert.Equal(new[] { LoadState3D.Loading, LoadState3D.Ready }, states);
        Assert.Null(model.Error);
        Assert.Contains(Add3dAssets.Idle, model.Animations);
        Assert.NotNull(model.ScreenPosition(Vector3.Zero));

        model.Source = "Assets/Add3d/no-such-model.glb";
        Until(host, () => model.LoadState != LoadState3D.Loading);
        Assert.Equal(LoadState3D.Failed, model.LoadState);
        Assert.NotNull(model.Error);
        Assert.Empty(model.Animations);
    }

    [Fact]
    public void TiltToPointer_leans_toward_the_finger_and_glides_home()
    {
        using var host = new HeadlessCanvasHost(400, 300, 1f, Colors.Black);
        var model = Model();
        model.Yaw = 10;
        model.Pitch = 5;
        model.TiltToPointer = 14;
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { model } };
        Frames(host, 3);
        var robot = new GestureRobot(host);
        // At the right edge and the bottom: yaw = home - 14, pitch = home + 14.
        robot.PointerDown(399, 299);
        robot.PointerHold(40);
        Assert.Equal(10 - 14, model.Yaw, 0);
        Assert.Equal(5 + 14, model.Pitch, 0);
        robot.PointerUp();
        Frames(host, 40);
        Assert.Equal(10, model.Yaw, 1);
        Assert.Equal(5, model.Pitch, 1);
        Assert.False(model.IsAnimating);
    }

    [Fact]
    public void ViewChanged_comes_at_most_once_a_frame_and_only_when_the_view_moves()
    {
        using var host = new HeadlessCanvasHost(400, 300, 1f, Colors.Black);
        var model = Model();
        var count = 0;
        model.ViewChanged += (_, _) => count++;
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { model } };
        Frames(host, 5);
        count = 0;
        Frames(host, 10);
        Assert.Equal(0, count);
        model.AutoRotate = 90;
        Frames(host, 10);
        Assert.InRange(count, 8, 10);
        model.AutoRotate = 0;
        Frames(host, 2);
        count = 0;
        model.Zoom = 1.4f;
        model.Yaw = 33;
        Frames(host, 3);
        Assert.Equal(1, count);
    }

    [Fact]
    public void SetMaterialRoughness_takes_known_materials_only()
    {
        var model = Model();
        Assert.True(model.SetMaterialRoughness("ball", 0.4f));
        Assert.True(model.SetMaterialRoughness("ball", null));
        Assert.False(model.SetMaterialRoughness("nothing", 0.4f));
    }
}
