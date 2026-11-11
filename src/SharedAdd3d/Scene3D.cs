#nullable disable
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DrawnUi.Add3d;

internal sealed class Node3D
{
    public string Name = "";
    public int Parent = -1;
    public NodeTransform Rest;
    public int Mesh = -1;
    public int Skin = -1;
}

internal sealed class Primitive3D
{
    public Vector3[] Positions;
    public Vector3[] Normals;
    /// <summary>The directions an outline pushes each vertex along, given by a shape made in code; null = <see cref="OutlineDirections"/> makes them.</summary>
    public Vector3[] OutlineNormals;

    /// <summary>
    /// The outline directions: given, else the normals averaged per position, made the first time a GPU uploads this
    /// primitive and kept, so every control showing the scene and every GPU context reads the same array.
    /// </summary>
    // A race between two render threads only makes the same array twice; either is kept.
    public Vector3[] OutlineDirections() => OutlineNormals ??= Scene3D.AveragedNormals(Positions, Normals);
    public Vector2[] Uvs;
    /// <summary>Joint indices as floats, empty when not skinned.</summary>
    public Vector4[] Joints = Array.Empty<Vector4>();
    public Vector4[] Weights = Array.Empty<Vector4>();
    public uint[] Indices;
    public int Material = -1;

    /// <summary>The triangles' box tree for rays (taps, points hidden behind the model), made at the first ray; rigid primitives only.</summary>
    public RayTree Tree;
}

internal sealed class Material3D
{
    public string Name = "";
    /// <summary>Linear RGBA.</summary>
    public Vector4 Color = Vector4.One;
    public int Texture = -1;
    /// <summary>glTF roughnessFactor: 1 = matte (no highlight), toward 0 = glossy.</summary>
    public float Roughness = 1;
    public bool DoubleSided;
    public bool Blend;
}

internal sealed class Image3D
{
    /// <summary>Unpremultiplied RGBA.</summary>
    public byte[] Rgba;
    public int Width;
    public int Height;
}

internal sealed class Skin3D
{
    public int[] Joints;
    public Matrix4x4[] InverseBind;
}

internal enum ChannelPath
{
    Translation,
    Rotation,
    Scale
}

internal sealed class Channel3D
{
    public int Node;
    public ChannelPath Path;
    public bool Step;
    public float[] Times;
    /// <summary>3 or 4 floats per key.</summary>
    public float[] Values;
}

internal sealed class Animation3D
{
    public string Name = "";
    public float Duration;
    public Channel3D[] Channels;
}

/// <summary>
/// A glTF scene on the CPU: read from a .glb, posed by its animations, hit by rays. One read per bytes, shared by every
/// control that shows them.
/// </summary>
internal sealed partial class Scene3D
{
    public Node3D[] Nodes;
    public Primitive3D[][] Meshes;
    public Material3D[] Materials;
    public Image3D[] Images = Array.Empty<Image3D>();
    public Skin3D[] Skins = Array.Empty<Skin3D>();
    public Animation3D[] Animations = Array.Empty<Animation3D>();
    /// <summary>Node indices with every parent before its children.</summary>
    public int[] Order;
    /// <summary>The scene's box at rest.</summary>
    public Vector3 BoundsMin, BoundsMax;
    /// <summary>The most joints any skin has.</summary>
    public int MaxJoints;

    /// <summary>Files read, by their bytes: every control showing the same array shares one scene, and the GPU keeps one copy.</summary>
    static readonly ConditionalWeakTable<byte[], Scene3D> SharedScenes = new();

    /// <summary>The scene of these bytes, read once while the bytes live; null and why when they cannot be read.</summary>
    public static Scene3D Shared(byte[] bytes, out string error)
    {
        error = null;
        if (bytes == null)
        {
            error = "no bytes";
            return null;
        }
        if (SharedScenes.TryGetValue(bytes, out var scene))
            return scene;
        try
        {
            scene = Load(bytes);
        }
        catch (Exception e)
        {
            error = e.Message;
            return null;
        }
        SharedScenes.AddOrUpdate(bytes, scene);
        return scene;
    }

