#nullable disable
using System.Numerics;
using System.Runtime.CompilerServices;
using DrawnUi.Draw;
using DrawnUi.Views;
using Foundation;
using Metal;
using SkiaSharp;

namespace DrawnUi.Add3d;

/// <summary>
/// The 3D renderer on a canvas drawing with Metal (DrawnUi.Maui on iOS and Mac Catalyst): the GL renderer's shaders
/// written in Metal Shading Language, drawn on the device and the command queue every DrawnUI Metal view shares. Each
/// control renders into its own color texture with a Depth32Float buffer in one command buffer, committed on the queue
/// while the canvas draws, so it runs before the work Skia commits for that frame; Metal tracks the texture between
/// the two. Skia then samples the texture.
/// </summary>
internal static unsafe class MetalRenderer3D
{
    const int MaxJoints = 64;

    // SharedAdd3d/Renderer3D.cs Vs and Fs in MSL: same math, depth mapped to 0..1, the image upright (top-left first).
    const string Source = """
#include <metal_stdlib>
using namespace metal;

struct DrawBlock {
    float4x4 model;
    float4x4 view_proj;
    float4 color;
    float4 light;
    float4 outline_color;
    float4 params; // x outline width (pixels), yz viewport
    int4 flags;    // x skinned, y textured, z look, w outline pass
};

struct VertexIn {
    float3 pos [[attribute(0)]];
    float3 nrm [[attribute(1)]];
    float2 uv [[attribute(2)]];
    float4 joints [[attribute(3)]];
    float4 weights [[attribute(4)]];
    float3 outline_nrm [[attribute(5)]];
};

struct VertexOut {
    float4 position [[position]];
    float3 nrm;
    float2 uv;
};

vertex VertexOut model_vertex(VertexIn v [[stage_in]], constant DrawBlock& d [[buffer(6)]], constant float4x4* joints [[buffer(7)]]) {
    float4x4 skin = float4x4(1.0);
    if (d.flags.x != 0) {
        skin = v.weights.x * joints[int(v.joints.x)] + v.weights.y * joints[int(v.joints.y)]
             + v.weights.z * joints[int(v.joints.z)] + v.weights.w * joints[int(v.joints.w)];
    }
    float4x4 m = d.model * skin;
    float3x3 m3 = float3x3(m[0].xyz, m[1].xyz, m[2].xyz);
    VertexOut o;
    o.nrm = m3 * v.nrm;
    o.uv = v.uv;
    o.position = d.view_proj * m * float4(v.pos, 1.0);
    if (d.params.x > 0.0) {
        // The outline pass: the back faces pushed out along their smoothed normals, a fixed number of pixels on screen.
        float4 n = d.view_proj * float4(m3 * v.outline_nrm, 0.0);
        float2 dir = normalize(n.xy + float2(1e-6));
        o.position.xy += dir * d.params.x * 2.0 / d.params.yz * o.position.w;
        o.position.z += 0.002 * o.position.w;
    }
    // GL clip depth -1..1 to Metal's 0..1.
    o.position.z = (o.position.z + o.position.w) * 0.5;
    return o;
}

fragment float4 model_fragment(VertexOut i [[stage_in]], bool front [[front_facing]], constant DrawBlock& d [[buffer(0)]],
                               texture2d<float> tex [[texture(0)]], sampler smp [[sampler(0)]]) {
    if (d.flags.w != 0) return float4(d.outline_color.rgb * d.outline_color.a, d.outline_color.a);
    float4 c = d.color;
    // glTF colors are linear; textures are sRGB: light in linear, show in sRGB.
    if (d.flags.y != 0) { float4 t = tex.sample(smp, i.uv); c *= float4(pow(t.rgb, float3(2.2)), t.a); }
    if (c.a < 0.02) discard_fragment();
    float3 n = normalize(i.nrm);
    if (!front) n = -n;
    float sun = max(dot(n, d.light.xyz), 0.0);
    float sky = 0.5 + 0.5 * n.y;
    float3 lit;
    if (d.flags.z == 2) lit = c.rgb;
    else if (d.flags.z == 1) lit = c.rgb * (sun > 0.35 ? 1.0 : 0.62);
    else lit = c.rgb * (0.30 + 0.25 * sky + 0.70 * sun);
    return float4(pow(lit, float3(1.0 / 2.2)) * c.a, c.a);
}
""";

