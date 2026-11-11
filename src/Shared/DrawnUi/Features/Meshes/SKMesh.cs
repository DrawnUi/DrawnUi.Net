#nullable disable
using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Threading;
using SkiaSharp;

namespace DrawnUi.Draw.Meshes;

/// <summary>Raw vertex bytes of a mesh, copied at creation (stand-in for SkiaSharp's SKMeshVertexBuffer).</summary>
public sealed class SKMeshVertexBuffer : IDisposable
{
    // Pooled: a mesh rebuilt every frame does not put its vertex copy on the large object heap. Returned to the
    // pool when the buffer and every mesh made from it are disposed (never returned if one is not: the GC takes it).
    byte[] _data;
    readonly int _size;
    int _refs = 1;
    bool _disposed;
    readonly object _lock = new();
    MeshGeometry _geometry; // last geometry built from these bytes, shared by the meshes that use the same range

    SKMeshVertexBuffer(int size)
    {
        _size = size;
        _data = size == 0 ? Array.Empty<byte>() : ArrayPool<byte>.Shared.Rent(size);
    }

    internal ReadOnlySpan<byte> Bytes => _data.AsSpan(0, _size);

    /// <summary>Size in bytes.</summary>
    public int Size => _size;

    /// <summary>Copies <paramref name="data"/> into a new vertex buffer.</summary>
    public static SKMeshVertexBuffer Make(ReadOnlySpan<byte> data)
    {
        var vb = new SKMeshVertexBuffer(data.Length);
        data.CopyTo(vb._data);
        return vb;
    }

    /// <summary>Copies <paramref name="size"/> bytes from <paramref name="data"/> into a new vertex buffer.</summary>
    public static SKMeshVertexBuffer Make(IntPtr data, int size)
    {
        if (size < 0 || (data == IntPtr.Zero && size > 0))
            return null;
        var vb = new SKMeshVertexBuffer(size);
        if (size > 0)
            Marshal.Copy(data, vb._data, 0, size);
        return vb;
    }

    /// <summary>Creates a zero-filled vertex buffer of <paramref name="size"/> bytes.</summary>
    public static SKMeshVertexBuffer Make(int size)
    {
        if (size < 0)
            return null;
        var vb = new SKMeshVertexBuffer(size);
        vb._data.AsSpan(0, size).Clear();
        return vb;
    }

    internal void AddRef()
    {
        if (_disposed && Volatile.Read(ref _refs) <= 0)
            throw new ObjectDisposedException(nameof(SKMeshVertexBuffer));
        Interlocked.Increment(ref _refs);
    }

    internal void Release()
    {
        if (Interlocked.Decrement(ref _refs) != 0)
            return;
        lock (_lock)
        {
            _geometry?.Release();
            _geometry = null;
            if (_data.Length > 0)
                ArrayPool<byte>.Shared.Return(_data);
            _data = null;
        }
    }

    // The buffer is immutable, so what depends only on its bytes (attribute image, texCoords, and positions
    // when the vertex program copies them) is built once and reused by every mesh drawing the same range:
    // a static mesh redrawn with new uniforms each frame uploads nothing new.
    internal MeshGeometry AcquireGeometry(in MeshGeometry.Key key, Func<MeshGeometry> build)
    {
        lock (_lock)
        {
            if (_geometry == null || !_geometry.Id.Equals(key))
            {
                var built = build();
                if (built == null)
                    return null;
                _geometry?.Release();
                _geometry = built; // the buffer's own reference
            }
            _geometry.AddRef();
            return _geometry;
        }
    }

    /// <summary>Releases this reference to the bytes; meshes made from the buffer keep theirs.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Release();
    }
}

/// <summary>Raw 16-bit index bytes of a mesh, copied at creation (stand-in for SkiaSharp's SKMeshIndexBuffer).</summary>
public sealed class SKMeshIndexBuffer : IDisposable
{
    internal readonly byte[] Data;

    SKMeshIndexBuffer(byte[] data) => Data = data;

    /// <summary>Size in bytes.</summary>
    public int Size => Data.Length;

    /// <summary>Copies <paramref name="data"/> (ushort indices as bytes) into a new index buffer.</summary>
    public static SKMeshIndexBuffer Make(ReadOnlySpan<byte> data) => new(data.ToArray());

    /// <summary>Copies <paramref name="size"/> bytes from <paramref name="data"/> into a new index buffer.</summary>
    public static SKMeshIndexBuffer Make(IntPtr data, int size)
    {
        if (size < 0 || (data == IntPtr.Zero && size > 0))
            return null;
        var bytes = new byte[size];
        if (size > 0)
            Marshal.Copy(data, bytes, 0, size);
        return new SKMeshIndexBuffer(bytes);
    }