    /// <summary>A scene from parts made in code (Model3D): no skins, no animations, no images.</summary>
    public static Scene3D Assemble(Node3D[] nodes, Primitive3D[][] meshes, Material3D[] materials)
    {
        var scene = new Scene3D { Nodes = nodes, Meshes = meshes, Materials = materials };
        scene.Finish();
        return scene;
    }

    /// <summary>Order, joint count and rest bounds, after the parts are in.</summary>
    void Finish()
    {
        var depth = new int[Nodes.Length];
        for (var i = 0; i < Nodes.Length; i++)
        {
            var d = 0;
            for (var p = Nodes[i].Parent; p >= 0 && d <= Nodes.Length; p = Nodes[p].Parent)
                d++;
            depth[i] = d;
        }
        Order = Enumerable.Range(0, Nodes.Length).OrderBy(i => depth[i]).ToArray();
        MaxJoints = Skins.Length == 0 ? 0 : Skins.Max(s => s.Joints.Length);
        (BoundsMin, BoundsMax) = RestBounds();
    }

    /// <summary>
    /// One normal per position, the average of the normals there: an outline pushed along them stays closed at hard
    /// edges (a coin's rim, a box's corner). Positions are matched to 1/10000 of a unit, through a hash table in
    /// pooled arrays (no per-vertex allocation, no boxing).
    /// </summary>
    internal static Vector3[] AveragedNormals(Vector3[] positions, Vector3[] normals)
    {
        var n = positions?.Length ?? 0;
        var result = new Vector3[n];
        if (n == 0 || normals == null || normals.Length != n)
            return result;
        var size = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(16, n * 2));
        var mask = size - 1;
        var slots = ArrayPool<int>.Shared.Rent(size);
        var keys = ArrayPool<int>.Shared.Rent(n * 3);
        var groupOf = ArrayPool<int>.Shared.Rent(n);
        var sums = ArrayPool<Vector3>.Shared.Rent(n);
        try
        {
            System.Array.Fill(slots, -1, 0, size);
            var groups = 0;
            for (var i = 0; i < n; i++)
            {
                var p = positions[i];
                int x = Quantize(p.X), y = Quantize(p.Y), z = Quantize(p.Z);
                var h = (int)((uint)(x * 73856093 ^ y * 19349663 ^ z * 83492791) & (uint)mask);
                int g;
                while (true)
                {
                    g = slots[h];
                    if (g < 0)
                    {
                        g = groups++;
                        slots[h] = g;
                        keys[g * 3] = x;
                        keys[g * 3 + 1] = y;
                        keys[g * 3 + 2] = z;
                        sums[g] = Vector3.Zero;
                        break;
                    }
                    if (keys[g * 3] == x && keys[g * 3 + 1] == y && keys[g * 3 + 2] == z)
                        break;
                    h = (h + 1) & mask;
                }
                sums[g] += normals[i];
                groupOf[i] = g;
            }
            for (var i = 0; i < n; i++)
            {
                var s = sums[groupOf[i]];
                var l = s.Length();
                result[i] = l > 0 ? s / l : Vector3.Zero;
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(slots);
            ArrayPool<int>.Shared.Return(keys);
            ArrayPool<int>.Shared.Return(groupOf);
            ArrayPool<Vector3>.Shared.Return(sums);
        }
        return result;
    }

    /// <summary>A coordinate in 1/10000 of a unit, rounded half away from zero.</summary>
    static int Quantize(float v) => (int)(v * 10_000 + (v < 0 ? -0.5f : 0.5f));

    /// <summary>The animation with this name, -1 when none.</summary>
    public int AnimationNamed(string name)
    {
        for (var i = 0; i < Animations.Length; i++)
            if (Animations[i].Name == name)
                return i;
        return -1;
    }

