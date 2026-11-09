using System.Numerics;
using DrawnUi.Add3d;
using DrawnUi.Draw;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests.Add3d;

/// <summary>Model3D / Mesh3D made in code (port of DrawnUI for Rust drawnui-add3d src/shapes.rs tests).</summary>
public class Shapes3DTests
{
    static SKPath Star()
    {
        var path = new SKPath();
        for (var i = 0; i < 10; i++)
        {
            var r = i % 2 == 0 ? 50f : 20f;
            var a = i * MathF.Tau / 10 - MathF.Tau / 4;
            var p = new SKPoint(50 + r * MathF.Cos(a), 50 + r * MathF.Sin(a));
            if (i == 0)
                path.MoveTo(p);
            else
                path.LineTo(p);
        }
        path.Close();
        return path;
    }

    /// <summary>Every triangle faces the way its vertices' normals say (outward, seen from outside).</summary>
    static bool FacesOutward(Mesh3D mesh)
    {
        for (var t = 0; t + 2 < mesh.Indices.Count; t += 3)
        {
            var a = mesh.Positions[(int)mesh.Indices[t]];
            var b = mesh.Positions[(int)mesh.Indices[t + 1]];
            var c = mesh.Positions[(int)mesh.Indices[t + 2]];
            var face = Vector3.Cross(b - a, c - a);
            // Degenerate triangles (a lathe's center, two equal points) have no facing; relative to the edges, as
            // .NET's fused cross product leaves ~1e-8 of rounding where Rust's gives exactly 0.
            var degenerate = face.Length() < 1e-5f * (b - a).Length() * (c - a).Length() + 1e-12f;
            if (!degenerate && Vector3.Dot(face, mesh.Normals[(int)mesh.Indices[t]]) <= 0)
                return false;
        }
        return true;
    }

    /// <summary>Every vertex's normal points away from the shape's middle (a convex shape, or a ring's tube).</summary>
    static bool NormalsPointOut(Mesh3D mesh, Func<Vector3, Vector3> middle)
    {
        for (var i = 0; i < mesh.Positions.Count; i++)
        {
            var p = mesh.Positions[i];
            if (Vector3.Dot(p - middle(p), mesh.Normals[i]) <= 0)
                return false;
        }
        return true;
    }

    [Fact]
    public void Round_shapes_point_their_normals_out()
    {
        Assert.True(NormalsPointOut(Mesh3D.Sphere(1), _ => Vector3.Zero), "sphere");
        Assert.True(NormalsPointOut(Mesh3D.Cylinder(1, 0.3f), _ => Vector3.Zero), "cylinder");
        Assert.True(NormalsPointOut(Mesh3D.Torus(1, 0.2f), p => Vector3.Normalize(new Vector3(p.X, 0, p.Z))), "torus");
    }

    /// <summary>A sphere is shaded round: each vertex's normal is the direction from the center (not its band's).</summary>
    [Fact]
    public void A_sphere_is_shaded_round()
    {
        var sphere = Mesh3D.Sphere(1);
        for (var i = 0; i < sphere.Positions.Count; i++)
            Assert.True(Vector3.Dot(Vector3.Normalize(sphere.Positions[i]), sphere.Normals[i]) > 0.9999f, $"vertex {i}");
    }

    [Fact]
    public void Shapes_face_outward()
    {
        foreach (var (name, mesh) in new[]
                 {
                     ("sphere", Mesh3D.Sphere(1)), ("cylinder", Mesh3D.Cylinder(1, 0.3f)), ("torus", Mesh3D.Torus(1, 0.2f)),
                     ("box", Mesh3D.Cuboid(1, 2, 0.5f)), ("star", Mesh3D.Extrude(Star(), 10)),
                 })
        {
            Assert.True(mesh.Indices.Count > 0, name);
            Assert.True(FacesOutward(mesh), name);
        }
    }

