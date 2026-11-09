#nullable disable
using System.Numerics;
using DrawnUi;
using DrawnUi.Draw;
using SkiaSharp;

namespace DrawnUi.Add3d;

/// <summary>
/// A mesh made in code: smooth shapes (turned profiles, spheres, rings, boxes, any 2D path pushed into depth). Put
/// them together as named, colored parts with <see cref="Model3D"/>.
/// </summary>
public sealed class Mesh3D
{
    internal readonly List<Vector3> Positions = new();
    internal readonly List<Vector3> Normals = new();
    internal readonly List<uint> Indices = new();
    /// <summary>Outline directions per vertex; empty = the normals averaged per position.</summary>
    internal readonly List<Vector3> Outline = new();

    void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
    {
        var at = (uint)Positions.Count;
        Positions.Add(a);
        Positions.Add(b);
        Positions.Add(c);
        Positions.Add(d);
        for (var i = 0; i < 4; i++)
            Normals.Add(normal);
        Indices.AddRange(new[] { at, at + 1, at + 2, at, at + 2, at + 3 });
    }

    /// <summary>
    /// A profile turned around the vertical axis: (radius, height) points from bottom to top (a cup, a vase, a coin's
    /// rim). Each step of the profile is its own band, so corners stay sharp.
    /// </summary>
    public static Mesh3D Lathe(IReadOnlyList<(float Radius, float Height)> profile, int segments)
    {
        segments = Math.Max(segments, 3);
        var mesh = new Mesh3D();
        for (var k = 0; k + 1 < profile.Count; k++)
        {
            var (r0, y0) = profile[k];
            var (r1, y1) = profile[k + 1];
            // The band's outward normal in the profile plane.
            var edge = new Vector2(y1 - y0, -(r1 - r0));
            var len = edge.Length();
            var (nr, ny) = len > 0 ? (edge.X / len, edge.Y / len) : (0f, 0f);
            var at = (uint)mesh.Positions.Count;
            for (var s = 0; s <= segments; s++)
            {
                var a = s / (float)segments * MathF.Tau;
                var (sin, cos) = MathF.SinCos(a);
                mesh.Positions.Add(new Vector3(r0 * cos, y0, r0 * sin));
                mesh.Normals.Add(new Vector3(nr * cos, ny, nr * sin));
                mesh.Positions.Add(new Vector3(r1 * cos, y1, r1 * sin));
                mesh.Normals.Add(new Vector3(nr * cos, ny, nr * sin));
            }
            for (uint s = 0; s < segments; s++)
            {
                uint a = at + s * 2, b = at + s * 2 + 1, c = at + s * 2 + 3, d = at + s * 2 + 2;
                mesh.Indices.AddRange(new[] { a, b, c, a, c, d });
            }
        }
        return mesh;
    }

    /// <summary>A disc or a short cylinder (a coin, a button, a medal), <paramref name="height"/> thick, centered.</summary>
    public static Mesh3D Cylinder(float radius, float height)
    {
        var h = height / 2;
        return Lathe(new[] { (0f, -h), (radius, -h), (radius, h), (0f, h) }, 64);
    }

    /// <summary>A sphere (a ball, a dot of a loader).</summary>
    public static Mesh3D Sphere(float radius)
    {
        const int steps = 24;
        var profile = new (float, float)[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            var a = MathF.PI * (i / (float)steps - 0.5f);
            // The poles exactly on the axis: float cos(pi/2) is -4e-8, a radius below zero that flips their triangles.
            var r = i == 0 || i == steps ? 0 : radius * MathF.Cos(a);
            profile[i] = (r, radius * MathF.Sin(a));
        }
        return Lathe(profile, 48);
    }

    /// <summary>A ring (a torus): <paramref name="radius"/> to the middle of the tube, <paramref name="tube"/> the tube's radius.</summary>
    public static Mesh3D Torus(float radius, float tube)
    {
        const int steps = 24;
        var profile = new (float, float)[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            var a = i / (float)steps * MathF.Tau;
            profile[i] = (radius + tube * MathF.Cos(a), -tube * MathF.Sin(a));
        }
        return Lathe(profile, 64);
    }

