#nullable disable
using System.Numerics;

namespace DrawnUi.Add3d;

/// <summary>
/// A bounding-box tree over a rigid primitive's triangles, in the primitive's own units: a ray tests the few triangles
/// whose boxes it crosses instead of every one. Made the first time a ray meets the primitive, kept with it.
/// </summary>
internal sealed class RayTree
{
    /// <summary>A box: a leaf holds <see cref="Count"/> triangles from <see cref="First"/>, else its children are the next node and <see cref="Second"/>.</summary>
    struct Node
    {
        public Vector3 Min, Max;
        public int First, Count, Second;
    }

    const int LeafSize = 6;

    readonly Node[] _nodes;
    int _used;
    /// <summary>Triangle numbers in tree order.</summary>
    readonly int[] _tris;

    RayTree(int triangles)
    {
        _tris = new int[triangles];
        _nodes = new Node[Math.Max(1, 2 * triangles)];
    }

    /// <summary>The tree of a primitive's triangles (positions and indices as stored, three per triangle).</summary>
    public static RayTree Build(Vector3[] positions, uint[] indices)
    {
        var count = indices.Length / 3;
        var tree = new RayTree(count);
        var centers = new Vector3[count];
        var mins = new Vector3[count];
        var maxs = new Vector3[count];
        for (var t = 0; t < count; t++)
        {
            tree._tris[t] = t;
            var (a, b, c) = Corners(positions, indices, t);
            mins[t] = Vector3.Min(a, Vector3.Min(b, c));
            maxs[t] = Vector3.Max(a, Vector3.Max(b, c));
            centers[t] = (mins[t] + maxs[t]) * 0.5f;
        }
        if (count > 0)
            tree.Split(0, count, centers, mins, maxs, new float[count]);
        return tree;
    }

    static (Vector3, Vector3, Vector3) Corners(Vector3[] positions, uint[] indices, int t)
    {
        var n = (uint)positions.Length;
        uint a = indices[t * 3], b = indices[t * 3 + 1], c = indices[t * 3 + 2];
        // A broken index makes a point triangle at the origin (the ray test skips it as degenerate).
        return (a < n ? positions[a] : default, b < n ? positions[b] : default, c < n ? positions[c] : default);
    }

    /// <summary>Makes the node of triangles [first, first + count): splits at the median along the box's longest side.</summary>
    int Split(int first, int count, Vector3[] centers, Vector3[] mins, Vector3[] maxs, float[] keys)
    {
        var index = _used++;
        Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
        for (var i = first; i < first + count; i++)
        {
            lo = Vector3.Min(lo, mins[_tris[i]]);
            hi = Vector3.Max(hi, maxs[_tris[i]]);
        }
        _nodes[index].Min = lo;
        _nodes[index].Max = hi;
        if (count <= LeafSize)
        {
            _nodes[index].First = first;
            _nodes[index].Count = count;
            return index;
        }
        var size = hi - lo;
        var axis = size.X >= size.Y && size.X >= size.Z ? 0 : size.Y >= size.Z ? 1 : 2;
        // Sorted by plain float keys: no comparer call per comparison (a Debug web build interprets every call).
        for (var i = first; i < first + count; i++)
            keys[i] = Axis(centers[_tris[i]], axis);
        Array.Sort(keys, _tris, first, count);
        var half = count / 2;
        Split(first, half, centers, mins, maxs, keys);
        _nodes[index].Second = Split(first + half, count - half, centers, mins, maxs, keys);
        return index;
    }

    static float Axis(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;

    /// <summary>
    /// The nearest triangle the ray meets closer than <paramref name="distance"/> (in units of <paramref name="dir"/>),
    /// which it then holds; with <paramref name="anyHit"/> the first one found. False when none.
    /// </summary>
    public bool Hit(Vector3[] positions, uint[] indices, Vector3 from, Vector3 dir, ref float distance, bool anyHit)
    {
        if (_used == 0)
            return false;
        // A zero direction component would turn the slab test into 0 * infinity.
        static float Inv(float v) => 1 / (MathF.Abs(v) < 1e-12f ? 1e-12f : v);
        var inverse = new Vector3(Inv(dir.X), Inv(dir.Y), Inv(dir.Z));
        // Median splits of at most a few million triangles stay far below 64 levels.
        Span<int> stack = stackalloc int[64];
        var top = 0;
        stack[top++] = 0;
        var found = false;
        while (top > 0)
        {
            var at = stack[--top];
            ref readonly var node = ref _nodes[at];
            if (!Crosses(node.Min, node.Max, from, inverse, distance))
                continue;
            if (node.Count > 0)
            {
                for (var i = node.First; i < node.First + node.Count; i++)
                {
                    var (a, b, c) = Corners(positions, indices, _tris[i]);
                    if (Scene3D.RayTriangle(from, dir, a, b, c, out var t) && t < distance)
                    {
                        distance = t;
                        found = true;
                        if (anyHit)
                            return true;
                    }
                }
                continue;
            }
            // The first child follows its parent.
            stack[top++] = node.Second;
            stack[top++] = at + 1;
        }
        return found;
    }

    /// <summary>Slab test: whether the ray crosses the box before <paramref name="limit"/>.</summary>
    static bool Crosses(in Vector3 min, in Vector3 max, in Vector3 from, in Vector3 inverse, float limit)
    {
        // Plain floats: a Debug web build interprets every vector operator as a call.
        float a = (min.X - from.X) * inverse.X, b = (max.X - from.X) * inverse.X;
        float enter = a < b ? a : b, leave = a < b ? b : a;
        a = (min.Y - from.Y) * inverse.Y;
        b = (max.Y - from.Y) * inverse.Y;
        if (a > b)
            (a, b) = (b, a);
        if (a > enter)
            enter = a;
        if (b < leave)
            leave = b;
        a = (min.Z - from.Z) * inverse.Z;
        b = (max.Z - from.Z) * inverse.Z;
        if (a > b)
            (a, b) = (b, a);
        if (a > enter)
            enter = a;
        if (b < leave)
            leave = b;
        if (enter < 0)
            enter = 0;
        if (limit < leave)
            leave = limit;
        return enter <= leave;
    }
}