    [Fact]
    public void A_shape_with_points_in_a_line_is_cut_without_holes()
    {
        // A heart: its top dip and its bottom tip share x = 50.
        var path = new SKPath();
        path.MoveTo(50, 88);
        path.CubicTo(20, 65, 0, 45, 0, 28);
        path.CubicTo(0, 12, 12, 0, 27, 0);
        path.CubicTo(38, 0, 46, 7, 50, 15);
        path.CubicTo(54, 7, 62, 0, 73, 0);
        path.CubicTo(88, 0, 100, 12, 100, 28);
        path.CubicTo(100, 45, 80, 65, 50, 88);
        path.Close();
        var outline = Mesh3D.Contours(path)[0];
        var triangles = Mesh3D.EarClip(outline);
        Assert.Equal(outline.Count - 2, triangles.Count); // every corner but two is cut off once
        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a - o).X * (b - o).Y - (a - o).Y * (b - o).X;
        var area = triangles.Sum(t => MathF.Abs(Cross(outline[(int)t.A], outline[(int)t.B], outline[(int)t.C])) / 2);
        Assert.True(MathF.Abs(area - MathF.Abs(Mesh3D.SignedArea(outline))) < 1, $"the triangles cover the shape: {area}");
        Assert.DoesNotContain(triangles, t => Cross(outline[(int)t.A], outline[(int)t.B], outline[(int)t.C]) < 0);
    }

    [Fact]
    public void A_star_extrudes_into_caps_and_walls()
    {
        var mesh = Mesh3D.Extrude(Star(), 10);
        // Two caps of 10 - 2 triangles, ten walls of two triangles.
        Assert.Equal(2 * 8 + 10 * 2, mesh.TriangleCount);
    }

    [Fact]
    public void Parts_become_named_nodes_and_materials()
    {
        var scene = new Model3D()
            .Part("disc", Mesh3D.Cylinder(1, 0.2f), Color.FromRgb(245, 184, 46))
            .Turned(90, 0, 0)
            .Part("star", Mesh3D.Extrude(Star(), 10), Colors.White)
            .Inside("disc")
            .ToScene();
        Assert.Equal(new[] { "disc", "star" }, scene.Nodes.Select(n => n.Name));
        Assert.Equal(0, scene.Nodes[1].Parent);
        Assert.Equal("star", scene.Materials[1].Name);
        var size = scene.BoundsMax - scene.BoundsMin;
        Assert.True(MathF.Min(size.X, MathF.Min(size.Y, size.Z)) > 0);
    }

    [Fact]
    public void A_turned_disc_faces_the_viewer()
    {
        // Euler XYZ as glam: a cylinder (axis y) turned 90 degrees around x lies along z, so it is thin in z.
        var scene = new Model3D().Part("disc", Mesh3D.Cylinder(1, 0.2f), Colors.White).Turned(90, 0, 0).ToScene();
        var size = scene.BoundsMax - scene.BoundsMin;
        Assert.InRange(size.Z, 0.19f, 0.21f);
        Assert.InRange(size.Y, 1.99f, 2.01f);
    }

    [Fact]
    public void Gl_matrices_project_like_glam()
    {
        // Perspective: the near plane maps to -1 and the far one to 1 (GL depth), not 0..1.
        var p = Math3D.PerspectiveGl(MathF.PI / 2, 1, 1, 10);
        Assert.Equal(-1, Math3D.Project(p, new Vector3(0, 0, -1)).Z, 4);
        Assert.Equal(1, Math3D.Project(p, new Vector3(0, 0, -10)).Z, 4);
        // Orthographic: the layer's points from (0, 0) at the top left to (w, -h).
        var o = Math3D.OrthographicGl(0, 200, -100, 0, -50, 50);
        var corner = Math3D.Project(o, new Vector3(200, -100, 0));
        Assert.Equal(1, corner.X, 4);
        Assert.Equal(-1, corner.Y, 4);
    }
}