    public int NodeNamed(string name)
    {
        for (var i = 0; i < Nodes.Length; i++)
            if (Nodes[i].Name == name)
                return i;
        return -1;
    }

    public int MaterialNamed(string name)
    {
        for (var i = 0; i < Materials.Length; i++)
            if (Materials[i].Name == name)
                return i;
        return -1;
    }

    /// <summary>Nodes hidden: those <paramref name="own"/> says, with everything under them.</summary>
    public void Hidden(ReadOnlySpan<bool> own, Span<bool> hidden)
    {
        foreach (var i in Order)
        {
            var p = Nodes[i].Parent;
            hidden[i] = own[i] || (p >= 0 && hidden[p]);
        }
    }

    /// <summary>The name of the material a node's primitive is drawn with ("" when none).</summary>
    public string MaterialOf(int node, int primitive)
    {
        var mesh = Nodes[node].Mesh;
        if (mesh < 0 || primitive >= Meshes[mesh].Length)
            return "";
        var m = Meshes[mesh][primitive].Material;
        return m >= 0 && m < Materials.Length ? Materials[m].Name : "";
    }

    /// <summary>Every node's own transform as the file gives it.</summary>
    public void Rest(Span<NodeTransform> local)
    {
        for (var i = 0; i < Nodes.Length; i++)
            local[i] = Nodes[i].Rest;
    }

    /// <summary>
    /// Writes an animation's values at its time into the nodes it moves; a looped one wraps, a once-only one stops at
    /// its end.
    /// </summary>
    public void Apply(in Track track, Span<NodeTransform> local)
    {
        if (track.Animation < 0 || track.Animation >= Animations.Length)
            return;
        var a = Animations[track.Animation];
        float t;
        if (a.Duration <= 0)
            t = 0;
        else if (track.Looped)
        {
            t = track.Time % a.Duration;
            if (t < 0)
                t += a.Duration;
        }
        else
            t = Math.Clamp(track.Time, 0, a.Duration);

        Span<float> v = stackalloc float[4];
        foreach (var c in a.Channels)
        {
            if (c.Node < 0 || c.Node >= local.Length)
                continue;
            ref var l = ref local[c.Node];
            switch (c.Path)
            {
                case ChannelPath.Translation:
                    Sample(c, t, 3, v);
                    l.T = new Vector3(v[0], v[1], v[2]);
                    break;
                case ChannelPath.Rotation:
                    Sample(c, t, 4, v);
                    l.R = Quaternion.Normalize(new Quaternion(v[0], v[1], v[2], v[3]));
                    break;
                case ChannelPath.Scale:
                    Sample(c, t, 3, v);
                    l.S = new Vector3(v[0], v[1], v[2]);
                    break;
            }
        }
    }

    /// <summary>Every node's world matrix from the local transforms, then placed by <paramref name="place"/>.</summary>
    public void World(ReadOnlySpan<NodeTransform> local, Span<Matrix4x4> world, in Matrix4x4 place)
    {
        foreach (var i in Order)
        {
            var own = local[i].ToMatrix();
            var p = Nodes[i].Parent;
            world[i] = p >= 0 ? own * world[p] : own * place;
        }
    }

    /// <summary>The world matrices with one looped animation at this time (bounds, tests).</summary>
    public Matrix4x4[] Pose(int animation, float time)
    {
        var local = new NodeTransform[Nodes.Length];
        Rest(local);
        if (animation >= 0)
            Apply(new Track(animation, time, true), local);
        var world = new Matrix4x4[Nodes.Length];
        World(local, world, Matrix4x4.Identity);
        return world;
    }