    /// <summary>Creates a zero-filled index buffer of <paramref name="size"/> bytes.</summary>
    public static SKMeshIndexBuffer Make(int size) => size < 0 ? null : new SKMeshIndexBuffer(new byte[size]);

    /// <summary>Nothing native to release, kept for parity with SkiaSharp.</summary>
    public void Dispose() { }
}

/// <summary>
/// A custom mesh: specification, vertices, optional indices, uniforms and children, drawn with
/// <see cref="SKMeshCanvasExtensions.DrawMesh(SKCanvas, SKMesh, SKPaint)"/> (stand-in for SkiaSharp's SKMesh).
/// Drawn through <c>SKCanvas.DrawVertices</c> and one runtime shader, so it also draws on raster (CPU) canvases.
/// </summary>
public sealed class SKMesh : IDisposable
{
    static readonly SKBlender Modulate = SKBlender.CreateBlendMode(SKBlendMode.Modulate);

    readonly SKMeshSpecification _spec;
    readonly SKMeshMode _mode;
    readonly SKMeshVertexBuffer _vertexBuffer;
    readonly int _vertexCount;
    readonly int _vertexOffset;
    readonly SKMeshIndexBuffer _indexBuffer;
    readonly int _indexCount;
    readonly int _indexOffset;
    readonly byte[] _uniforms;
    readonly SKRuntimeEffectChild[] _children;
    readonly object _lock = new();

    // Built at the first draw.
    bool _built;
    bool _disposed;
    MeshGeometry _geometry;
    SKVertices[] _ownVertices; // positions from the vertex program run with this mesh's uniforms
    SKVertices[] _vertices;

    // Last draw shader, reused while the paint shader, color and blender stay the same.
    SKShader _shader;
    SKShader _shaderPaintShader;
    SKColor _shaderPaintColor;
    SKBlender _shaderBlender;
    SKShader _colorShader;
    SKColor _colorShaderColor;

    SKMesh(SKMeshSpecification spec, SKMeshMode mode, SKMeshVertexBuffer vertexBuffer, int vertexCount, int vertexOffset,
        SKMeshIndexBuffer indexBuffer, int indexCount, int indexOffset, byte[] uniforms, SKRuntimeEffectChild[] children,
        SKRect bounds)
    {
        _spec = spec;
        _mode = mode;
        _vertexBuffer = vertexBuffer;
        _vertexCount = vertexCount;
        _vertexOffset = vertexOffset;
        _indexBuffer = indexBuffer;
        _indexCount = indexCount;
        _indexOffset = indexOffset;
        _uniforms = uniforms;
        _children = children;
        Bounds = bounds;
        vertexBuffer.AddRef();
    }

    /// <summary>True for every mesh <c>Make</c> / <c>MakeIndexed</c> returned.</summary>
    public bool IsValid => _spec != null;

    // Bounds given at creation, used to skip draws outside the clip (SkiaSharp does not expose them).
    internal SKRect Bounds { get; }

    /// <summary>Creates a mesh without uniforms or children. Returns null and the reason in <paramref name="errors"/> on failure.</summary>
    public static SKMesh Make(SKMeshSpecification specification, SKMeshMode mode, SKMeshVertexBuffer vertexBuffer,
        int vertexCount, int vertexOffset, SKRect bounds, out string errors)
        => Make(specification, mode, vertexBuffer, vertexCount, vertexOffset, null, default, bounds, out errors);

    /// <summary>Creates a mesh with uniform data. Returns null and the reason in <paramref name="errors"/> on failure.</summary>
    public static SKMesh Make(SKMeshSpecification specification, SKMeshMode mode, SKMeshVertexBuffer vertexBuffer,
        int vertexCount, int vertexOffset, SKData uniforms, SKRect bounds, out string errors)
        => Make(specification, mode, vertexBuffer, vertexCount, vertexOffset, uniforms, default, bounds, out errors);

    /// <summary>
    /// Creates a mesh with uniform data and child effects (fragment program order). <paramref name="vertexOffset"/>
    /// is in bytes. Returns null and the reason in <paramref name="errors"/> on failure.
    /// </summary>
    public static SKMesh Make(SKMeshSpecification specification, SKMeshMode mode, SKMeshVertexBuffer vertexBuffer,
        int vertexCount, int vertexOffset, SKData uniforms, ReadOnlySpan<SKRuntimeEffectChild> children, SKRect bounds,
        out string errors)
    {
        if (specification == null)
            throw new ArgumentNullException(nameof(specification));
        if (vertexBuffer == null)
            throw new ArgumentNullException(nameof(vertexBuffer));

        errors = ValidateVertices(specification, mode, vertexBuffer, vertexCount, vertexOffset, false)
                 ?? ValidateUniformsAndChildren(specification, uniforms, children);
        if (errors != null)
            return null;

        return new SKMesh(specification, mode, vertexBuffer, vertexCount, vertexOffset, null, 0, 0,
            CopyUniforms(uniforms), children.ToArray(), bounds);
    }

