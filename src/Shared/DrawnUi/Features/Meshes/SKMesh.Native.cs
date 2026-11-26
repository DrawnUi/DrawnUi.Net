#if SKIASHARP_MESH
#nullable disable
using System.Runtime.InteropServices;
using SkiaSharp;

namespace DrawnUi.Draw.Meshes;

// On a GPU canvas a mesh draws through Skia's own SkMesh (SkiaSharp 4.159+), built from the same inputs; on a CPU
// canvas or while recording a picture it draws through the C# path (SkiaSharp's SkMesh draws nothing there).
// Anything Skia refuses (a program, a mesh) falls back to the C# path too.

public sealed partial class SKMeshSpecification
{
    bool _nativeTried;
    SkiaSharp.SKMeshSpecification _native;
    SkiaSharp.SKMeshBuilder _nativeBuilder;
    SkiaSharp.SKIndexedMeshBuilder _nativeIndexedBuilder;

    // Skia's specification for these programs, made once; null when Skia refuses them. Under EffectLock.
    internal SkiaSharp.SKMeshSpecification Native
    {
        get
        {
            if (!_nativeTried)
            {
                _nativeTried = true;
                try
                {
                    var attrs = new SkiaSharp.SKMeshSpecificationAttribute[Attributes.Length];
                    for (int i = 0; i < attrs.Length; i++)
                        attrs[i] = new SkiaSharp.SKMeshSpecificationAttribute(
                            (SkiaSharp.SKMeshSpecificationAttributeType)(int)Attributes[i].Type, Attributes[i].Offset, Attributes[i].Name);
                    var varys = new SkiaSharp.SKMeshSpecificationVarying[SourceVaryings.Length];
                    for (int i = 0; i < varys.Length; i++)
                        varys[i] = new SkiaSharp.SKMeshSpecificationVarying(
                            (SkiaSharp.SKMeshSpecificationVaryingType)(int)SourceVaryings[i].Type, SourceVaryings[i].Name);
                    _native = SkiaSharp.SKMeshSpecification.Create(attrs, Stride, varys, SourceVertexShader,
                        SourceFragmentShader, SourceColorSpace, SourceAlphaType, out var errors);
                    if (_native == null)
                        Super.Log($"[SKMesh] Skia refused the mesh programs, drawing them in C#: {errors}");
                    else
                        Super.Log("[SKMesh] mesh programs compiled by Skia, GPU canvases draw them with SkMesh");
                }
                catch (Exception e)
                {
                    Super.Log($"[SKMesh] Skia mesh unavailable, drawing in C#: {e.Message}");
                    _native = null;
                }
            }
            return _native;
        }
    }

    // One builder per specification, refilled for every mesh (a mesh made every frame allocates only Skia's mesh).
    internal SkiaSharp.SKMeshBuilder NativeBuilder => _nativeBuilder ??= Native?.CreateMeshBuilder();
    internal SkiaSharp.SKIndexedMeshBuilder NativeIndexedBuilder => _nativeIndexedBuilder ??= Native?.CreateIndexedMeshBuilder();

    void DisposeNative()
    {
        _nativeBuilder?.Dispose();
        _nativeIndexedBuilder?.Dispose();
        _native?.Dispose();
        _nativeBuilder = null;
        _nativeIndexedBuilder = null;
        _native = null;
    }
}

public sealed partial class SKMeshVertexBuffer
{
    SkiaSharp.SKMeshVertexBuffer _native;
    GRRecordingContext _nativeContext;

    // The bytes as Skia's vertex buffer, on the GPU of the canvas that draws (the bytes never change).
    internal SkiaSharp.SKMeshVertexBuffer GetNative(GRRecordingContext context)
    {
        lock (_lock)
        {
            if (_data == null)
                return null;
            if (_native == null || !ReferenceEquals(_nativeContext, context))
            {
                _native?.Dispose();
                _native = context is GRContext gpu
                    ? SkiaSharp.SKMeshVertexBuffer.Create(gpu, Bytes)
                    : SkiaSharp.SKMeshVertexBuffer.Create(Bytes);
                _nativeContext = context;
            }
            return _native;
        }
    }

    void DisposeNative()
    {
        _native?.Dispose();
        _native = null;
        _nativeContext = null;
    }
}