    /// <summary>
    /// Each joint's skinning matrix of a skin in this pose (inverse bind, then the joint's world), into
    /// <paramref name="joints"/>; returns how many.
    /// </summary>
    public int JointMatrices(int skin, ReadOnlySpan<Matrix4x4> pose, Span<Matrix4x4> joints)
    {
        var s = Skins[skin];
        var n = Math.Min(s.Joints.Length, joints.Length);
        for (var j = 0; j < n; j++)
            joints[j] = s.InverseBind[j] * pose[s.Joints[j]];
        return n;
    }

    /// <summary>A primitive's vertices in the scene as posed: skinned by its joints, else moved by its node.</summary>
    public void PosedPositions(int node, Primitive3D primitive, ReadOnlySpan<Matrix4x4> pose, Span<Vector3> result)
    {
        var skin = Nodes[node].Skin;
        var skinned = skin >= 0 && skin < Skins.Length && primitive.Joints.Length == primitive.Positions.Length;
        if (!skinned)
        {
            var m = pose[node];
            for (var v = 0; v < primitive.Positions.Length; v++)
                result[v] = Vector3.Transform(primitive.Positions[v], m);
            return;
        }

        var joints = new Matrix4x4[Skins[skin].Joints.Length];
        var count = JointMatrices(skin, pose, joints);
        for (var v = 0; v < primitive.Positions.Length; v++)
        {
            var at = primitive.Positions[v];
            var j = primitive.Joints[v];
            var w = primitive.Weights[v];
            var sum = Vector3.Zero;
            Add(ref sum, j.X, w.X);
            Add(ref sum, j.Y, w.Y);
            Add(ref sum, j.Z, w.Z);
            Add(ref sum, j.W, w.W);
            result[v] = sum;

            void Add(ref Vector3 total, float joint, float weight)
            {
                var k = (int)joint;
                if (k >= 0 && k < count)
                    total += Vector3.Transform(at, joints[k]) * weight;
            }
        }
    }

    /// <summary>
    /// The nearest surface a ray meets: node, primitive, distance; false when none. Hidden nodes and materials are
    /// passed through.
    /// </summary>
    public bool RayHit(ReadOnlySpan<Matrix4x4> pose, ReadOnlySpan<bool> hidden, ReadOnlySpan<MaterialOverride> materials,
        Vector3 from, Vector3 dir, out int hitNode, out int hitPrimitive, out float distance) =>
        Ray(pose, hidden, materials, from, dir, float.MaxValue, false, out hitNode, out hitPrimitive, out distance);

    /// <summary>
    /// Whether any surface lies on the ray closer than <paramref name="limit"/> (in units of <paramref name="dir"/>):
    /// stops at the first one found. Hidden nodes and materials are passed through.
    /// </summary>
    public bool RayBlocked(ReadOnlySpan<Matrix4x4> pose, ReadOnlySpan<bool> hidden, ReadOnlySpan<MaterialOverride> materials,
        Vector3 from, Vector3 dir, float limit) =>
        Ray(pose, hidden, materials, from, dir, limit, true, out _, out _, out _);

    /// <summary>Posed positions of skinned parts during a ray test, reused (rays run on the UI thread).</summary>
    [ThreadStatic] static Vector3[] _rayScratch;

