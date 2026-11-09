using SkiaSharp;
using System.Numerics;
using DrawnUi.Add3d;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests.Add3d;

/// <summary>
/// The 3D add-on's SkiaModel3D (port of DrawnUI for Rust drawnui-add3d tests/scene.rs and the lib tests): a glb read
/// with its animations, moves played once and back to the loop, held, taps that tell the node and material, nodes and
/// materials by name.
/// </summary>
public class Model3DSceneTests
{
    sealed class Scene : IDisposable
    {
        public readonly HeadlessCanvasHost Host = new(400, 400, 1f, Colors.Black);
        public readonly SkiaModel3D Boss;
        public readonly List<string> Ended = new();
        public readonly List<ModelHit> Hits = new();

        public Scene()
        {
            Host.Canvas.Content = new SkiaLayer
            {
                VerticalOptions = LayoutOptions.Fill,
                Children =
                {
                    new SkiaModel3D(Add3dAssets.Dragon)
                        {
                            Animation = Add3dAssets.Idle,
                            Yaw = 0,
                            Pitch = 0,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill,
                        }
                        .Assign(out Boss)
                        .OnAnimationEnded((_, name) => Ended.Add(name))
                        .OnTapped3D((_, hit) => Hits.Add(hit)),
                }
            };
            // The idle loop never settles: a few frames lay it out.
            Frames(0.05f);
        }

        public void Frames(float seconds)
        {
            for (var i = 0; i < (int)(seconds * 60); i++)
                Host.RenderFrame(16.7);
        }

        public void Tap(float x, float y)
        {
            new GestureRobot(Host).Tap(x, y, 16.7);
            Frames(0.1f);
        }

        public void Dispose() => Host.Dispose();
    }

    [Fact]
    public void A_skinned_glb_loads_with_its_animations_and_moves_over_time()
    {
        var scene = Scene3D.Load(Add3dAssets.Dragon);
        Assert.Equal(8, scene.Animations.Length);
        Assert.Contains(scene.Animations, a => a.Name == Add3dAssets.Punch);
        Assert.Single(scene.Skins);
        Assert.Equal(4350, scene.Meshes.SelectMany(m => m).Sum(p => p.Positions.Length));
        var size = scene.BoundsMax - scene.BoundsMin;
        Assert.True(MathF.Min(size.X, MathF.Min(size.Y, size.Z)) > 0, $"a real box: {scene.BoundsMin} {scene.BoundsMax}");
        var punch = scene.AnimationNamed(Add3dAssets.Punch);
        var a = scene.Pose(punch, 0);
        var b = scene.Pose(punch, 0.4f);
        Assert.True(a.Zip(b).Any(p => !Near(p.First, p.Second)), "the bones move");
    }

    [Fact]
    public void Bounds_go_through_the_bones()
    {
        // glTF skinned meshes ignore their node's transform: the box holds the skinned vertices, not the raw ones.
        var scene = Scene3D.Load(Add3dAssets.Dragon);
        var pose = scene.Pose(-1, 0);
        var node = Array.FindIndex(scene.Nodes, n => n.Mesh >= 0);
        var primitive = scene.Meshes[scene.Nodes[node].Mesh][0];
        var posed = new Vector3[primitive.Positions.Length];
        scene.PosedPositions(node, primitive, pose, posed);
        Assert.All(posed, v => Assert.InRange(v.Y, scene.BoundsMin.Y - 1e-3f, scene.BoundsMax.Y + 1e-3f));
        Assert.True(posed.Zip(primitive.Positions).Any(p => Vector3.Distance(p.First, p.Second) > 1e-3f), "the skin moves the vertices");
    }

    [Fact]
    public void A_broken_file_reports_why()
    {
        var model = new SkiaModel3D("not a model"u8.ToArray());
        Assert.False(string.IsNullOrEmpty(model.Error));
        Assert.Empty(model.Animations);
        Assert.Throws<InvalidDataException>(() => Scene3D.Load("not a model"u8.ToArray()));
    }

    [Fact]
    public void The_same_bytes_are_read_once()
    {
        var a = Scene3D.Shared(Add3dAssets.Dragon, out _);
        var b = Scene3D.Shared(Add3dAssets.Dragon, out _);
        Assert.Same(a, b);
    }

    /// <summary>
    /// The outline directions are made once per scene: the first upload makes them, every later upload (another
    /// control showing the same file, another GPU context) gets the same arrays. They used to be rebuilt at each
    /// upload, which blocked the first frame of a page.
    /// </summary>
    [Fact]
    public void Outline_directions_are_made_once_per_scene()
    {
        var scene = Scene3D.Shared(Add3dAssets.Dragon, out _);
        var primitives = scene.Meshes.SelectMany(m => m).ToArray();
        var first = primitives.Select(p => p.OutlineDirections()).ToArray();
        Assert.All(primitives, p => Assert.Equal(p.Positions.Length, p.OutlineDirections().Length));
        // The same bytes give the same scene, and its primitives keep the arrays made the first time.
        var again = Scene3D.Shared(Add3dAssets.Dragon, out _).Meshes.SelectMany(m => m).Select(p => p.OutlineDirections()).ToArray();
        for (var i = 0; i < first.Length; i++)
            Assert.Same(first[i], again[i]);
    }

    /// <summary>Smooth 3D edges are opt-in: off by default, switchable at any time.</summary>
    [Fact]
    public void Smooth_edges_are_off_by_default()
    {
        using var s = new Scene();
        Assert.False(s.Boss.Antialias);
        Assert.False(new SkiaModel3D(new Model3D().Part("box", Mesh3D.Cuboid(1, 1, 1), Colors.White)).Antialias);
        s.Boss.Antialias = true;
        s.Frames(0.05f);
        Assert.True(s.Boss.Antialias);
        s.Boss.Antialias = false;
        s.Frames(0.05f);
        Assert.False(s.Boss.Antialias);
    }

