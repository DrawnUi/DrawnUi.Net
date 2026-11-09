#nullable disable
using System.Numerics;
using DrawnUi;

namespace DrawnUi.Add3d;

/// <summary>
/// How 3D is drawn (<see cref="SkiaModel3D.Look"/>, <see cref="SkiaScene3D.Look"/>).
/// </summary>
public enum Look
{
    /// <summary>Shaded by a light and the sky (the default).</summary>
    Lit,

    /// <summary>Two tones, lit and shade, like a cartoon.</summary>
    Toon,

    /// <summary>Each color as it is, no shading: an illustration (with an outline).</summary>
    Flat
}

/// <summary>
/// What a tap on a <see cref="SkiaModel3D"/> hit: the node and the material under the finger, and the point in the scene.
/// </summary>
/// <param name="Node">The glTF node's name ("" when it has none).</param>
/// <param name="Material">The material's name ("" when it has none).</param>
/// <param name="Point">Where the ray met the surface, in scene units.</param>
public sealed record ModelHit(string Node, string Material, Vector3 Point);

/// <summary>
/// What a tap on a <see cref="SkiaScene3D"/> hit: the object by its name, and the node and material of its model under the finger.
/// </summary>
/// <param name="Object">The object's name given to <see cref="SkiaScene3D.Add"/>.</param>
/// <param name="Node">The node of its model ("" when it has none).</param>
/// <param name="Material">The material of its model ("" when it has none).</param>
public sealed record SceneHit(string Object, string Node, string Material);

/// <summary>
/// An object's animation played with PlayOnce or Hold ended (<see cref="SkiaScene3D.AnimationEnded"/>).
/// </summary>
/// <param name="Object">The object's name.</param>
/// <param name="Animation">The animation's name.</param>
public sealed record SceneAnimationEnded(string Object, string Animation);

/// <summary>An animation playing: its index and seconds into it.</summary>
internal struct Track
{
    public int Animation;
    public float Time;
    public bool Looped;

    public Track(int animation, float time, bool looped)
    {
        Animation = animation;
        Time = time;
        Looped = looped;
    }
}

/// <summary>A material changed from code (SetMaterialColor, SetMaterialVisible).</summary>
internal struct MaterialOverride
{
    public bool HasColor;
    /// <summary>Linear RGBA.</summary>
    public Vector4 Color;
    public bool Hidden;
}

/// <summary>A node's own transform: translation, rotation, scale.</summary>
internal struct NodeTransform
{
    public Vector3 T;
    public Quaternion R;
    public Vector3 S;

    public NodeTransform(Vector3 t, Quaternion r, Vector3 s)
    {
        T = t;
        R = r;
        S = s;
    }

    /// <summary>The matrix in System.Numerics order (row vectors): scale, then rotation, then translation.</summary>
    public readonly Matrix4x4 ToMatrix()
    {
        var m = Matrix4x4.CreateFromQuaternion(R);
        m.M11 *= S.X; m.M12 *= S.X; m.M13 *= S.X;
        m.M21 *= S.Y; m.M22 *= S.Y; m.M23 *= S.Y;
        m.M31 *= S.Z; m.M32 *= S.Z; m.M33 *= S.Z;
        m.M41 = T.X; m.M42 = T.Y; m.M43 = T.Z;
        return m;
    }
}

/// <summary>Colors and the GL math both engines share.</summary>
internal static class Math3D
{
    /// <summary>A UI color (sRGB) as a glTF material color (linear).</summary>
    public static Vector4 Linear(Color c) =>
        new(MathF.Pow(c.Red, 2.2f), MathF.Pow(c.Green, 2.2f), MathF.Pow(c.Blue, 2.2f), c.Alpha);

    /// <summary>A color for the shader as given (sRGB).</summary>
    public static Vector4 Srgb(Color c) => new(c.Red, c.Green, c.Blue, c.Alpha);

    public static float Radians(float degrees) => degrees * (MathF.PI / 180f);

    /// <summary>Rotations around x, y, z in degrees, as glam's EulerRot::XYZ (x * y * z).</summary>
    public static Quaternion Euler(float x, float y, float z) =>
        Quaternion.CreateFromAxisAngle(Vector3.UnitX, Radians(x))
        * Quaternion.CreateFromAxisAngle(Vector3.UnitY, Radians(y))
        * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, Radians(z));

    /// <summary>A right-handed perspective with OpenGL's -1..1 depth (System.Numerics' own maps to 0..1).</summary>
    public static Matrix4x4 PerspectiveGl(float fovY, float aspect, float near, float far)
    {
        var f = 1f / MathF.Tan(fovY * 0.5f);
        return new Matrix4x4(
            f / aspect, 0, 0, 0,
            0, f, 0, 0,
            0, 0, (far + near) / (near - far), -1,
            0, 0, 2 * far * near / (near - far), 0);
    }

    /// <summary>A right-handed orthographic projection with OpenGL's -1..1 depth.</summary>
    public static Matrix4x4 OrthographicGl(float left, float right, float bottom, float top, float near, float far)
    {
        return new Matrix4x4(
            2 / (right - left), 0, 0, 0,
            0, 2 / (top - bottom), 0, 0,
            0, 0, -2 / (far - near), 0,
            -(right + left) / (right - left), -(top + bottom) / (top - bottom), -(far + near) / (far - near), 1);
    }

    /// <summary>A point through a projective matrix, divided by w.</summary>
    public static Vector3 Project(in Matrix4x4 m, Vector3 p)
    {
        var v = Vector4.Transform(new Vector4(p, 1), m);
        return new Vector3(v.X, v.Y, v.Z) / v.W;
    }
}

/// <summary>
/// One object to draw: its scene, every node's world matrix (the object's place included), the nodes hidden and the
/// materials changed.
/// </summary>
internal struct RenderItem
{
    public Scene3D Scene;
    public Matrix4x4[] Pose;
    public bool[] Hidden;
    public MaterialOverride[] Materials;
}

/// <summary>How a control draws its objects: the look, and an outline around every shape.</summary>
internal struct RenderStyle
{
    public Look Look;
    /// <summary>Outline width in pixels (0 = none).</summary>
    public float Outline;
    /// <summary>Outline color, sRGB, premultiplied by the shader.</summary>
    public Vector4 OutlineColor;
}

/// <summary>A control's frame: the texture it draws into with its depth buffer, and the image Skia shows of it.</summary>
internal sealed class RenderTarget
{
    /// <summary>The GPU context its objects belong to; another one means they are gone.</summary>
    public object Context;
    public uint Frame, Color, Depth;
    /// <summary>The objects of a renderer other than GL (Vulkan, Metal), null on GL.</summary>
    public object Native;
    public int Width, Height;
    public SkiaSharp.SKImage Image;
}