    bool Ray(ReadOnlySpan<Matrix4x4> pose, ReadOnlySpan<bool> hidden, ReadOnlySpan<MaterialOverride> materials,
        Vector3 from, Vector3 dir, float limit, bool anyHit, out int hitNode, out int hitPrimitive, out float distance)
    {
        hitNode = -1;
        hitPrimitive = -1;
        distance = limit;
        for (var i = 0; i < Nodes.Length; i++)
        {
            var mesh = Nodes[i].Mesh;
            if (mesh < 0 || mesh >= Meshes.Length || (hidden.Length > i && hidden[i]))
                continue;
            var skin = Nodes[i].Skin;
            var primitives = Meshes[mesh];
            for (var p = 0; p < primitives.Length; p++)
            {
                var primitive = primitives[p];
                if (primitive.Material >= 0 && primitive.Material < materials.Length && materials[primitive.Material].Hidden)
                    continue;
                if (!(skin >= 0 && skin < Skins.Length && primitive.Joints.Length == primitive.Positions.Length))
                {
                    // A rigid part: the ray goes into the part's own space instead of every vertex out of it, and through
                    // its box tree. A point along a ray keeps its parameter through an affine map, so distances compare.
                    if (!Matrix4x4.Invert(pose[i], out var inverse))
                        continue;
                    primitive.Tree ??= RayTree.Build(primitive.Positions, primitive.Indices);
                    if (primitive.Tree.Hit(primitive.Positions, primitive.Indices, Vector3.Transform(from, inverse),
                            Vector3.TransformNormal(dir, inverse), ref distance, anyHit))
                    {
                        hitNode = i;
                        hitPrimitive = p;
                        if (anyHit)
                            return true;
                    }
                    continue;
                }
                // A skinned part: its vertices where the pose puts them, tested one by one.
                if (_rayScratch == null || _rayScratch.Length < primitive.Positions.Length)
                    _rayScratch = new Vector3[primitive.Positions.Length];
                PosedPositions(i, primitive, pose, _rayScratch);
                var at = _rayScratch;
                var idx = primitive.Indices;
                var n = (uint)primitive.Positions.Length;
                for (var k = 0; k + 2 < idx.Length; k += 3)
                {
                    uint a = idx[k], b = idx[k + 1], c = idx[k + 2];
                    if (a >= n || b >= n || c >= n)
                        continue;
                    if (RayTriangle(from, dir, at[(int)a], at[(int)b], at[(int)c], out var t) && t < distance)
                    {
                        distance = t;
                        hitNode = i;
                        hitPrimitive = p;
                        if (anyHit)
                            return true;
                    }
                }
            }
        }
        return hitNode >= 0;
    }

    (Vector3, Vector3) RestBounds()
    {
        var pose = Pose(-1, 0);
        var lo = new Vector3(float.MaxValue);
        var hi = new Vector3(float.MinValue);
        Vector3[] at = null;
        for (var i = 0; i < Nodes.Length; i++)
        {
            var mesh = Nodes[i].Mesh;
            if (mesh < 0 || mesh >= Meshes.Length)
                continue;
            foreach (var p in Meshes[mesh])
            {
                if (at == null || at.Length < p.Positions.Length)
                    at = new Vector3[p.Positions.Length];
                PosedPositions(i, p, pose, at);
                for (var v = 0; v < p.Positions.Length; v++)
                {
                    lo = Vector3.Min(lo, at[v]);
                    hi = Vector3.Max(hi, at[v]);
                }
            }
        }
        return lo.X > hi.X ? (new Vector3(-1), new Vector3(1)) : (lo, hi);
    }