    /// <summary>The uniforms of one draw, laid out as the MSL DrawBlock.</summary>
    struct DrawBlock
    {
        public Matrix4x4 Model, ViewProj;
        public Vector4 Color, Light, OutlineColor, Params;
        public int Skinned, Textured, Look, OutlinePass;
    }

    /// <summary>A control's frame on Metal, in <see cref="RenderTarget.Native"/>.</summary>
    sealed class MtlTarget
    {
        public IntPtr Device;
        public IMTLTexture Color, Depth;
        public MTLRenderPassDescriptor Pass;

        public void Dispose()
        {
            Pass?.Dispose();
            Color?.Dispose();
            Depth?.Dispose();
        }
    }

    /// <summary>One model on the GPU: its vertex data in one buffer, per mesh, per primitive the offsets of each attribute.</summary>
    sealed class GpuModel
    {
        public IMTLBuffer Buffer;
        public nuint[][][] Offsets; // [mesh][primitive][0..5 attributes, 6 indices]
        public int[][] Counts;
        public IMTLTexture[] Textures;
    }

    static IntPtr _device;
    static IMTLDevice _mtlDevice;
    static IMTLRenderPipelineState _opaque, _blend;
    static IMTLDepthStencilState _depthWrite, _depthRead;
    static IMTLSamplerState _sampler;
    static IMTLTexture _white;
    static bool _failed;
    static ConditionalWeakTable<Scene3D, GpuModel> _models = new();
    // ponytail: like models, the texture of an image that is gone is freed only with the device.
    /// <summary>Images put on materials from code (Texture3D), by their pixels: uploaded once per device.</summary>
    static ConditionalWeakTable<Image3D, IMTLTexture> _images = new();
    static readonly Matrix4x4[] _joints = new Matrix4x4[MaxJoints];
    static readonly Vector3 Light = Vector3.Normalize(new Vector3(0.4f, 0.8f, 0.6f));

    /// <summary>Frees a control's textures. Metal keeps them alive while a command buffer still uses them.</summary>
    public static void Release(RenderTarget target)
    {
        target.Image?.Dispose();
        target.Image = null;
        (target.Native as MtlTarget)?.Dispose();
        target.Native = null;
    }

    /// <summary>Draws the objects into the control's texture on the canvas's Metal device; null when it cannot.</summary>
    public static SKImage Draw(DrawnView canvas, GRContext gr, RenderTarget target, RenderItem[] items, int count, in RenderStyle style, in Matrix4x4 viewProj, int w, int h)
    {
        if (_failed || (canvas?.CanvasView as Microsoft.Maui.Controls.View)?.Handler?.PlatformView is not SKMetalViewRetained view)
            return null;
        var device = view.Device;
        var queue = view.Queue;
        if (device == null || queue == null)
            return null;
        try
        {
            if (device.Handle != _device)
                Init(device);
            var t = EnsureTarget(gr, target, w, h);
            Render(queue, t, items, count, style, viewProj, w, h);
            return target.Image;
        }
        catch (Exception e)
        {
            // ponytail: one failure turns the 3D off for the process; a shader the device refuses is the likely cause.
            _failed = true;
            Super.Log($"[SkiaModel3D] Metal: {e.Message}");
            return null;
        }
    }