    /// <summary>A box, centered.</summary>
    public static Mesh3D Cuboid(float width, float height, float depth)
    {
        float x = width / 2, y = height / 2, z = depth / 2;
        Vector3 V(float a, float b, float c) => new(a * x, b * y, c * z);
        var mesh = new Mesh3D();
        mesh.Quad(V(-1, -1, 1), V(1, -1, 1), V(1, 1, 1), V(-1, 1, 1), Vector3.UnitZ);
        mesh.Quad(V(1, -1, -1), V(-1, -1, -1), V(-1, 1, -1), V(1, 1, -1), -Vector3.UnitZ);
        mesh.Quad(V(1, -1, 1), V(1, -1, -1), V(1, 1, -1), V(1, 1, 1), Vector3.UnitX);
        mesh.Quad(V(-1, -1, -1), V(-1, -1, 1), V(-1, 1, 1), V(-1, 1, -1), -Vector3.UnitX);
        mesh.Quad(V(-1, 1, 1), V(1, 1, 1), V(1, 1, -1), V(-1, 1, -1), Vector3.UnitY);
        mesh.Quad(V(-1, -1, -1), V(1, -1, -1), V(1, -1, 1), V(-1, -1, 1), -Vector3.UnitY);
        return mesh;
    }

    // ponytail: contours are filled as solids, holes (an O, a ring) are not cut; draw a ring with Torus or Lathe
    // until a shape needs a hole.
    /// <summary>
    /// A 2D path pushed into depth (a star, a heart, a check mark, a logo, a digit), <paramref name="depth"/> thick,
    /// facing the viewer, centered on the path's bounds; path units, y down as in Skia. Each closed contour is its own
    /// solid.
    /// </summary>
    public static Mesh3D Extrude(SKPath path, float depth)
    {
        var bounds = path.Bounds;
        var center = new Vector2(bounds.MidX, bounds.MidY);
        var mesh = new Mesh3D();
        foreach (var raw in Contours(path))
        {
            var contour = raw.Select(p => new Vector2(p.X - center.X, center.Y - p.Y)).ToList();
            if (SignedArea(contour) < 0)
                contour.Reverse();
            var n = contour.Count;
            if (n < 3)
                continue;
            // The outline follows the 2D shape: at each corner, between its two edges' normals.
            Vector2 EdgeNormal(int i)
            {
                var e = contour[(i + 1) % n] - contour[i];
                return NormalizeOrZero(new Vector2(e.Y, -e.X));
            }
            var corner = new Vector3[n];
            for (var i = 0; i < n; i++)
            {
                var v = NormalizeOrZero(EdgeNormal((i + n - 1) % n) + EdgeNormal(i));
                corner[i] = new Vector3(v.X, v.Y, 0);
            }
            float front = depth / 2, back = -depth / 2;
            var triangles = EarClip(contour);
            foreach (var (z, normal, flip) in new[] { (front, Vector3.UnitZ, false), (back, -Vector3.UnitZ, true) })
            {
                var at = (uint)mesh.Positions.Count;
                for (var i = 0; i < n; i++)
                {
                    mesh.Positions.Add(new Vector3(contour[i].X, contour[i].Y, z));
                    mesh.Normals.Add(normal);
                    mesh.Outline.Add(corner[i]);
                }
                foreach (var (a, b, c) in triangles)
                {
                    mesh.Indices.Add(at + a);
                    mesh.Indices.Add(at + (flip ? c : b));
                    mesh.Indices.Add(at + (flip ? b : c));
                }
            }
            for (var i = 0; i < n; i++)
            {
                var a = contour[i];
                var b = contour[(i + 1) % n];
                var edge = b - a;
                if (edge.LengthSquared() < 1e-12f)
                    continue;
                var normal = Vector3.Normalize(new Vector3(edge.Y, -edge.X, 0));
                mesh.Quad(new Vector3(a.X, a.Y, back), new Vector3(b.X, b.Y, back), new Vector3(b.X, b.Y, front), new Vector3(a.X, a.Y, front), normal);
                var ca = corner[i];
                var cb = corner[(i + 1) % n];
                mesh.Outline.Add(ca);
                mesh.Outline.Add(cb);
                mesh.Outline.Add(cb);
                mesh.Outline.Add(ca);
            }
        }
        return mesh;
    }

