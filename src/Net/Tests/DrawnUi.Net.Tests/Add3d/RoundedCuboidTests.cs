using System.Numerics;
using DrawnUi.Add3d;
using Xunit;

namespace DrawnUi.Net.Tests.Add3d;

/// <summary>Mesh3D.RoundedCuboid: a box with rounded edges and corners, its faces flat, its curve smooth.</summary>
public class RoundedCuboidTests
{
    [Fact]
    public void Its_points_stay_inside_the_box_and_its_corners_are_round()
    {
        var mesh = Mesh3D.RoundedCuboid(2, 1, 1, 0.2f);
        Assert.NotEmpty(mesh.Indices);
        Assert.All(mesh.Positions, p =>
        {
            Assert.InRange(MathF.Abs(p.X), 0, 1.0001f);
            Assert.InRange(MathF.Abs(p.Y), 0, 0.5001f);
            Assert.InRange(MathF.Abs(p.Z), 0, 0.5001f);
        });
        // no point at a sharp corner: the corner is cut by the rounding
        Assert.DoesNotContain(mesh.Positions, p => MathF.Abs(p.X) > 0.99f && MathF.Abs(p.Y) > 0.49f && MathF.Abs(p.Z) > 0.49f);
        // every normal is a unit vector pointing out
        Assert.All(mesh.Positions.Zip(mesh.Normals), pn =>
        {
            Assert.Equal(1, pn.Second.Length(), 3);
            Assert.True(Vector3.Dot(pn.First, pn.Second) > 0);
        });
        // the top face is flat out to where its edges start to curve, and faces up
        var top = mesh.Positions.Select((p, i) => (p, n: mesh.Normals[i])).Where(x => MathF.Abs(x.p.X) <= 0.8001f && MathF.Abs(x.p.Z) <= 0.3001f && x.p.Y > 0.4999f).ToList();
        Assert.NotEmpty(top);
        Assert.All(top, x => Assert.Equal(1, x.n.Y, 4));
    }

    [Fact]
    public void An_atlas_gives_each_face_its_own_cell()
    {
        var mesh = Mesh3D.RoundedCuboid(1, 1, 1, 0.1f, atlas: true);
        var perFace = mesh.Uvs.Count / 6;
        for (var f = 0; f < 6; f++)
        {
            var uvs = mesh.Uvs.Skip(f * perFace).Take(perFace).ToList();
            float col = f % 3, row = f / 3;
            Assert.All(uvs, uv =>
            {
                Assert.InRange(uv.X, col / 3 - 1e-4f, (col + 1) / 3 + 1e-4f);
                Assert.InRange(uv.Y, row / 2 - 1e-4f, (row + 1) / 2 + 1e-4f);
            });
        }
    }
}