    /// <summary>Creates an indexed mesh without uniforms or children. Returns null and the reason in <paramref name="errors"/> on failure.</summary>
    public static SKMesh MakeIndexed(SKMeshSpecification specification, SKMeshMode mode, SKMeshVertexBuffer vertexBuffer,
        int vertexCount, int vertexOffset, SKMeshIndexBuffer indexBuffer, int indexCount, int indexOffset, SKRect bounds,
        out string errors)
        => MakeIndexed(specification, mode, vertexBuffer, vertexCount, vertexOffset, indexBuffer, indexCount, indexOffset,
            null, default, bounds, out errors);

    /// <summary>Creates an indexed mesh with uniform data. Returns null and the reason in <paramref name="errors"/> on failure.</summary>
    public static SKMesh MakeIndexed(SKMeshSpecification specification, SKMeshMode mode, SKMeshVertexBuffer vertexBuffer,
        int vertexCount, int vertexOffset, SKMeshIndexBuffer indexBuffer, int indexCount, int indexOffset, SKData uniforms,
        SKRect bounds, out string errors)
        => MakeIndexed(specification, mode, vertexBuffer, vertexCount, vertexOffset, indexBuffer, indexCount, indexOffset,
            uniforms, default, bounds, out errors);

    /// <summary>
    /// Creates an indexed mesh with uniform data and child effects. Offsets are in bytes; indices are ushort and
    /// relative to <paramref name="vertexOffset"/>. Returns null and the reason in <paramref name="errors"/> on failure.
    /// </summary>
    public static SKMesh MakeIndexed(SKMeshSpecification specification, SKMeshMode mode, SKMeshVertexBuffer vertexBuffer,
        int vertexCount, int vertexOffset, SKMeshIndexBuffer indexBuffer, int indexCount, int indexOffset, SKData uniforms,
        ReadOnlySpan<SKRuntimeEffectChild> children, SKRect bounds, out string errors)
    {
        if (specification == null)
            throw new ArgumentNullException(nameof(specification));
        if (vertexBuffer == null)
            throw new ArgumentNullException(nameof(vertexBuffer));
        if (indexBuffer == null)
            throw new ArgumentNullException(nameof(indexBuffer));

        errors = ValidateVertices(specification, mode, vertexBuffer, vertexCount, vertexOffset, true);
        if (errors == null)
        {
            if (indexCount < 3)
                errors = $"{ModeName(mode)} mode requires at least 3 indices but index count is {indexCount}.";
            else if (indexOffset < 0 || indexOffset % 2 != 0)
                errors = "The index offset must be a multiple of 2.";
            else if ((long)indexOffset + (long)indexCount * 2 > indexBuffer.Size)
                errors = "The index buffer offset and index count reads beyond the end of the index buffer.";
        }
        errors ??= ValidateUniformsAndChildren(specification, uniforms, children);
        if (errors != null)
            return null;

        return new SKMesh(specification, mode, vertexBuffer, vertexCount, vertexOffset, indexBuffer, indexCount, indexOffset,
            CopyUniforms(uniforms), children.ToArray(), bounds);
    }

    static string ModeName(SKMeshMode mode) => mode == SKMeshMode.Triangles ? "triangles" : "triangle-strip";

    static string ValidateVertices(SKMeshSpecification spec, SKMeshMode mode, SKMeshVertexBuffer vb, int count, int offset, bool indexed)
    {
        if ((!indexed && count < 3) || count < 0)
            return $"{ModeName(mode)} mode requires at least 3 vertices but vertex count is {count}.";
        if (offset < 0 || offset % spec.Stride != 0)
            return $"The vertex offset ({offset}) must be a multiple of the vertex stride ({spec.Stride}).";
        if ((long)offset + (long)count * spec.Stride > vb.Size)
            return "The vertex buffer offset and vertex count reads beyond the end of the vertex buffer.";
        return null;
    }

    static string ValidateUniformsAndChildren(SKMeshSpecification spec, SKData uniforms, ReadOnlySpan<SKRuntimeEffectChild> children)
    {
        long size = uniforms?.Size ?? 0;
        if (size < spec.UniformSize)
            return $"The uniform data is {size} bytes but must be at least {spec.UniformSize}.";
        if (children.Length != spec.Children.Length)
            return $"The mesh specification declares {spec.Children.Length} child effects, but the mesh supplies {children.Length}.";
        for (int i = 0; i < children.Length; i++)
        {
            var child = children[i];
            if (child.Value == null)
                continue;
            var passed = child.Shader != null ? "shader" : child.ColorFilter != null ? "colorFilter" : "blender";
            if (passed != spec.Children[i].Kind)
                return $"Child effect '{spec.Children[i].Name}' was specified as a {KindName(spec.Children[i].Kind)}, but passed as a {KindName(passed)}.";
        }
        return null;
    }