    /// <summary>The number of triangles.</summary>
    public int TriangleCount => Indices.Count / 3;

    static Vector2 NormalizeOrZero(Vector2 v)
    {
        var l = v.Length();
        return l > 0 && float.IsFinite(l) ? v / l : Vector2.Zero;
    }

    /// <summary>The path's contours as polygons: lines as they are, curves in short straight steps.</summary>
    internal static List<List<Vector2>> Contours(SKPath path)
    {
        var result = new List<List<Vector2>>();
        var current = new List<Vector2>();
        using var it = path.CreateRawIterator();
        var pts = new SKPoint[4];
        SKPathVerb verb;
        static Vector2 V(SKPoint p) => new(p.X, p.Y);
        while ((verb = it.Next(pts)) != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Move:
                    if (current.Count > 2)
                        result.Add(current);
                    current = new List<Vector2> { V(pts[0]) };
                    break;
                case SKPathVerb.Line:
                    current.Add(V(pts[1]));
                    break;
                case SKPathVerb.Quad:
                case SKPathVerb.Conic:
                {
                    var a = current.Count > 0 ? current[^1] : V(pts[0]);
                    Vector2 b = V(pts[1]), c = V(pts[2]);
                    for (var i = 1; i <= 12; i++)
                    {
                        var t = i / 12f;
                        current.Add(a * (1 - t) * (1 - t) + b * 2 * t * (1 - t) + c * t * t);
                    }
                    break;
                }
                case SKPathVerb.Cubic:
                {
                    var a = current.Count > 0 ? current[^1] : V(pts[0]);
                    Vector2 b = V(pts[1]), c = V(pts[2]), d = V(pts[3]);
                    for (var i = 1; i <= 16; i++)
                    {
                        var t = i / 16f;
                        var u = 1 - t;
                        current.Add(a * u * u * u + b * 3 * u * u * t + c * 3 * u * t * t + d * t * t * t);
                    }
                    break;
                }
                case SKPathVerb.Close:
                    if (current.Count > 2)
                        result.Add(current);
                    current = new List<Vector2>();
                    break;
            }
        }
        if (current.Count > 2)
            result.Add(current);
        // A closing point equal to the first one is dropped.
        foreach (var c in result)
        {
            if (c.Count > 3 && Vector2.DistanceSquared(c[0], c[^1]) < 1e-8f)
                c.RemoveAt(c.Count - 1);
        }
        return result;
    }

    internal static float SignedArea(IReadOnlyList<Vector2> polygon)
    {
        var sum = 0f;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return sum / 2;
    }

    static float Cross(Vector2 o, Vector2 a, Vector2 b)
    {
        var u = a - o;
        var v = b - o;
        return u.X * v.Y - u.Y * v.X;
    }

    /// <summary>Triangles of a simple counter-clockwise polygon, by clipping ears.</summary>
    internal static List<(uint A, uint B, uint C)> EarClip(IReadOnlyList<Vector2> polygon)
    {
        // Strictly inside: a point on an edge (two points of the outline in a line) does not block an ear.
        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c) => Cross(a, b, p) > 1e-9f && Cross(b, c, p) > 1e-9f && Cross(c, a, p) > 1e-9f;
        var left = Enumerable.Range(0, polygon.Count).ToList();
        var result = new List<(uint, uint, uint)>();
        var guard = 0;
        while (left.Count > 3 && guard < polygon.Count * polygon.Count)
        {
            guard++;
            var n = left.Count;
            var ear = -1;
            for (var i = 0; i < n && ear < 0; i++)
            {
                int ia = left[(i + n - 1) % n], ib = left[i], ic = left[(i + 1) % n];
                Vector2 a = polygon[ia], b = polygon[ib], c = polygon[ic];
                if (Cross(a, b, c) <= 1e-9f)
                    continue;
                var blocked = false;
                foreach (var j in left)
                {
                    if (j == ia || j == ib || j == ic)
                        continue;
                    if (Inside(polygon[j], a, b, c))
                    {
                        blocked = true;
                        break;
                    }
                }
                if (!blocked)
                    ear = i;
            }
            // None found (a pinched outline): the most convex corner goes anyway, so no hole is left.
            if (ear < 0)
            {
                var best = float.NegativeInfinity;
                for (var i = 0; i < n; i++)
                {
                    var c = Cross(polygon[left[(i + n - 1) % n]], polygon[left[i]], polygon[left[(i + 1) % n]]);
                    if (c >= best)
                    {
                        best = c;
                        ear = i;
                    }
                }
            }
            result.Add(((uint)left[(ear + n - 1) % n], (uint)left[ear], (uint)left[(ear + 1) % n]));
            left.RemoveAt(ear);
        }
        if (left.Count == 3)
            result.Add(((uint)left[0], (uint)left[1], (uint)left[2]));
        return result;
    }
}

