#nullable disable
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using SkiaSharp;

namespace DrawnUi.Draw.Meshes;

/// <summary>Raw vertex bytes of a mesh, copied at creation (stand-in for SkiaSharp's SKMeshVertexBuffer).</summary>
public sealed class SKMeshVertexBuffer : IDisposable
{
    // Pooled: a mesh rebuilt every frame does not put its vertex copy on the large object heap. Returned to the
    // pool when the buffer and every mesh made from it are disposed, and Skia let go of the image wrapping the
    // bytes (never returned if one is not disposed: the GC takes it).
    byte[] _data;
    readonly int _size;
    int _refs = 1;
    bool _disposed;
    readonly object _lock = new();
    MeshGeometry _geometry; // last geometry built from these bytes, shared by the meshes that use the same range
    MeshData _image;       // the bytes as the programs read them

    SKMeshVertexBuffer(int size)
    {
        _size = size;
        _data = ArrayPool<byte>.Shared.Rent(MeshData.PaddedBytes(size));
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
            if (_image != null)
                _image.Dispose(); // the bytes go back to the pool when Skia releases the image
            else
                ArrayPool<byte>.Shared.Return(_data);
            _image = null;
            _data = null;
        }
    }

    // The bytes wrapped as they are (no copy) in an image the programs read.
    internal MeshData GetImage()
    {
        lock (_lock)
        {
            if (_image == null && _data != null)
            {
                var data = _data;
                _image = MeshData.Wrap(data, _size, () => ArrayPool<byte>.Shared.Return(data));
            }
            return _image;
        }
    }

    // The buffer is immutable, so what depends only on its bytes (image, texCoords, and positions when the
    // vertex program copies them) is built once and reused by every mesh drawing the same range: a static
    // mesh redrawn with new uniforms each frame uploads nothing new.
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
    internal readonly byte[] Data; // padded to the image size the programs read
    readonly int _size;
    readonly object _lock = new();
    MeshData _image;

    SKMeshIndexBuffer(int size)
    {
        _size = size;
        Data = new byte[MeshData.PaddedBytes(size)];
    }

    /// <summary>Size in bytes.</summary>
    public int Size => _size;

    /// <summary>Copies <paramref name="data"/> (ushort indices as bytes) into a new index buffer.</summary>
    public static SKMeshIndexBuffer Make(ReadOnlySpan<byte> data)
    {
        var ib = new SKMeshIndexBuffer(data.Length);
        data.CopyTo(ib.Data);
        return ib;
    }

    /// <summary>Copies <paramref name="size"/> bytes from <paramref name="data"/> into a new index buffer.</summary>
    public static SKMeshIndexBuffer Make(IntPtr data, int size)
    {
        if (size < 0 || (data == IntPtr.Zero && size > 0))
            return null;
        var ib = new SKMeshIndexBuffer(size);
        if (size > 0)
            Marshal.Copy(data, ib.Data, 0, size);
        return ib;
    }

    /// <summary>Creates a zero-filled index buffer of <paramref name="size"/> bytes.</summary>
    public static SKMeshIndexBuffer Make(int size) => size < 0 ? null : new SKMeshIndexBuffer(size);

    // The bytes wrapped as they are in an image the draw program reads; it keeps the array alive.
    internal MeshData GetImage()
    {
        lock (_lock)
        {
            if (_image == null)
            {
                var data = Data;
                _image = MeshData.Wrap(data, _size, () => GC.KeepAlive(data));
            }
            return _image;
        }
    }

    /// <summary>Kept for parity with SkiaSharp; the index image is released with the buffer by the GC, since
    /// meshes made from the buffer may still draw.</summary>
    public void Dispose() { }
}

// Mesh bytes as an RGBA8 image, one texel per 4 bytes, rows of a power-of-two width, wrapped without a copy.
sealed class MeshData : IDisposable
{
    static readonly SKSamplingOptions Nearest = new(SKFilterMode.Nearest, SKMipmapMode.None);

    public SKImage Image { get; private init; }
    public SKShader Shader { get; private init; }
    public int Width { get; private init; }