    static string KindName(string kind) => kind == "colorFilter" ? "color filter" : kind;

    static byte[] CopyUniforms(SKData uniforms) => uniforms == null ? Array.Empty<byte>() : uniforms.ToArray();

    internal void Draw(SKCanvas canvas, SKBlender blender, SKPaint paint)
    {
        lock (_lock)
        {
            if (!_built)
            {
                _built = true;
                Build();
            }
            if (_vertices == null)
                return;
            var shader = GetShader(blender, paint);
            if (shader == null)
                return;

            using var drawPaint = paint.Clone();
            drawPaint.Shader = shader;
            drawPaint.Color = SKColors.White; // paint color and alpha are applied inside the shader, as SkMesh does
            foreach (var vertices in _vertices)
                canvas.DrawVertices(vertices, SKBlendMode.Modulate, drawPaint);
        }
    }

    void Build()
    {
        var key = new MeshGeometry.Key(_spec, _mode, _vertexCount, _vertexOffset, _indexBuffer, _indexCount, _indexOffset);
        _geometry = _vertexBuffer.AcquireGeometry(key,
            () => MeshGeometry.Build(key, _spec, _mode, _vertexBuffer.Bytes, _vertexCount, _vertexOffset, ReadIndices()));
        if (_geometry == null)
            return;

        if (_geometry.Vertices != null)
        {
            _vertices = _geometry.Vertices;
            return;
        }

        // The vertex program moves the vertices: run it on the CPU with this mesh's uniforms.
        var positions = _geometry.RunVertexProgram(_spec, _uniforms);
        if (positions == null)
            return;
        var corners = _geometry.Corners;
        var texs = _geometry.TexCoords;
        _ownVertices = new SKVertices[texs.Length];
        int corner = 0;
        for (int c = 0; c < texs.Length; c++)
        {
            var points = new SKPoint[texs[c].Length];
            for (int i = 0; i < points.Length; i++)
                points[i] = positions[corners[corner++]];
            _ownVertices[c] = SKVertices.CreateCopy(SKVertexMode.Triangles, points, texs[c], null);
        }
        _vertices = _ownVertices;
    }

    ushort[] ReadIndices() => _indexBuffer == null
        ? null
        : MemoryMarshal.Cast<byte, ushort>(_indexBuffer.Data.AsSpan(_indexOffset, _indexCount * 2)).ToArray();

    SKShader GetShader(SKBlender blender, SKPaint paint)
    {
        var paintShader = paint.Shader;
        var paintColor = paint.Color;
        if (!_spec.HasColor)
            blender = null;
        else
            blender ??= Modulate;

        if (_shader != null && ReferenceEquals(_shaderPaintShader, paintShader) && _shaderPaintColor == paintColor
            && ReferenceEquals(_shaderBlender, blender))
            return _shader;

        SKShader source = paintShader;
        if (source == null)
        {
            if (_colorShader == null || _colorShaderColor != paintColor)
            {
                _colorShader?.Dispose();
                _colorShader = SKShader.CreateColor(paintColor);
                _colorShaderColor = paintColor;
            }
            source = _colorShader;
        }

        SKShader shader;
        lock (_spec.EffectLock)
        {
            var uniforms = _spec.DrawUniforms;
            MeshGeometry.SetUniforms(uniforms, _spec.Uniforms, _uniforms);
            float alpha = paintShader == null ? 1f : paintColor.Alpha / 255f;
            uniforms["_sm_grid"] = (ReadOnlySpan<float>)stackalloc float[] { _geometry.HiRows, alpha, 0, 0 };

            var children = _spec.DrawChildren;
            for (int i = 0; i < _spec.Children.Length; i++)
                children[_spec.Children[i].Name] = _children[i].Value == null ? (SKRuntimeEffectChild?)null : _children[i];
            children["_sm_attr"] = _geometry.AttributeShader;
            children["_sm_paint"] = source;
            if (blender != null)
                children["_sm_blend"] = blender;
            shader = _spec.DrawEffect.ToShader(uniforms, children);
            children.Reset(); // holds no mesh's objects between draws
        }

        _shader?.Dispose();
        _shader = shader;
        _shaderPaintShader = paintShader;
        _shaderPaintColor = paintColor;
        _shaderBlender = blender;
        return _shader;
    }