/// <summary>
/// A 3D model made in code: named, colored parts, each a <see cref="Mesh3D"/> with its own place (At, Turned, Scaled)
/// and optionally hung under another part (Inside), for <see cref="SkiaModel3D"/> and <see cref="SkiaScene3D.Model(string, Model3D)"/>.
/// A part is a node and a material of its own name, so SetNode* and SetMaterialColor drive it like a part of a model
/// file. The calls after <see cref="Part"/> change the part just added.
/// </summary>
public sealed class Model3D
{
    sealed class PartInfo
    {
        public string Name;
        public Mesh3D Mesh;
        public Color Color;
        public Vector3 Translation = Vector3.Zero;
        public Quaternion Rotation = Quaternion.Identity;
        public Vector3 Scale = Vector3.One;
        public string Parent;
    }

    readonly List<PartInfo> _parts = new();
    Scene3D _scene;

    /// <summary>Adds a part: a mesh in one color. Its name is its node's and its material's.</summary>
    public Model3D Part(string name, Mesh3D mesh, Color color)
    {
        _parts.Add(new PartInfo { Name = name, Mesh = mesh, Color = color });
        _scene = null;
        return this;
    }

    Model3D Last(Action<PartInfo> change)
    {
        if (_parts.Count > 0)
            change(_parts[^1]);
        _scene = null;
        return this;
    }

    /// <summary>Places the last part (in its parent's units).</summary>
    public Model3D At(float x, float y, float z) => Last(p => p.Translation = new Vector3(x, y, z));

    /// <summary>Turns the last part: degrees around x, y, z (in that order).</summary>
    public Model3D Turned(float x, float y, float z) => Last(p => p.Rotation = Math3D.Euler(x, y, z));

    /// <summary>Scales the last part.</summary>
    public Model3D Scaled(float x, float y, float z) => Last(p => p.Scale = new Vector3(x, y, z));

    /// <summary>Hangs the last part under another one by name: it moves, turns and hides with it.</summary>
    public Model3D Inside(string parent) => Last(p => p.Parent = parent);

    /// <summary>The scene of these parts, made once and shared by every control that shows this model.</summary>
    internal Scene3D ToScene()
    {
        if (_scene != null)
            return _scene;
        var nodes = new Node3D[_parts.Count];
        var meshes = new Primitive3D[_parts.Count][];
        var materials = new Material3D[_parts.Count];
        for (var i = 0; i < _parts.Count; i++)
        {
            var p = _parts[i];
            var parent = p.Parent == null ? -1 : _parts.FindIndex(q => q.Name == p.Parent);
            nodes[i] = new Node3D
            {
                Name = p.Name,
                Parent = parent == i ? -1 : parent,
                Rest = new NodeTransform(p.Translation, p.Rotation, p.Scale),
                Mesh = i,
            };
            var n = p.Mesh.Positions.Count;
            meshes[i] = new[]
            {
                new Primitive3D
                {
                    Positions = p.Mesh.Positions.ToArray(),
                    Normals = p.Mesh.Normals.ToArray(),
                    // Only a mesh that gave one for every vertex keeps its own outline directions.
                    OutlineNormals = p.Mesh.Outline.Count == n ? p.Mesh.Outline.ToArray() : null,
                    Uvs = new Vector2[n],
                    Indices = p.Mesh.Indices.ToArray(),
                    Material = i,
                }
            };
            var color = p.Color ?? Colors.White;
            materials[i] = new Material3D
            {
                Name = p.Name,
                Color = Math3D.Linear(color),
                Blend = color.Alpha < 1,
            };
        }
        return _scene = Scene3D.Assemble(nodes, meshes, materials);
    }
}