public sealed partial class SKMeshIndexBuffer
{
    SkiaSharp.SKMeshIndexBuffer _native;
    GRRecordingContext _nativeContext;

    internal SkiaSharp.SKMeshIndexBuffer GetNative(GRRecordingContext context)
    {
        lock (_lock)
        {
            if (Data == null)
                return null;
            if (_native == null || !ReferenceEquals(_nativeContext, context))
            {
                _native?.Dispose();
                var bytes = new ReadOnlySpan<byte>(Data, 0, Size);
                _native = context is GRContext gpu
                    ? SkiaSharp.SKMeshIndexBuffer.Create(gpu, bytes)
                    : SkiaSharp.SKMeshIndexBuffer.Create(bytes);
                _nativeContext = context;
            }
            return _native;
        }
    }

    void DisposeNative()
    {
        _native?.Dispose();
        _native = null;
        _nativeContext = null;
    }
}

public sealed partial class SKMesh
{
    bool _nativeFailed;
    SkiaSharp.SKMesh _native;
    GRRecordingContext _nativeContext;

    // Draws through Skia's SkMesh when the canvas is on the GPU. False = the C# path draws.
    bool TryDrawNative(SKCanvas canvas, SKBlender blender, SKPaint paint)
    {
        var context = canvas.Context;
        if (context == null || _nativeFailed)
            return false;

        SkiaSharp.SKMesh mesh;
        lock (_lock)
        {
            if (_native == null || !ReferenceEquals(_nativeContext, context))
            {
                _native?.Dispose();
                _native = BuildNative(context);
                _nativeContext = context;
                if (_native == null)
                {
                    _nativeFailed = true;
                    return false;
                }
            }
            mesh = _native;
        }

        if (blender != null)
            canvas.DrawMesh(mesh, blender, paint);
        else
            canvas.DrawMesh(mesh, paint);
        return true;
    }

    SkiaSharp.SKMesh BuildNative(GRRecordingContext context)
    {
        lock (_spec.EffectLock)
        {
            if (_spec.Native == null)
                return null;

            var vertices = _vertexBuffer.GetNative(context);
            if (vertices == null)
                return null;

            SkiaSharp.SKMeshBuilder builder;
            if (_indexBuffer != null)
            {
                var indices = _indexBuffer.GetNative(context);
                if (indices == null)
                    return null;
                var indexed = _spec.NativeIndexedBuilder;
                indexed.SetIndexBuffer(indices, _indexCount, _indexOffset);
                builder = indexed;
            }
            else
            {
                builder = _spec.NativeBuilder;
            }

            builder.Mode = (SkiaSharp.SKMeshMode)(int)_mode;
            builder.Bounds = Bounds;
            builder.SetVertexBuffer(vertices, _vertexCount, _vertexOffset);

            // every uniform is written for every mesh: the builder keeps the previous mesh's values otherwise
            var uniforms = builder.Uniforms;
            foreach (var slot in _spec.Uniforms)
            {
                var bytes = new ReadOnlySpan<byte>(_uniforms, slot.Offset, slot.Size);
                if (slot.IsInt)
                {
                    var ints = MemoryMarshal.Cast<byte, int>(bytes);
                    if (ints.Length == 1 && !slot.IsArray)
                        uniforms[slot.Name] = ints[0];
                    else
                        uniforms[slot.Name] = (ReadOnlySpan<int>)ints;
                }
                else
                {
                    var floats = MemoryMarshal.Cast<byte, float>(bytes);
                    if (floats.Length == 1 && !slot.IsArray)
                        uniforms[slot.Name] = floats[0];
                    else
                        uniforms[slot.Name] = (ReadOnlySpan<float>)floats;
                }
            }

            var children = builder.Children;
            for (int i = 0; i < _spec.Children.Length; i++)
                children[_spec.Children[i].Name] = _children[i].Value == null ? (SKRuntimeEffectChild?)null : _children[i];

            var mesh = builder.Build(out var errors);
            if (mesh == null || !mesh.IsValid)
            {
                Super.Log($"[SKMesh] Skia refused the mesh, drawing it in C#: {errors}");
                mesh?.Dispose();
                return null;
            }
            return mesh;
        }
    }

    void DisposeNative()
    {
        _native?.Dispose();
        _native = null;
        _nativeContext = null;
    }
}
#endif