    /// <summary>Releases the drawing resources of this mesh.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _shader?.Dispose();
            _shader = null;
            _colorShader?.Dispose();
            _colorShader = null;
            if (_ownVertices != null)
                foreach (var v in _ownVertices)
                    v.Dispose();
            _ownVertices = null;
            _vertices = null;
            _geometry?.Release();
            _geometry = null;
            if (!_disposed)
            {
                _disposed = true;
                _vertexBuffer.Release();
            }
        }
    }
}

// What a mesh draws with that depends only on its vertex bytes and range: the triangles as a triangle list,
// the attribute image the draw program reads, the DrawVertices texCoords, and the positions when the
// vertex program copies the "position" attribute. Reference counted: the buffer and every mesh using it.
sealed class MeshGeometry
{
    static readonly SKSamplingOptions Nearest = new(SKFilterMode.Nearest, SKMipmapMode.None);

    internal readonly record struct Key(SKMeshSpecification Spec, SKMeshMode Mode, int VertexCount, int VertexOffset,
        SKMeshIndexBuffer IndexBuffer, int IndexCount, int IndexOffset);

    // Triangles per SKVertices: its SKPoint arrays stay under the large object heap threshold (85 KB).
    const int ChunkTriangles = 3000;

    public Key Id { get; private init; }
    public int[] Corners { get; private set; }          // 3 vertex indices per triangle (pooled, longer than needed)
    public SKPoint[][] TexCoords { get; private set; }  // per chunk; triangle i: cell (i % w, i / w) + barycentric corners
    public SKImage AttributeImage { get; private set; }
    public SKShader AttributeShader { get; private set; }
    public float HiRows { get; private set; }
    public SKVertices[] Vertices { get; private set; }  // per chunk, set when positions are the "position" attribute

    // Vertex program input, F32 (exact on the CPU), for meshes whose vertex program moves the vertices.
    SKImage _positionInput;
    SKShader _positionInputShader;
    int _perRow, _rows, _available;

    int _refs = 1;

    public void AddRef() => Interlocked.Increment(ref _refs);

    public void Release()
    {
        if (Interlocked.Decrement(ref _refs) != 0)
            return;
        if (Vertices != null)
            foreach (var v in Vertices)
                v.Dispose();
        if (Corners != null)
            ArrayPool<int>.Shared.Return(Corners);
        Corners = null;
        AttributeShader?.Dispose();
        AttributeImage?.Dispose();
        _positionInputShader?.Dispose();
        _positionInput?.Dispose();
        _outputSurface?.Dispose();
        _outputPaint?.Dispose();
    }