    /// <summary>A new device (the first one): the pipelines and the models are made on it.</summary>
    static void Init(IMTLDevice device)
    {
        _device = device.Handle;
        _mtlDevice = device;
        _models = new ConditionalWeakTable<Scene3D, GpuModel>();
        _images = new ConditionalWeakTable<Image3D, IMTLTexture>();

        using var library = device.CreateLibrary(Source, new MTLCompileOptions(), out var error);
        if (library == null)
            throw new InvalidOperationException($"shaders: {error?.LocalizedDescription}");
        using var vs = library.CreateFunction("model_vertex");
        using var fs = library.CreateFunction("model_fragment");

        // One buffer per attribute, as the GL renderer: position, normal, uv, joints, weights, outline normal.
        var layout = new MTLVertexDescriptor();
        var sizes = new[] { 3, 3, 2, 4, 4, 3 };
        for (var i = 0; i < 6; i++)
        {
            layout.Attributes[i].Format = sizes[i] == 2 ? MTLVertexFormat.Float2 : sizes[i] == 3 ? MTLVertexFormat.Float3 : MTLVertexFormat.Float4;
            layout.Attributes[i].BufferIndex = (nuint)i;
            layout.Attributes[i].Offset = 0;
            layout.Layouts[i].Stride = (nuint)(sizes[i] * 4);
        }
        _opaque = Pipeline(device, vs, fs, layout, false);
        _blend = Pipeline(device, vs, fs, layout, true);

        _depthWrite = device.CreateDepthStencilState(new MTLDepthStencilDescriptor { DepthCompareFunction = MTLCompareFunction.Less, DepthWriteEnabled = true });
        _depthRead = device.CreateDepthStencilState(new MTLDepthStencilDescriptor { DepthCompareFunction = MTLCompareFunction.Less, DepthWriteEnabled = false });
        _sampler = device.CreateSamplerState(new MTLSamplerDescriptor
        {
            MinFilter = MTLSamplerMinMagFilter.Linear, MagFilter = MTLSamplerMinMagFilter.Linear, MipFilter = MTLSamplerMipFilter.Linear,
            SAddressMode = MTLSamplerAddressMode.Repeat, TAddressMode = MTLSamplerAddressMode.Repeat,
        });
        _white = UploadTexture(new Image3D { Width = 1, Height = 1, Rgba = new byte[] { 255, 255, 255, 255 } }, null);
    }

    static IMTLRenderPipelineState Pipeline(IMTLDevice device, IMTLFunction vs, IMTLFunction fs, MTLVertexDescriptor layout, bool blend)
    {
        var info = new MTLRenderPipelineDescriptor
        {
            VertexFunction = vs,
            FragmentFunction = fs,
            VertexDescriptor = layout,
            DepthAttachmentPixelFormat = MTLPixelFormat.Depth32Float,
        };
        var color = info.ColorAttachments[0];
        color.PixelFormat = MTLPixelFormat.RGBA8Unorm;
        if (blend)
        {
            color.BlendingEnabled = true;
            color.SourceRgbBlendFactor = MTLBlendFactor.One;
            color.DestinationRgbBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;
            color.RgbBlendOperation = MTLBlendOperation.Add;
            color.SourceAlphaBlendFactor = MTLBlendFactor.One;
            color.DestinationAlphaBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;
            color.AlphaBlendOperation = MTLBlendOperation.Add;
        }
        var state = device.CreateRenderPipelineState(info, out var error);
        if (state == null)
            throw new InvalidOperationException($"pipeline: {error?.LocalizedDescription}");
        return state;
    }

    /// <summary>A texture with its mipmaps, as glGenerateMipmap. <paramref name="queue"/> null: no mipmaps (1x1).</summary>
    static IMTLTexture UploadTexture(Image3D source, IMTLCommandQueue queue)
    {
        var mipmapped = queue != null && (source.Width > 1 || source.Height > 1);
        var info = MTLTextureDescriptor.CreateTexture2DDescriptor(MTLPixelFormat.RGBA8Unorm, (nuint)source.Width, (nuint)source.Height, mipmapped);
        info.Usage = MTLTextureUsage.ShaderRead;
        var texture = _mtlDevice.CreateTexture(info);
        fixed (byte* p = source.Rgba)
            texture.ReplaceRegion(MTLRegion.Create2D(0, 0, source.Width, source.Height), 0, (IntPtr)p, (nuint)(source.Width * 4));
        if (mipmapped)
        {
            using var cb = queue.CommandBuffer();
            using var blit = cb.BlitCommandEncoder;
            blit.GenerateMipmapsForTexture(texture);
            blit.EndEncoding();
            cb.Commit();
        }
        return texture;
    }