    /// <summary>Vertices at one position (a hard edge) share one outline direction, the average of their normals.</summary>
    [Fact]
    public void Outline_directions_average_the_normals_at_one_position()
    {
        var positions = new[] { new Vector3(1, 1, 1), new Vector3(1, 1, 1), new Vector3(0, 0, 0), new Vector3(1, 1, 1.00000001f) };
        var normals = new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.UnitZ };
        var result = Scene3D.AveragedNormals(positions, normals);
        var corner = Vector3.Normalize(new Vector3(1, 1, 1));
        Assert.True(Vector3.Distance(corner, result[0]) < 1e-5f, result[0].ToString());
        Assert.Equal(result[0], result[1]);
        Assert.Equal(result[0], result[3]);
        Assert.Equal(Vector3.UnitZ, result[2]);
    }

    [Fact]
    public void A_move_played_once_ends_and_goes_back_to_the_loop()
    {
        using var s = new Scene();
        Assert.Equal(Add3dAssets.Idle, s.Boss.CurrentAnimation);
        Assert.True(s.Boss.PlayOnce(Add3dAssets.Punch));
        Assert.False(s.Boss.PlayOnce("no such move"));
        s.Frames(0.3f);
        Assert.Equal(Add3dAssets.Punch, s.Boss.CurrentAnimation);
        Assert.Empty(s.Ended);
        s.Frames(3);
        Assert.Equal(new[] { Add3dAssets.Punch }, s.Ended); // told once
        Assert.Equal(Add3dAssets.Idle, s.Boss.CurrentAnimation); // back to idle
        Assert.True(s.Boss.IsAnimating, "idle keeps looping");
    }

    [Fact]
    public void A_held_move_stays_on_its_last_frame_and_frames_stop()
    {
        using var s = new Scene();
        Assert.True(s.Boss.Hold(Add3dAssets.Death));
        s.Frames(4);
        Assert.Equal(Add3dAssets.Death, s.Boss.CurrentAnimation);
        Assert.False(s.Boss.IsAnimating, "nothing moves any more");
        s.Frames(0.1f);
        Assert.False(s.Host.NeedsFrame, "the canvas asks for no frames");
    }

    [Fact]
    public void A_tap_on_the_boss_tells_the_node_and_material_a_tap_beside_it_nothing()
    {
        using var s = new Scene();
        s.Tap(200, 200);
        var hit = Assert.Single(s.Hits);
        Assert.False(string.IsNullOrEmpty(hit.Node), hit.ToString());
        Assert.False(string.IsNullOrEmpty(hit.Material), hit.ToString());
        s.Tap(3, 3);
        Assert.Single(s.Hits); // the empty corner hits nothing
        // A hidden material lets the ray through to what is behind it.
        Assert.True(s.Boss.SetMaterialVisible(hit.Material, false));
        s.Tap(200, 200);
        Assert.All(s.Hits.Skip(1), h => Assert.NotEqual(hit.Material, h.Material));
        // A hidden node is not hit at all.
        Assert.True(s.Boss.SetNodeVisible(hit.Node, false));
        var before = s.Hits.Count;
        s.Tap(200, 200);
        Assert.Equal(before, s.Hits.Count);
    }

    [Fact]
    public void Nodes_and_materials_by_name()
    {
        using var s = new Scene();
        var nodes = s.Boss.NodeNames;
        Assert.Contains("Dragon_Main", s.Boss.MaterialNames);
        Assert.True(s.Boss.SetMaterialColor("Dragon_Main", Color.FromRgb(40, 120, 220)));
        Assert.True(s.Boss.SetMaterialColor("Dragon_Main", null));
        Assert.False(s.Boss.SetMaterialColor("Nothing", null));
        Assert.True(s.Boss.SetNodeTranslation(nodes[0], 0, 1, 0));
        Assert.True(s.Boss.SetNodeRotation(nodes[0], 0, 90, 0));
        Assert.True(s.Boss.ResetNode(nodes[0]));
        Assert.False(s.Boss.SetNodeVisible("Nothing", false));
    }

    [Fact]
    public void An_image_goes_on_a_material_by_name()
    {
        using var s = new Scene();
        using var surface = SKSurface.Create(new SKImageInfo(8, 4));
        surface.Canvas.Clear(new SKColor(200, 30, 30));
        using var snapshot = surface.Snapshot();
        var drawn = Texture3D.FromImage(snapshot);
        Assert.NotNull(drawn);
        Assert.Equal((8, 4), (drawn.Width, drawn.Height));
        Assert.Null(Texture3D.Decode("not an image"u8.ToArray()));
        Assert.True(s.Boss.SetMaterialImage("Dragon_Main", drawn));
        Assert.True(s.Boss.SetMaterialImage("Dragon_Main", null));
        Assert.False(s.Boss.SetMaterialImage("Nothing", null));
    }

    [Fact]
    public void A_drag_turns_the_model()
    {
        using var s = new Scene();
        var yaw = s.Boss.Yaw;
        new GestureRobot(s.Host).Pan(100, 200, 300, 200, 200, 12);
        Assert.True(s.Boss.Yaw > yaw + 30, $"yaw {yaw} -> {s.Boss.Yaw}");
    }

    static bool Near(Matrix4x4 a, Matrix4x4 b)
    {
        for (var r = 0; r < 4; r++)
        for (var c = 0; c < 4; c++)
            if (MathF.Abs(a[r, c] - b[r, c]) > 1e-4f)
                return false;
        return true;
    }
}