    public static MeshGeometry Build(Key key, SKMeshSpecification spec, SKMeshMode mode, ReadOnlySpan<byte> vertexData, int vertexCount,
        int vertexOffset, ushort[] indices)
    {
        int texels = spec.TexelsPerVertex;
        int perVertex = texels * 4;

        // Vertices the mesh can reach: the counted range, or for indices, everything the buffer holds past the offset.
        int available = indices == null ? vertexCount : (vertexData.Length - vertexOffset) / spec.Stride;
        if (available <= 0)
            return null;

        int sourceCount = indices?.Length ?? vertexCount;
        int triangles = mode == SKMeshMode.Triangles ? sourceCount / 3 : Math.Max(0, sourceCount - 2);
        var packed = ArrayPool<float>.Shared.Rent(available * perVertex);
        var corners = ArrayPool<int>.Shared.Rent(Math.Max(1, triangles * 3));
        ushort[] pixels = null;
        MeshGeometry g = null;
        try
        {
            Unpack(spec, vertexData, vertexOffset, packed, available);

            int n = 0;
            for (int t = 0; t < triangles; t++)
            {
                int a = mode == SKMeshMode.Triangles ? t * 3 : t;
                int i0 = indices == null ? a : indices[a];
                int i1 = indices == null ? a + 1 : indices[a + 1];
                int i2 = indices == null ? a + 2 : indices[a + 2];
                if ((uint)i0 >= (uint)available || (uint)i1 >= (uint)available || (uint)i2 >= (uint)available)
                    continue; // index outside the vertex buffer: skipped (undefined in Skia)
                corners[n * 3] = i0;
                corners[n * 3 + 1] = i1;
                corners[n * 3 + 2] = i2;
                n++;
            }
            if (n == 0)
                return null;

            // Attribute image: triangle i in cell (i % w, i / w), its 3 vertices side by side, `texels` texels each.
            // Rows [0, rows) hold the high halves, rows [rows, 2 rows) the low halves.
            int w = Math.Max(1, Math.Min(n, SKMeshSpecification.MaxTextureSide / (3 * texels)));
            int rows = (n + w - 1) / w;
            int width = w * 3 * texels;
            int pixelCount = width * rows * 2 * 4;
            pixels = ArrayPool<ushort>.Shared.Rent(pixelCount);
            int loBase = width * rows * 4;

            g = new MeshGeometry { Id = key, Corners = corners, _available = available };
            corners = null; // owned by the geometry now
            int chunks = (n + ChunkTriangles - 1) / ChunkTriangles;
            var texs = new SKPoint[chunks][];
            for (int c = 0; c < chunks; c++)
                texs[c] = new SKPoint[Math.Min(ChunkTriangles, n - c * ChunkTriangles) * 3];
            for (int t = 0; t < n; t++)
            {
                int cx = t % w, cy = t / w;
                for (int k = 0; k < 3; k++)
                {
                    int dst = (cy * width + (cx * 3 + k) * texels) * 4;
                    var src = packed.AsSpan(g.Corners[t * 3 + k] * perVertex, perVertex);
                    SplitHalves(src, pixels.AsSpan(dst, perVertex), pixels.AsSpan(loBase + dst, perVertex));
                }
                var chunk = texs[t / ChunkTriangles];
                int o = t % ChunkTriangles * 3;
                chunk[o] = new SKPoint(cx + SKMeshSpecification.CornerLow, cy + SKMeshSpecification.CornerLow);
                chunk[o + 1] = new SKPoint(cx + SKMeshSpecification.CornerHigh, cy + SKMeshSpecification.CornerLow);
                chunk[o + 2] = new SKPoint(cx + SKMeshSpecification.CornerLow, cy + SKMeshSpecification.CornerHigh);
            }
            g.TexCoords = texs;

            var info = new SKImageInfo(width, rows * 2, SKColorType.RgbaF16, SKAlphaType.Unpremul);
            g.AttributeImage = SKImage.FromPixelCopy(info, MemoryMarshal.AsBytes(pixels.AsSpan(0, pixelCount)));
            if (g.AttributeImage == null)
            {
                g.Release();
                return null;
            }
            g.AttributeShader = g.AttributeImage.ToRawShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, Nearest);
            g.HiRows = rows;

            if (spec.PositionEffect == null)
            {
                // The vertex program copies the "position" attribute.
                int p = spec.PositionAttributeFloat;
                g.Vertices = new SKVertices[chunks];
                int corner = 0;
                for (int c = 0; c < chunks; c++)
                {
                    var points = new SKPoint[texs[c].Length];
                    for (int i = 0; i < points.Length; i++)
                    {
                        int v = g.Corners[corner++] * perVertex + p;
                        points[i] = new SKPoint(packed[v], packed[v + 1]);
                    }
                    g.Vertices[c] = SKVertices.CreateCopy(SKVertexMode.Triangles, points, texs[c], null);
                }
            }
            else
            {
                // Vertex v sits at (v % perRow, v / perRow): the packed order.
                g._perRow = Math.Max(1, Math.Min(available, SKMeshSpecification.MaxTextureSide / texels));
                g._rows = (available + g._perRow - 1) / g._perRow;
                var input = new SKImageInfo(g._perRow * texels, g._rows, SKColorType.RgbaF32, SKAlphaType.Unpremul);
                int floats = input.Width * input.Height * 4;
                var image = ArrayPool<float>.Shared.Rent(floats);
                try
                {
                    packed.AsSpan(0, available * perVertex).CopyTo(image);
                    image.AsSpan(available * perVertex, floats - available * perVertex).Clear();
                    g._positionInput = SKImage.FromPixelCopy(input, MemoryMarshal.AsBytes(image.AsSpan(0, floats)));
                }
                finally
                {
                    ArrayPool<float>.Shared.Return(image);
                }
                g._positionInputShader = g._positionInput?.ToRawShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, Nearest);
                if (g._positionInputShader == null)
                {
                    g.Release();
                    return null;
                }
            }
            return g;
        }
        finally
        {
            ArrayPool<float>.Shared.Return(packed);
            if (corners != null)
                ArrayPool<int>.Shared.Return(corners);
            if (pixels != null)
                ArrayPool<ushort>.Shared.Return(pixels);
        }
    }

    // DrawVertices needs positions on the CPU: the vertex program runs once per vertex on a raster F32 surface,
    // with the mesh's uniforms, and the positions are read back.
    // The surface drawing into _output, kept with the geometry: every mesh made from the same vertex bytes
    // (a strip redrawn with new uniforms each frame) runs its vertex program into it.
    SKSurface _outputSurface;
    SKPaint _outputPaint;
    float[] _output; // pinned

    public SKPoint[] RunVertexProgram(SKMeshSpecification spec, byte[] uniformData)
    {
        lock (this)
        {
            if (_outputSurface == null)
            {
                var outInfo = new SKImageInfo(_perRow, _rows, SKColorType.RgbaF32, SKAlphaType.Premul);
                _output = GC.AllocateArray<float>(_perRow * _rows * 4, pinned: true);
                _outputSurface = SKSurface.Create(outInfo, Marshal.UnsafeAddrOfPinnedArrayElement(_output, 0), outInfo.RowBytes);
                if (_outputSurface == null)
                    return null;
                _outputPaint = new SKPaint { BlendMode = SKBlendMode.Src };
            }

            SKShader shader;
            lock (spec.EffectLock)
            {
                var uniforms = spec.PositionUniforms;
                SetUniforms(uniforms, spec.VertexUniforms, uniformData);
                var children = spec.PositionChildren;
                children["_sm_attr"] = _positionInputShader;
                shader = spec.PositionEffect.ToShader(uniforms, children);
                children.Reset();
            }
            using (shader)
            {
                _outputPaint.Shader = shader;
                _outputSurface.Canvas.DrawRect(0, 0, _perRow, _rows, _outputPaint); // raster: written at once
                _outputPaint.Shader = null;
            }

            var positions = new SKPoint[_available];
            for (int v = 0; v < positions.Length; v++)
                positions[v] = new SKPoint(_output[v * 4], _output[v * 4 + 1]);
            return positions;
        }
    }

    public static void SetUniforms(SKRuntimeEffectUniforms target, SKMeshSpecification.UniformSlot[] slots, byte[] data)
    {
        foreach (var slot in slots)
        {
            var bytes = new ReadOnlySpan<byte>(data, slot.Offset, slot.Size);
            if (slot.IsInt)
            {
                var ints = MemoryMarshal.Cast<byte, int>(bytes);
                if (ints.Length == 1 && !slot.IsArray)
                    target[slot.Name] = ints[0];
                else
                    target[slot.Name] = ints;
            }
            else
            {
                var floats = MemoryMarshal.Cast<byte, float>(bytes);
                if (floats.Length == 1 && !slot.IsArray)
                    target[slot.Name] = floats[0];
                else
                    target[slot.Name] = floats;
            }
        }
    }

    // Attribute floats of every reachable vertex, packed, `texels * 4` floats per vertex.
    // Stride and offsets are multiples of 4, so the buffer reads as floats.
    static void Unpack(SKMeshSpecification spec, ReadOnlySpan<byte> vertexData, int vertexOffset, float[] packed, int count)
    {
        int perVertex = spec.TexelsPerVertex * 4;
        int strideFloats = spec.Stride / 4;
        int baseFloat = vertexOffset / 4;
        var floats = MemoryMarshal.Cast<byte, float>(vertexData.Slice(0, vertexData.Length & ~3));
        int dst = 0;
        foreach (var a in spec.Attributes)
        {
            int at = baseFloat + a.Offset / 4;
            if (a.Type == SKMeshSpecificationAttributeType.Ubyte4Unorm)
            {
                for (int v = 0; v < count; v++)
                {
                    int src = (at + v * strideFloats) * 4;
                    int d = v * perVertex + dst;
                    packed[d] = vertexData[src] / 255f;
                    packed[d + 1] = vertexData[src + 1] / 255f;
                    packed[d + 2] = vertexData[src + 2] / 255f;
                    packed[d + 3] = vertexData[src + 3] / 255f;
                }
                dst += 4;
            }
            else
            {
                int n = SKMeshSpecification.AttributeFloats(a.Type);
                for (int v = 0; v < count; v++)
                {
                    int src = at + v * strideFloats;
                    int d = v * perVertex + dst;
                    for (int c = 0; c < n; c++)
                        packed[d + c] = floats[src + c];
                }
                dst += n;
            }
        }
        if (dst < perVertex) // padding lanes of the last texel (pooled array: clear them)
            for (int v = 0; v < count; v++)
                packed.AsSpan(v * perVertex + dst, perVertex - dst).Clear();
    }

    // Each float as two halves, hi = half(x) and lo = half(x - hi): both exact in fp16 storage and fp16 math,
    // ~22 bits of precision once added back in float. Same results as (Half) casts, much faster.
    static void SplitHalves(ReadOnlySpan<float> src, Span<ushort> hi, Span<ushort> lo)
    {
        int i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            ref float s = ref MemoryMarshal.GetReference(src);
            ref ushort h = ref MemoryMarshal.GetReference(hi);
            ref ushort l = ref MemoryMarshal.GetReference(lo);
            for (; i <= src.Length - 8; i += 8)
            {
                var x0 = Vector128.LoadUnsafe(ref s, (nuint)i);
                var x1 = Vector128.LoadUnsafe(ref s, (nuint)i + 4);
                var h0 = ToHalfBits(x0.AsUInt32());
                var h1 = ToHalfBits(x1.AsUInt32());
                Vector128.Narrow(h0, h1).StoreUnsafe(ref h, (nuint)i);
                var l0 = ToHalfBits((x0 - FromHalfBits(h0)).AsUInt32());
                var l1 = ToHalfBits((x1 - FromHalfBits(h1)).AsUInt32());
                Vector128.Narrow(l0, l1).StoreUnsafe(ref l, (nuint)i);
            }
        }
        for (; i < src.Length; i++)
        {
            float x = src[i];
            ushort h = FloatToHalf(x);
            hi[i] = h;
            lo[i] = FloatToHalf(x - (float)BitConverter.UInt16BitsToHalf(h));
        }
    }

    // float -> IEEE half bits, round to nearest even, same result as (Half)f.
    internal static ushort FloatToHalf(float f)
    {
        uint x = BitConverter.SingleToUInt32Bits(f);
        uint sign = (x >> 16) & 0x8000;
        uint a = x & 0x7FFFFFFF;
        if (a >= 0x47800000) // overflow, infinity, NaN
            return (ushort)(sign | (a > 0x7F800000 ? 0x7E00u : 0x7C00u));
        if (a < 0x38800000) // half subnormal: let a float add do the rounding
            return (ushort)(sign | (BitConverter.SingleToUInt32Bits(BitConverter.UInt32BitsToSingle(a) + 0.5f) - 0x3F000000u));
        a += 0xC8000FFFu + ((a >> 13) & 1); // exponent rebias + round to nearest even
        return (ushort)(sign | (a >> 13));
    }

    static Vector128<uint> ToHalfBits(Vector128<uint> x)
    {
        var sign = (x >> 16) & Vector128.Create(0x8000u);
        var a = x & Vector128.Create(0x7FFFFFFFu);
        var normal = (a + Vector128.Create(0xC8000FFFu) + ((a >> 13) & Vector128<uint>.One)) >> 13;
        var sub = (a.AsSingle() + Vector128.Create(0.5f)).AsUInt32() - Vector128.Create(0x3F000000u);
        var over = Vector128.ConditionalSelect(Vector128.GreaterThan(a, Vector128.Create(0x7F800000u)), Vector128.Create(0x7E00u), Vector128.Create(0x7C00u));
        var r = Vector128.ConditionalSelect(Vector128.LessThan(a, Vector128.Create(0x38800000u)), sub, normal);
        r = Vector128.ConditionalSelect(Vector128.GreaterThanOrEqual(a, Vector128.Create(0x47800000u)), over, r);
        return r | sign;
    }

    static Vector128<float> FromHalfBits(Vector128<uint> h)
    {
        var sign = (h & Vector128.Create(0x8000u)) << 16;
        var e = h & Vector128.Create(0x7C00u);
        var m = (h & Vector128.Create(0x7FFFu)) << 13;
        var normal = m + Vector128.Create(0x38000000u);
        var infNan = m + Vector128.Create(0x70000000u);
        var sub = (Vector128.ConvertToSingle((h & Vector128.Create(0x3FFu)).AsInt32()) * Vector128.Create(5.9604645E-08f)).AsUInt32();
        var r = Vector128.ConditionalSelect(Vector128.Equals(e, Vector128.Create(0x7C00u)), infNan, normal);
        r = Vector128.ConditionalSelect(Vector128.Equals(e, Vector128<uint>.Zero), sub, r);
        return (r | sign).AsSingle();
    }
}