    static GpuModel Upload(Scene3D scene, IMTLCommandQueue queue)
    {
        var model = new GpuModel
        {
            Textures = new IMTLTexture[scene.Images.Length],
            Offsets = new nuint[scene.Meshes.Length][][],
            Counts = new int[scene.Meshes.Length][],
        };
        for (var i = 0; i < scene.Images.Length; i++)
            model.Textures[i] = UploadTexture(scene.Images[i], queue);

        // All vertex and index data of the model in one buffer; attributes a primitive lacks get the values GL's
        // generic attributes give (uv and joints 0, weight 1 on the first joint).
        static nuint Align(nuint v) => (v + 15) & ~(nuint)15;
        nuint total = 0;
        foreach (var primitives in scene.Meshes)
        foreach (var p in primitives)
        {
            var n = (nuint)p.Positions.Length;
            total += Align(n * 12) * 3 + Align(n * 8) + Align(n * 16) * 2 + Align((nuint)p.Indices.Length * 4);
        }
        var buffer = _mtlDevice.CreateBuffer(Math.Max(16, total), MTLResourceOptions.StorageModeShared);
        var mapped = (byte*)buffer.Contents;
        nuint at = 0;
        nuint Put<T>(T[] data, int count, T fill) where T : unmanaged
        {
            var start = at;
            var dst = (T*)(mapped + at);
            if (data != null && data.Length == count)
                fixed (T* src = data)
                    System.Buffer.MemoryCopy(src, dst, (long)count * sizeof(T), (long)count * sizeof(T));
            else
                for (var i = 0; i < count; i++)
                    dst[i] = fill;
            at = Align(at + (nuint)(count * sizeof(T)));
            return start;
        }
        for (var m = 0; m < scene.Meshes.Length; m++)
        {
            var primitives = scene.Meshes[m];
            model.Offsets[m] = new nuint[primitives.Length][];
            model.Counts[m] = new int[primitives.Length];
            for (var k = 0; k < primitives.Length; k++)
            {
                var p = primitives[k];
                var n = p.Positions.Length;
                model.Offsets[m][k] = new[]
                {
                    Put(p.Positions, n, Vector3.Zero),
                    Put(p.Normals, n, Vector3.Zero),
                    Put(p.Uvs, n, Vector2.Zero),
                    Put(p.Joints, n, Vector4.Zero),
                    Put(p.Weights, n, new Vector4(1, 0, 0, 0)),
                    Put(p.OutlineDirections(), n, Vector3.Zero),
                    Put(p.Indices, p.Indices.Length, 0u),
                };
                model.Counts[m][k] = p.Indices.Length;
            }
        }
        model.Buffer = buffer;
        return model;
    }

    /// <summary>The texture of an image put on a material, uploaded the first time it is drawn.</summary>
    static IMTLTexture ImageTexture(Image3D image, IMTLCommandQueue queue)
    {
        if (!_images.TryGetValue(image, out var texture))
        {
            texture = UploadTexture(image, queue);
            _images.AddOrUpdate(image, texture);
        }
        return texture;
    }

    static GpuModel Model(Scene3D scene, IMTLCommandQueue queue)
    {
        if (!_models.TryGetValue(scene, out var model))
        {
            model = Upload(scene, queue);
            _models.AddOrUpdate(scene, model);
        }
        return model;
    }