    /// <summary>View and projection that fit the scene's box, turned by yaw / pitch (degrees).</summary>
    public Matrix4x4 Camera(float yaw, float pitch, float zoom, float aspect)
    {
        var center = (BoundsMin + BoundsMax) * 0.5f;
        var radius = MathF.Max((BoundsMax - BoundsMin).Length() * 0.5f, 1e-3f);
        var fov = Math3D.Radians(35);
        var fit = radius / MathF.Sin(fov * 0.5f) * (aspect < 1 ? 1 / aspect : 1);
        var distance = fit * zoom;
        // glam: from_rotation_y(yaw) * from_rotation_x(-pitch), applied to (0, 0, distance).
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, Math3D.Radians(yaw))
                       * Quaternion.CreateFromAxisAngle(Vector3.UnitX, Math3D.Radians(-pitch));
        var eye = center + Vector3.Transform(new Vector3(0, 0, distance), rotation);
        var view = Matrix4x4.CreateLookAt(eye, center, Vector3.UnitY);
        var proj = Math3D.PerspectiveGl(fov, aspect, MathF.Max(distance - radius * 2, distance * 0.01f), distance + radius * 2);
        return view * proj;
    }

    /// <summary>
    /// The ray through a point of the view (fx, fy from 0 to 1, from the top left): its start on the near plane and
    /// its direction.
    /// </summary>
    public static (Vector3 From, Vector3 Dir) Ray(in Matrix4x4 viewProj, float fx, float fy)
    {
        Matrix4x4.Invert(viewProj, out var inverse);
        float nx = fx * 2 - 1, ny = 1 - fy * 2;
        var near = Math3D.Project(inverse, new Vector3(nx, ny, -1));
        var far = Math3D.Project(inverse, new Vector3(nx, ny, 1));
        return (near, Vector3.Normalize(far - near));
    }

    /// <summary>Where a ray meets a triangle, both sides (Moller-Trumbore): the distance along the ray.</summary>
    public static bool RayTriangle(in Vector3 from, in Vector3 dir, in Vector3 a, in Vector3 b, in Vector3 c, out float t)
    {
        // Plain floats: a Debug web build interprets every vector operator as a call.
        t = 0;
        float e1x = b.X - a.X, e1y = b.Y - a.Y, e1z = b.Z - a.Z;
        float e2x = c.X - a.X, e2y = c.Y - a.Y, e2z = c.Z - a.Z;
        float px = dir.Y * e2z - dir.Z * e2y, py = dir.Z * e2x - dir.X * e2z, pz = dir.X * e2y - dir.Y * e2x;
        var det = e1x * px + e1y * py + e1z * pz;
        if (det > -1e-9f && det < 1e-9f)
            return false;
        var inv = 1 / det;
        float sx = from.X - a.X, sy = from.Y - a.Y, sz = from.Z - a.Z;
        var u = (sx * px + sy * py + sz * pz) * inv;
        if (u < 0 || u > 1)
            return false;
        float qx = sy * e1z - sz * e1y, qy = sz * e1x - sx * e1z, qz = sx * e1y - sy * e1x;
        var v = (dir.X * qx + dir.Y * qy + dir.Z * qz) * inv;
        if (v < 0 || u + v > 1)
            return false;
        t = (e2x * qx + e2y * qy + e2z * qz) * inv;
        return t > 0;
    }

    /// <summary>The value of a channel at <paramref name="t"/>, <paramref name="width"/> floats into <paramref name="result"/>.</summary>
    public static void Sample(Channel3D c, float t, int width, Span<float> result)
    {
        var keys = c.Times.Length;
        if (keys == 0 || c.Values.Length < keys * width)
        {
            result[..width].Clear();
            return;
        }
        // The first key after t (partition point of k <= t).
        int lo = 0, hi = keys;
        while (lo < hi)
        {
            var mid = (lo + hi) >> 1;
            if (c.Times[mid] <= t)
                lo = mid + 1;
            else
                hi = mid;
        }
        var next = lo;
        var values = c.Values.AsSpan();
        if (next == 0)
        {
            values.Slice(0, width).CopyTo(result);
            return;
        }
        if (next >= keys)
        {
            values.Slice((keys - 1) * width, width).CopyTo(result);
            return;
        }
        int k0 = next - 1, k1 = next;
        if (c.Step)
        {
            values.Slice(k0 * width, width).CopyTo(result);
            return;
        }
        var span = MathF.Max(c.Times[k1] - c.Times[k0], 1e-6f);
        var f = Math.Clamp((t - c.Times[k0]) / span, 0, 1);
        var a = values.Slice(k0 * width, width);
        var b = values.Slice(k1 * width, width);
        if (width == 4)
        {
            var q = Quaternion.Slerp(new Quaternion(a[0], a[1], a[2], a[3]), new Quaternion(b[0], b[1], b[2], b[3]), f);
            result[0] = q.X;
            result[1] = q.Y;
            result[2] = q.Z;
            result[3] = q.W;
            return;
        }
        for (var i = 0; i < width; i++)
            result[i] = a[i] + (b[i] - a[i]) * f;
    }
}