/// <summary><c>SKCanvas.DrawMesh</c> for the stand-in <see cref="SKMesh"/>, same overloads as SkiaSharp's.</summary>
public static class SKMeshCanvasExtensions
{
    /// <summary>Draws <paramref name="mesh"/>; a fragment color is multiplied with the paint's shader or color (Modulate).</summary>
    public static void DrawMesh(this SKCanvas canvas, SKMesh mesh, SKPaint paint)
        => DrawMesh(canvas, mesh, null, paint);

    /// <summary>
    /// Draws <paramref name="mesh"/>. When the fragment program outputs a color, <paramref name="blender"/> combines the
    /// paint's shader or color (source) with it (destination); null means Modulate.
    /// </summary>
    public static void DrawMesh(this SKCanvas canvas, SKMesh mesh, SKBlender blender, SKPaint paint)
    {
        if (canvas == null)
            throw new ArgumentNullException(nameof(canvas));
        if (mesh == null)
            throw new ArgumentNullException(nameof(mesh));
        if (paint == null)
            throw new ArgumentNullException(nameof(paint));
        if (!mesh.IsValid)
            return;
        // Bounds cull like SkCanvas::drawMesh (filters can draw outside them, so no cull then).
        if (paint.ImageFilter == null && paint.MaskFilter == null && canvas.QuickReject(mesh.Bounds))
            return;
        mesh.Draw(canvas, blender, paint);
    }
}