    static MtlTarget EnsureTarget(GRContext gr, RenderTarget target, int w, int h)
    {
        var t = target.Native as MtlTarget;
        if (t != null && t.Device == _device && target.Width == w && target.Height == h && target.Image != null)
            return t;
        Release(target);

        var colorInfo = MTLTextureDescriptor.CreateTexture2DDescriptor(MTLPixelFormat.RGBA8Unorm, (nuint)w, (nuint)h, false);
        colorInfo.Usage = MTLTextureUsage.RenderTarget | MTLTextureUsage.ShaderRead;
        colorInfo.StorageMode = MTLStorageMode.Private;
        var depthInfo = MTLTextureDescriptor.CreateTexture2DDescriptor(MTLPixelFormat.Depth32Float, (nuint)w, (nuint)h, false);
        depthInfo.Usage = MTLTextureUsage.RenderTarget;
        depthInfo.StorageMode = MTLStorageMode.Private;
        t = new MtlTarget { Device = _device, Color = _mtlDevice.CreateTexture(colorInfo), Depth = _mtlDevice.CreateTexture(depthInfo) };

        // Made once per size: the frames only reuse it.
        t.Pass = new MTLRenderPassDescriptor();
        var color = t.Pass.ColorAttachments[0];
        color.Texture = t.Color;
        color.LoadAction = MTLLoadAction.Clear;
        color.StoreAction = MTLStoreAction.Store;
        color.ClearColor = new MTLClearColor(0, 0, 0, 0);
        var depth = t.Pass.DepthAttachment;
        depth.Texture = t.Depth;
        depth.LoadAction = MTLLoadAction.Clear;
        depth.StoreAction = MTLStoreAction.DontCare;
        depth.ClearDepth = 1;

        target.Native = t;
        target.Context = gr;
        target.Width = w;
        target.Height = h;
        // The texture keeps its identity: one image wraps it until the next resize. Metal's rows are top-left first.
        using var backend = new GRBackendTexture(w, h, false, new GRMtlTextureInfo(t.Color.Handle));
        target.Image = SKImage.FromTexture(gr, backend, GRSurfaceOrigin.TopLeft, SKColorType.Rgba8888, SKAlphaType.Premul);
        return t;
    }

    static void Render(IMTLCommandQueue queue, MtlTarget t, RenderItem[] items, int count, in RenderStyle style, in Matrix4x4 viewProj, int w, int h)
    {
        // Models and images first: an upload may commit its own command buffer (mipmaps).
        for (var o = 0; o < count; o++)
        {
            Model(items[o].Scene, queue);
            foreach (var m in items[o].Materials)
                if (m.Image != null)
                    ImageTexture(m.Image.Pixels, queue);
        }

        // ponytail: the command buffer and encoder are new ObjC wrappers each frame (Metal has no reusable ones).
        using var cb = queue.CommandBuffer();
        using var encoder = cb.CreateRenderCommandEncoder(t.Pass);
        encoder.SetViewport(new MTLViewport(0, 0, w, h, 0, 1));
        // GL's counter-clockwise front faces: Metal keeps the y-up clip space, so the winding stays.
        encoder.SetFrontFacingWinding(MTLWinding.CounterClockwise);
        encoder.SetFragmentSamplerState(_sampler, 0);

        var block = new DrawBlock
        {
            ViewProj = viewProj,
            Light = new Vector4(Light, 0),
            OutlineColor = style.OutlineColor,
            Params = new Vector4(0, w, h, 0),
            Look = (int)style.Look,
        };
        var state = new EncoderState { Cull = -1 };
        var firstPass = style.Outline > 0 ? 0 : 1;
        for (var pass = firstPass; pass < 3; pass++)
        {
            var outlinePass = pass == 0;
            var blendPass = pass == 2;
            block.OutlinePass = outlinePass ? 1 : 0;
            block.Params.X = outlinePass ? style.Outline : 0;
            encoder.SetRenderPipelineState(blendPass ? _blend : _opaque);
            encoder.SetDepthStencilState(blendPass ? _depthRead : _depthWrite);
            for (var o = 0; o < count; o++)
                DrawItem(encoder, queue, items[o], outlinePass, blendPass, ref block, ref state);
        }

        encoder.EndEncoding();
        cb.Commit();
    }

