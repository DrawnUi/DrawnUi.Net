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

/// <summary>Where loading a model's <see cref="SkiaModel3D.Source"/> stands.</summary>
public enum LoadState3D
{
    /// <summary>The file is on its way.</summary>
    Loading,
    /// <summary>The model is shown (also a model given as bytes or made in code).</summary>
    Ready,
    /// <summary>It could not be loaded or read: the reason is in <see cref="SkiaModel3D.Error"/>.</summary>
    Failed,
}

/// <summary>
/// Where a 3D point of a model shows in its control (<see cref="SkiaModel3D.ScreenPosition"/>): points from the control's
/// top left, and whether the model's own surface hides it (a pin on the far side of a globe, a hotspot behind the
/// model). Same as DrawnUI for Rust's PointOnScreen.
/// </summary>
public readonly record struct PointOnScreen(float X, float Y, bool Occluded);

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

/// <summary>A material changed from code (SetMaterialColor, SetMaterialVisible, SetMaterialImage).</summary>
internal struct MaterialOverride
{
    public bool HasColor;
    /// <summary>Linear RGBA.</summary>
    public Vector4 Color;
    public bool Hidden;
    /// <summary>An image in place of the file's own, null = the file's.</summary>
    public Texture3D Image;
    public bool HasRoughness;
    /// <summary>0 glossy .. 1 matte, in place of the file's roughness.</summary>
    public float Roughness;
}

/// <summary>
/// An image to put on a material (<see cref="SkiaModel3D.SetMaterialImage"/>, <see cref="SkiaScene3D.SetMaterialImage"/>):
/// a PNG / JPEG / WebP file, or any Skia image (a photo, a pattern drawn in code). One copy of its pixels is uploaded
/// once per GPU context, however many materials and controls show it. It wraps by the surface's texture coordinates
/// (the model file's, or those of the shapes made in code) and is multiplied by the material's color: a white material
/// shows it as it is.
/// </summary>
public sealed class Texture3D
{
    internal readonly Image3D Pixels;

    Texture3D(Image3D pixels) => Pixels = pixels;

    /// <summary>From encoded bytes (PNG, JPEG, WebP); null when they are not an image.</summary>
    public static Texture3D Decode(byte[] encoded)
    {
        var pixels = Scene3D.DecodeImage(encoded);
        return pixels == null ? null : new Texture3D(pixels);
    }

    /// <summary>From a Skia image in memory (decoded, or drawn on a raster surface); null for an image that lives on the GPU only.</summary>
    public static Texture3D FromImage(SkiaSharp.SKImage image)
    {
        var pixels = Scene3D.ImagePixels(image);
        return pixels == null ? null : new Texture3D(pixels);
    }

    /// <summary>Width in pixels.</summary>
    public int Width => Pixels.Width;

    /// <summary>Height in pixels.</summary>
    public int Height => Pixels.Height;
}

/// <summary>A node's own transform: translation, rotation, scale.</summary>
internal struct NodeTransform
{
    public Vector3 T;
    public Quaternion R;
    public Vector3 S;

    // `in`: past the 8 float argument registers, Mono AOT on arm64 (iOS Release) reads stack-passed float structs
    // wrong (dotnet/runtime#135197)
    public NodeTransform(in Vector3 t, in Quaternion r, in Vector3 s)
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
    /// <summary>
    /// <paramref name="a"/> times <paramref name="b"/>, as <c>a * b</c>, written out in floats: the WebAssembly interpreter
    /// runs <see cref="Matrix4x4"/>'s operator about six times slower (its vector code has no hardware there), and the
    /// JIT and AOT make this as fast. Used for every node and joint of every object, every frame.
    /// <paramref name="r"/> must not be <paramref name="a"/> or <paramref name="b"/>.
    /// </summary>
    public static void Multiply(in Matrix4x4 a, in Matrix4x4 b, out Matrix4x4 r)
    {
        r.M11 = a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41;
        r.M12 = a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42;
        r.M13 = a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43;
        r.M14 = a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44;
        r.M21 = a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41;
        r.M22 = a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42;
        r.M23 = a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43;
        r.M24 = a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44;
        r.M31 = a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41;
        r.M32 = a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42;
        r.M33 = a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43;
        r.M34 = a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44;
        r.M41 = a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41;
        r.M42 = a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42;
        r.M43 = a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43;
        r.M44 = a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44;
    }

    /// <summary>A UI color (sRGB) as a glTF material color (linear).</summary>
    public static Vector4 Linear(Color c) =>
        new(MathF.Pow(c.Red, 2.2f), MathF.Pow(c.Green, 2.2f), MathF.Pow(c.Blue, 2.2f), c.Alpha);

    /// <summary>A color for the shader as given (sRGB).</summary>
    public static Vector4 Srgb(Color c) => new(c.Red, c.Green, c.Blue, c.Alpha);

    public static float Radians(float degrees) => degrees * (MathF.PI / 180f);

    /// <summary>
    /// The samples per pixel smooth edges take on a GPU offering <paramref name="max"/>: 4, or fewer when that is all
    /// it has; 0 when it has fewer than 2 (no smoothing: the plain path). Same as DrawnUI for Rust's msaa_samples.
    /// </summary>
    public static int MsaaSamples(int max) => max < 2 ? 0 : Math.Min(max, 4);

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
    /// <summary>Samples per pixel asked for (smooth edges); 1 or 0 = none. The renderer uses at most what the GPU offers.</summary>
    public int Samples;
}

/// <summary>A control's frame: the texture it draws into with its depth buffer, and the image Skia shows of it.</summary>
internal sealed class RenderTarget
{
    /// <summary>The GPU context its objects belong to; another one means they are gone.</summary>
    public object Context;
    public uint Frame, Color, Depth;
    /// <summary>GL, smooth edges on: the multisampled frame drawn into, resolved into Frame's texture; 0 when off.</summary>
    public uint MsaaFrame, MsaaColor;
    /// <summary>Samples per pixel the frame was made with (1 = none).</summary>
    public int Samples = 1;
    /// <summary>The objects of a renderer other than GL (Vulkan, Metal), null on GL.</summary>
    public object Native;
    public int Width, Height;
    public SkiaSharp.SKImage Image;
}