    static int WidthFor(int bytes)
    {
        int texels = Math.Max(1, (bytes + 3) / 4);
        int w = 1;
        while (w < texels && w < SKMeshSpecification.MaxTextureSide)
            w <<= 1;
        return w;
    }

    // Bytes an array needs so the image's last row is complete.
    public static int PaddedBytes(int bytes)
    {
        int w = WidthFor(bytes);
        int rows = (Math.Max(1, (bytes + 3) / 4) + w - 1) / w;
        return w * rows * 4;
    }

    // Null when the bytes need more rows than a texture may have (64 MB). The array must stay untouched until
    // `released` runs, which Skia does when it no longer needs the pixels.
    public static MeshData Wrap(byte[] data, int size, Action released)
    {
        int w = WidthFor(size);
        int rows = (Math.Max(1, (size + 3) / 4) + w - 1) / w;
        if (rows > SKMeshSpecification.MaxTextureSide)
            return null;
        var info = new SKImageInfo(w, rows, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var pin = GCHandle.Alloc(data, GCHandleType.Pinned);
        SKImage image;
        using (var pixmap = new SKPixmap(info, pin.AddrOfPinnedObject(), info.RowBytes))
            image = SKImage.FromPixels(pixmap, (_, _) => { pin.Free(); released(); });
        if (image == null)
        {
            pin.Free();
            return null;
        }
        return new MeshData { Image = image, Shader = image.ToRawShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, Nearest), Width = w };
    }