    /// <summary>Encoder state set during one frame, so a crowd of one model does not repeat it per object.</summary>
    struct EncoderState
    {
        /// <summary>-1 = not set, else the <see cref="MTLCullMode"/>.</summary>
        public int Cull;
        public IntPtr Texture;
    }

    static void DrawItem(IMTLRenderCommandEncoder encoder, IMTLCommandQueue queue, in RenderItem item, bool outlinePass, bool blendPass,
        ref DrawBlock block, ref EncoderState state)
    {
        var scene = item.Scene;
        var gpu = Model(scene, queue);
        var nodes = scene.Nodes;
        for (var i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (node.Mesh < 0 || node.Mesh >= gpu.Offsets.Length || item.Hidden[i])
                continue;
            var skinned = node.Skin >= 0 && node.Skin < scene.Skins.Length;
            var primitives = scene.Meshes[node.Mesh];
            var jointsSet = false;
            for (var k = 0; k < primitives.Length; k++)
            {
                var p = primitives[k];
                var materialIndex = p.Material;
                var material = materialIndex >= 0 && materialIndex < scene.Materials.Length ? scene.Materials[materialIndex] : null;
                var changed = materialIndex >= 0 && materialIndex < item.Materials.Length ? item.Materials[materialIndex] : default;
                if (changed.Hidden || (material?.Blend ?? false) != blendPass)
                    continue;
                if (!jointsSet)
                {
                    // A skinned mesh is placed by its joints only (glTF ignores its node's transform).
                    jointsSet = true;
                    block.Model = skinned ? Matrix4x4.Identity : item.Pose[i];
                    // Bound for every draw (the shader declares it); unskinned draws read none of it.
                    var n = skinned ? Math.Max(1, scene.JointMatrices(node.Skin, item.Pose, _joints)) : 1;
                    fixed (Matrix4x4* j = _joints)
                        encoder.SetVertexBytes((IntPtr)j, (nuint)(n * sizeof(Matrix4x4)), 7);
                }
                block.Color = changed.HasColor ? changed.Color : material?.Color ?? new Vector4(0.8f, 0.8f, 0.8f, 1);
                // An image put on the material from code, else the file's own.
                var texture = changed.Image != null ? ImageTexture(changed.Image.Pixels, queue)
                    : material != null && material.Texture >= 0 && material.Texture < gpu.Textures.Length ? gpu.Textures[material.Texture] : null;
                block.Textured = texture != null ? 1 : 0;
                block.Skinned = skinned && p.Joints.Length > 0 ? 1 : 0;
                var b = block;
                encoder.SetVertexBytes((IntPtr)(&b), (nuint)sizeof(DrawBlock), 6);
                encoder.SetFragmentBytes((IntPtr)(&b), (nuint)sizeof(DrawBlock), 0);

                var cull = (int)(outlinePass ? MTLCullMode.Front : (material?.DoubleSided ?? false) ? MTLCullMode.None : MTLCullMode.Back);
                if (state.Cull != cull)
                {
                    encoder.SetCullMode((MTLCullMode)cull);
                    state.Cull = cull;
                }
                var bound = texture ?? _white;
                if (state.Texture != bound.Handle)
                {
                    encoder.SetFragmentTexture(bound, 0);
                    state.Texture = bound.Handle;
                }
                var at = gpu.Offsets[node.Mesh][k];
                for (var a = 0; a < 6; a++)
                    encoder.SetVertexBuffer(gpu.Buffer, at[a], (nuint)a);
                encoder.DrawIndexedPrimitives(MTLPrimitiveType.Triangle, (nuint)gpu.Counts[node.Mesh][k], MTLIndexType.UInt32, gpu.Buffer, at[6]);
            }
        }
    }
}
