using System.Numerics;
using DrawnUi.Add3d;
using Xunit;

namespace DrawnUi.Net.Tests.Add3d;

/// <summary>The add-on's float matrix product gives what <see cref="Matrix4x4"/>'s operator gives.</summary>
public class Math3DTests
{
    /// <summary>Equal but for float rounding (the vector code adds in another order).</summary>
    static void Close(float expected, float actual) =>
        Assert.True(MathF.Abs(expected - actual) <= 1e-5f * MathF.Max(1, MathF.Abs(expected)), $"{expected} vs {actual}");

    [Fact]
    public void Multiply_matches_the_matrix_operator()
    {
        var random = new Random(3);
        Matrix4x4 Any()
        {
            var m = new Matrix4x4();
            for (var r = 0; r < 4; r++)
            for (var c = 0; c < 4; c++)
                m[r, c] = (float)(random.NextDouble() * 4 - 2);
            return m;
        }

        for (var i = 0; i < 100; i++)
        {
            var a = Any();
            var b = Any();
            Math3D.Multiply(in a, in b, out var r);
            var expected = a * b;
            for (var row = 0; row < 4; row++)
            for (var col = 0; col < 4; col++)
                Close(expected[row, col], r[row, col]);
        }
    }

    [Fact]
    public void A_posed_scene_is_the_same_as_with_the_matrix_operator()
    {
        var scene = Scene3D.Shared(Add3dAssets.Dragon, out _);
        var local = new NodeTransform[scene.Nodes.Length];
        scene.Rest(local);
        var place = Matrix4x4.CreateRotationY(0.7f) * Matrix4x4.CreateTranslation(3, -2, 1);
        var world = new Matrix4x4[scene.Nodes.Length];
        scene.World(local, world, place);
        foreach (var i in scene.Order)
        {
            var own = local[i].ToMatrix();
            var parent = scene.Nodes[i].Parent;
            var expected = own * (parent >= 0 ? world[parent] : place);
            for (var row = 0; row < 4; row++)
            for (var col = 0; col < 4; col++)
                Close(expected[row, col], world[i][row, col]);
        }
    }
}