    public void Dispose()
    {
        Shader?.Dispose();
        Image?.Dispose();
    }
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
    SKShader _shaderPacked;
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
                Build(canvas.Context);
            }
            if (_vertices == null)
                return;
            var shader = GetShader(blender, paint, canvas.Context);
            if (shader == null)
                return;

            using var drawPaint = paint.Clone();
            drawPaint.Shader = shader;
            drawPaint.Color = SKColors.White; // paint color and alpha are applied inside the shader, as SkMesh does
            foreach (var vertices in _vertices)
                canvas.DrawVertices(vertices, SKBlendMode.Modulate, drawPaint);
        }
    }

    void Build(GRRecordingContext context)
    {
        var key = new MeshGeometry.Key(_spec, _mode, _vertexCount, _vertexOffset, _indexBuffer, _indexCount, _indexOffset);
        _geometry = _vertexBuffer.AcquireGeometry(key, () => MeshGeometry.Build(key, _vertexBuffer));
        if (_geometry == null)
            return;
        // Positions copied from the "position" attribute, else the vertex program runs on the CPU with this
        // mesh's uniforms.
        _vertices = _geometry.Vertices ?? (_ownVertices = _geometry.RunVertexProgram(_uniforms, context));
    }

    SKShader GetShader(SKBlender blender, SKPaint paint, GRRecordingContext context)
    {
        var paintShader = paint.Shader;
        var paintColor = paint.Color;
        if (!_spec.HasColor)
            blender = null;
        else
            blender ??= Modulate;

        var packed = _geometry.GetPacked(context);
        if (packed == null)
            return null;
        if (_shader != null && ReferenceEquals(_shaderPaintShader, paintShader) && _shaderPaintColor == paintColor
            && ReferenceEquals(_shaderBlender, blender) && ReferenceEquals(_shaderPacked, packed))
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

        var g = _geometry;
        SKShader shader;
        lock (_spec.EffectLock)
        {
            var uniforms = _spec.DrawUniforms;
            MeshGeometry.SetUniforms(uniforms, _spec.Uniforms, _uniforms);
            float alpha = paintShader == null ? 1f : paintColor.Alpha / 255f;
            uniforms["_sm_grid"] = (ReadOnlySpan<float>)stackalloc float[]
                { g.CellsPerRow, alpha, _mode == SKMeshMode.TriangleStrip ? 1 : 0, g.IndexImage != null ? 1 : 0 };
            uniforms["_sm_pk"] = (ReadOnlySpan<float>)stackalloc float[] { 0, g.VerticesPerRow, 1f / g.VerticesPerRow, g.PackedRows };
            var idx = g.IndexImage ?? g.VertexImage; // any shader when not indexed: never read
            uniforms["_sm_ix"] = (ReadOnlySpan<float>)stackalloc float[] { g.FirstIndex, idx.Width, 1f / idx.Width, 0 };

            var children = _spec.DrawChildren;
            for (int i = 0; i < _spec.Children.Length; i++)
                children[_spec.Children[i].Name] = _children[i].Value == null ? (SKRuntimeEffectChild?)null : _children[i];
            children["_sm_attr"] = packed;
            children["_sm_idx"] = idx.Shader;
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
        _shaderPacked = packed;
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

// What a mesh draws with that depends only on its vertex bytes and range: the attributes packed per vertex
// as the draw program reads them, the DrawVertices texCoords, and the positions when the vertex program
// copies the "position" attribute. Reference counted: the buffer and every mesh using it.
//
// No per-float work on the CPU: the app's bytes go to the GPU as they are and the convert program packs
// them there, once per vertex range. The CPU only gathers 2 floats per corner for DrawVertices. Triangle i
// covers texCoord cell (i % w, i / w); the draw program finds the vertex of each of its corners.
sealed class MeshGeometry
{
    internal readonly record struct Key(SKMeshSpecification Spec, SKMeshMode Mode, int VertexCount, int VertexOffset,
        SKMeshIndexBuffer IndexBuffer, int IndexCount, int IndexOffset);

    // Triangles per SKVertices: its SKPoint arrays stay under the large object heap threshold (85 KB).
    const int ChunkTriangles = 3000;

    public Key Id { get; private init; }
    public int CellsPerRow { get; private init; }
    public MeshData VertexImage { get; private init; }  // owned by the vertex buffer
    public MeshData IndexImage { get; private init; }   // owned by the index buffer, null when not indexed
    public float FirstFloat { get; private init; }      // first float of the mesh's vertices in the vertex image
    public float FirstIndex { get; private init; }
    public int VerticesPerRow { get; private init; }    // in the packed images, a power of two
    public int PackedRows { get; private init; }        // rows of vertices (the fp16 image has them twice: hi, lo)
    public SKVertices[] Vertices { get; private set; }  // per chunk, set when positions are the "position" attribute

    int _available;
    SKPoint[][] _texCoords;

    // Packed fp16 hi/lo image for the draw program, made on the context it is drawn with (null: raster).
    SKImage _packed;
    SKShader _packedShader;
    IntPtr _packedContext;
    // Packed F32 image (raster) for the vertex program run on the CPU.
    SKImage _packedF32;
    SKShader _packedF32Shader;

    // Vertex program output (one RGBA F32 pixel per vertex), kept with the geometry: every mesh made from the
    // same vertex bytes (a strip redrawn with new uniforms each frame) runs its vertex program into it.
    int _perRow, _rows;
    SKSurface _outputSurface;
    float[] _output; // pinned

    int _refs = 1;

    public void AddRef() => Interlocked.Increment(ref _refs);

    public void Release()
    {
        if (Interlocked.Decrement(ref _refs) != 0)
            return;
        if (Vertices != null)
            foreach (var v in Vertices)
                v.Dispose();
        _outputSurface?.Dispose();
        _packedShader?.Dispose();
        _packed?.Dispose();
        _packedF32Shader?.Dispose();
        _packedF32?.Dispose();
    }

    public static MeshGeometry Build(Key key, SKMeshVertexBuffer buffer)
    {
        var spec = key.Spec;
        bool indexed = key.IndexBuffer != null;
        int count = indexed ? key.IndexCount : key.VertexCount;
        int triangles = key.Mode == SKMeshMode.Triangles ? count / 3 : Math.Max(0, count - 2);
        // Vertices the mesh can reach: the counted range, or for indices, everything the buffer holds past the offset.
        int available = indexed ? (buffer.Size - key.VertexOffset) / spec.Stride : key.VertexCount;
        if (triangles == 0 || available <= 0)
            return null;

        var vertexImage = buffer.GetImage();
        var indexImage = indexed ? key.IndexBuffer.GetImage() : null;
        if (vertexImage == null || (indexed && indexImage == null))
            return null;

        int texels = spec.TexelsPerVertex;
        int perRow = 1;
        while (perRow * 2 * texels <= SKMeshSpecification.MaxTextureSide)
            perRow *= 2;
        int rows = (available + perRow - 1) / perRow;
        if (rows * 2 > SKMeshSpecification.MaxTextureSide)
            return null;

        int w = (int)Math.Ceiling(Math.Sqrt(triangles)); // keeps texCoords small: their fraction is the barycentric
        var g = new MeshGeometry
        {
            Id = key,
            CellsPerRow = w,
            VertexImage = vertexImage,
            IndexImage = indexImage,
            FirstFloat = key.VertexOffset / 4,
            FirstIndex = key.IndexOffset / 2,
            VerticesPerRow = perRow,
            PackedRows = rows,
            _available = available,
            _texCoords = TexCoords(triangles, w),
        };

        if (spec.PositionEffect == null)
        {
            // The vertex program copies the "position" attribute: positions straight from the bytes.
            var floats = MemoryMarshal.Cast<byte, float>(buffer.Bytes.Slice(0, buffer.Size & ~3));
            g.Vertices = g.MakeVertices(floats, key.VertexOffset / 4 + spec.PositionAttributeFloat, spec.Stride / 4);
        }
        else
        {
            g._perRow = Math.Min(available, SKMeshSpecification.MaxTextureSide);
            g._rows = (available + g._perRow - 1) / g._perRow;
            if (g._rows > SKMeshSpecification.MaxTextureSide)
                return null;
        }
        return g;
    }

    // The draw program's input on `context` (null: a raster or recording canvas): made by the convert program,
    // on the GPU when there is one, once per context.
    public SKShader GetPacked(GRRecordingContext context)
    {
        var handle = context?.Handle ?? IntPtr.Zero;
        lock (this)
        {
            if (_packedShader != null && _packedContext == handle)
                return _packedShader;
            var image = Convert(context, false);
            if (image == null)
                return null;
            _packedShader?.Dispose();
            _packed?.Dispose();
            _packed = image;
            _packedShader = image.ToRawShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, Nearest);
            _packedContext = handle;
            return _packedShader;
        }
    }

    static readonly SKSamplingOptions Nearest = new(SKFilterMode.Nearest, SKMipmapMode.None);

    // Runs the convert program into a new image: fp16 hi + lo rows, or one F32 block (`plain`, raster only).
    SKImage Convert(GRRecordingContext context, bool plain)
    {
        var spec = Id.Spec;
        int width = VerticesPerRow * spec.TexelsPerVertex;
        int height = plain ? PackedRows : PackedRows * 2;
        // Premul: an Unpremul GPU surface is refused. The program writes with Src, so values are stored as they are.
        var info = new SKImageInfo(width, height, plain ? SKColorType.RgbaF32 : SKColorType.RgbaF16, SKAlphaType.Premul);
        using var surface = (context != null ? SKSurface.Create(context, true, info) : null) ?? SKSurface.Create(info);
        if (surface == null)
            return null;
        SKShader shader;
        lock (spec.EffectLock)
        {
            var uniforms = spec.ConvertUniforms;
            uniforms["_sm_vtx"] = (ReadOnlySpan<float>)stackalloc float[] { FirstFloat, VertexImage.Width, 1f / VertexImage.Width, 0 };
            uniforms["_sm_cv"] = (ReadOnlySpan<float>)stackalloc float[] { VerticesPerRow, PackedRows, plain ? 1 : 0, 0 };
            var children = spec.ConvertChildren;
            children["_sm_attr"] = VertexImage.Shader;
            shader = spec.ConvertEffect.ToShader(uniforms, children);
            children.Reset();
        }
        using (shader)
        using (var paint = new SKPaint { Shader = shader, BlendMode = SKBlendMode.Src })
            surface.Canvas.DrawRect(0, 0, width, height, paint);
        return surface.Snapshot();
    }

    // DrawVertices triangles of the whole mesh, corner positions read from `src` (vertex v's x at
    // first + v * stride). A triangle with an index outside the buffer is drawn empty (undefined in Skia).
    SKVertices[] MakeVertices(ReadOnlySpan<float> src, int first, int stride)
    {
        var key = Id;
        bool strip = key.Mode == SKMeshMode.TriangleStrip;
        ReadOnlySpan<ushort> indices = key.IndexBuffer == null
            ? default
            : MemoryMarshal.Cast<byte, ushort>(key.IndexBuffer.Data.AsSpan(key.IndexOffset, key.IndexCount * 2));
        bool indexed = key.IndexBuffer != null;
        uint available = (uint)_available;

        var result = new SKVertices[_texCoords.Length];
        int t = 0;
        for (int c = 0; c < _texCoords.Length; c++)
        {
            var texs = _texCoords[c];
            var points = Scratch(texs.Length);
            for (int j = 0; j < texs.Length; j += 3, t++)
            {
                int a = strip ? t : t * 3;
                int v0 = indexed ? indices[a] : a;
                int v1 = indexed ? indices[a + 1] : a + 1;
                int v2 = indexed ? indices[a + 2] : a + 2;
                if ((uint)v0 >= available || (uint)v1 >= available || (uint)v2 >= available)
                {
                    points[j] = points[j + 1] = points[j + 2] = default;
                    continue;
                }
                int f0 = first + v0 * stride, f1 = first + v1 * stride, f2 = first + v2 * stride;
                points[j] = new SKPoint(src[f0], src[f0 + 1]);
                points[j + 1] = new SKPoint(src[f1], src[f1 + 1]);
                points[j + 2] = new SKPoint(src[f2], src[f2 + 1]);
            }
            result[c] = SKVertices.CreateCopy(SKVertexMode.Triangles, points, texs, null);
        }
        return result;
    }

    // DrawVertices copies the points: one array per thread and length is reused for every mesh.
    [ThreadStatic] static SKPoint[] t_full, t_tail;

    static SKPoint[] Scratch(int length)
    {
        if (length == ChunkTriangles * 3)
            return t_full ??= new SKPoint[length];
        if (t_tail == null || t_tail.Length != length)
            t_tail = new SKPoint[length];
        return t_tail;
    }

    // TexCoords depend only on the triangle count: shared by every mesh of that size (a mesh rebuilt each
    // frame reuses them). Triangle i: cell (i % w, i / w) with corners at +0.125 / +0.875, so the interpolated
    // texCoord gives the triangle (floor) and its barycentrics (fraction).
    static readonly Dictionary<int, SKPoint[][]> s_texCoords = new();

    static SKPoint[][] TexCoords(int triangles, int w)
    {
        lock (s_texCoords)
        {
            if (s_texCoords.TryGetValue(triangles, out var cached))
                return cached;
            int chunks = (triangles + ChunkTriangles - 1) / ChunkTriangles;
            var texs = new SKPoint[chunks][];
            for (int c = 0; c < chunks; c++)
            {
                var chunk = texs[c] = new SKPoint[Math.Min(ChunkTriangles, triangles - c * ChunkTriangles) * 3];
                for (int j = 0, t = c * ChunkTriangles; j < chunk.Length; j += 3, t++)
                {
                    float cx = t % w, cy = t / w;
                    chunk[j] = new SKPoint(cx + SKMeshSpecification.CornerLow, cy + SKMeshSpecification.CornerLow);
                    chunk[j + 1] = new SKPoint(cx + SKMeshSpecification.CornerHigh, cy + SKMeshSpecification.CornerLow);
                    chunk[j + 2] = new SKPoint(cx + SKMeshSpecification.CornerLow, cy + SKMeshSpecification.CornerHigh);
                }
            }
            if (s_texCoords.Count >= 16) // ponytail: drops all sizes at once; an LRU if apps cycle more sizes
                s_texCoords.Clear();
            s_texCoords[triangles] = texs;
            return texs;
        }
    }

    // DrawVertices needs positions on the CPU: the vertex program runs once per vertex, with the mesh's uniforms,
    // and the positions are read back. On the canvas's GPU when it has one (input: the packed fp16 image the
    // draw program uses; output: each position float as its 4 IEEE bytes in an RGBA8 target, since Skia renders
    // no F32 on the GPU), else on a raster F32 surface (input: packed F32). Both give the floats bit-exact.
    public SKVertices[] RunVertexProgram(byte[] uniformData, GRRecordingContext context)
    {
        lock (this)
        {
            // The GPU path waits for the GPU once per draw (the read-back), about half a millisecond: small meshes
            // (a strip of a few dozen vertices, often drawn many times a frame) run on the CPU instead.
            // ponytail: fixed vertex threshold; measure per device if meshes of ~1000 vertices matter.
            if (context != null && _available >= GpuVertexProgramMinVertices)
            {
                // 2 pixels per vertex: x then y.
                int perRow = Math.Min(_available, SKMeshSpecification.MaxTextureSide / 2);
                int rows = (_available + perRow - 1) / perRow;
                var packed = GetPacked(context);
                if (packed != null && rows <= SKMeshSpecification.MaxTextureSide)
                {
                    var info = new SKImageInfo(perRow * 2, rows, SKColorType.Rgba8888, SKAlphaType.Premul); // read back as written
                    using var surface = SKSurface.Create(context, true, info);
                    if (surface != null)
                    {
                        if (_bytes == null || _bytes.Length < info.BytesSize)
                            _bytes = GC.AllocateArray<byte>(info.BytesSize, pinned: true);
                        if (RunVertexProgram(surface, uniformData, packed, PackedRows, perRow, rows * 1f, true)
                            && surface.ReadPixels(info, Marshal.UnsafeAddrOfPinnedArrayElement(_bytes, 0), info.RowBytes, 0, 0))
                            return MakeVertices(MemoryMarshal.Cast<byte, float>(_bytes.AsSpan(0, info.BytesSize)), 0, 2);
                    }
                }
            }

            var rasterInfo = new SKImageInfo(_perRow, _rows, SKColorType.RgbaF32, SKAlphaType.Premul);
            if (_outputSurface == null)
            {
                _output = GC.AllocateArray<float>(_perRow * _rows * 4, pinned: true);
                _outputSurface = SKSurface.Create(rasterInfo, Marshal.UnsafeAddrOfPinnedArrayElement(_output, 0), rasterInfo.RowBytes);
                if (_outputSurface == null)
                    return null;
            }
            if (_packedF32Shader == null)
            {
                _packedF32 = Convert(null, true);
                if (_packedF32 == null)
                    return null;
                _packedF32Shader = _packedF32.ToRawShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, Nearest);
            }
            if (!RunVertexProgram(_outputSurface, uniformData, _packedF32Shader, 0, _perRow, _rows, false)) // raster: written at once
                return null;
            return MakeVertices(_output, 0, 4);
        }
    }

    byte[] _bytes; // pinned, GPU read-back
    const int GpuVertexProgramMinVertices = 1024;

    // `loRows`: rows of the high half block of an fp16 hi/lo input, 0 for F32. `bytes`: the RGBA8 output.
    bool RunVertexProgram(SKSurface surface, byte[] uniformData, SKShader input, int loRows, int perRow, float rows, bool bytes)
    {
        var spec = Id.Spec;
        SKShader shader;
        lock (spec.EffectLock)
        {
            var uniforms = spec.PositionUniforms;
            SetUniforms(uniforms, spec.VertexUniforms, uniformData);
            uniforms["_sm_pk"] = (ReadOnlySpan<float>)stackalloc float[] { 0, VerticesPerRow, 1f / VerticesPerRow, loRows };
            uniforms["_sm_pos"] = (ReadOnlySpan<float>)stackalloc float[] { perRow, bytes ? 1 : 0, 0, 0 };
            var children = spec.PositionChildren;
            children["_sm_attr"] = input;
            shader = spec.PositionEffect.ToShader(uniforms, children);
            children.Reset();
        }
        if (shader == null)
            return false;
        using (shader)
        using (var paint = new SKPaint { Shader = shader, BlendMode = SKBlendMode.Src })
            surface.Canvas.DrawRect(0, 0, bytes ? perRow * 2 : perRow, rows, paint);
        return true;
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
