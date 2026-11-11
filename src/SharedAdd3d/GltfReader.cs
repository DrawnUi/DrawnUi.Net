#nullable disable
using System.Numerics;
using System.Text.Json;
using SkiaSharp;

namespace DrawnUi.Add3d;

// ponytail: base color factor and texture only (no normal, metallic-roughness or emissive maps), no morph targets,
// no sparse accessors, vertex colors ignored, CUBICSPLINE samplers read as linear. Add them when a model needs them.
internal sealed partial class Scene3D
{
    /// <summary>Reads a .glb (or a .gltf with its buffers embedded as data: URIs).</summary>
    public static Scene3D Load(byte[] bytes)
    {
        var (json, bin) = SplitGlb(bytes);
        using var doc = JsonDocument.Parse(json);
        return new GltfReader(doc.RootElement, bin).Read();
    }

    static (ReadOnlyMemory<byte> Json, byte[] Bin) SplitGlb(byte[] bytes)
    {
        if (bytes.Length >= 20 && bytes[0] == (byte)'g' && bytes[1] == (byte)'l' && bytes[2] == (byte)'T' && bytes[3] == (byte)'F')
        {
            var length = Math.Min(BitConverter.ToInt32(bytes, 8), bytes.Length);
            ReadOnlyMemory<byte> json = default;
            byte[] bin = null;
            var at = 12;
            while (at + 8 <= length)
            {
                var chunkLength = BitConverter.ToInt32(bytes, at);
                var chunkType = BitConverter.ToUInt32(bytes, at + 4);
                var start = at + 8;
                if (chunkLength < 0 || start + chunkLength > length)
                    throw new InvalidDataException("broken GLB chunk");
                if (chunkType == 0x4E4F534A) // JSON
                    json = new ReadOnlyMemory<byte>(bytes, start, chunkLength);
                else if (chunkType == 0x004E4942) // BIN
                    bin = bytes.AsSpan(start, chunkLength).ToArray();
                at = start + chunkLength;
            }
            if (json.IsEmpty)
                throw new InvalidDataException("GLB without a JSON chunk");
            return (json, bin);
        }
        var first = Array.FindIndex(bytes, b => b is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0xEF or 0xBB or 0xBF));
        if (first < 0 || bytes[first] != (byte)'{')
            throw new InvalidDataException("not a glTF or GLB file");
        return (bytes, null);
    }

    sealed class GltfReader
    {
        readonly JsonElement _root;
        readonly byte[] _bin;
        readonly List<byte[]> _buffers = new();

        readonly JsonElement[] _accessors, _views, _textures;

        public GltfReader(JsonElement root, byte[] bin)
        {
            _root = root;
            _bin = bin;
            _accessors = Array("accessors");
            _views = Array("bufferViews");
            _textures = Array("textures");
        }

        JsonElement[] Array(string name) =>
            _root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Array
                ? e.EnumerateArray().ToArray()
                : System.Array.Empty<JsonElement>();

        static int Int(JsonElement e, string name, int fallback = -1) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : fallback;

        static string Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        static float[] Floats(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array)
                return null;
            return v.EnumerateArray().Select(x => x.GetSingle()).ToArray();
        }

        public Scene3D Read()
        {
            foreach (var b in Array("buffers"))
            {
                var uri = Str(b, "uri");
                if (uri == null)
                    _buffers.Add(_bin ?? throw new InvalidDataException("no binary chunk"));
                else
                    _buffers.Add(DataUri(uri) ?? throw new InvalidDataException($"external buffer {uri} (use a .glb)"));
            }

            var scene = new Scene3D
            {
                Images = Array("images").Select(ReadImage).ToArray(),
                Materials = Array("materials").Select(ReadMaterial).ToArray(),
                Meshes = Array("meshes").Select(ReadMesh).ToArray(),
                Skins = Array("skins").Select(ReadSkin).ToArray(),
            };

            var nodes = Array("nodes");
            scene.Nodes = nodes.Select(ReadNode).ToArray();
            for (var i = 0; i < nodes.Length; i++)
            {
                if (!nodes[i].TryGetProperty("children", out var children))
                    continue;
                foreach (var child in children.EnumerateArray())
                {
                    var c = child.GetInt32();
                    if (c >= 0 && c < scene.Nodes.Length && c != i)
                        scene.Nodes[c].Parent = i;
                }
            }

            scene.Animations = Array("animations").Select(ReadAnimation).ToArray();
            scene.Finish();
            return scene;
        }

        Image3D ReadImage(JsonElement image)
        {
            byte[] encoded = null;
            var view = Int(image, "bufferView");
            if (view >= 0)
            {
                var (bytes, offset, length, _) = View(view);
                encoded = bytes.AsSpan(offset, length).ToArray();
            }
            else if (Str(image, "uri") is { } uri)
                encoded = DataUri(uri);
            return DecodeImage(encoded) ?? new Image3D { Rgba = new byte[] { 255, 255, 255, 255 }, Width = 1, Height = 1 };
        }

        Material3D ReadMaterial(JsonElement m)
        {
            var material = new Material3D
            {
                Name = Str(m, "name") ?? "",
                DoubleSided = m.TryGetProperty("doubleSided", out var ds) && ds.ValueKind == JsonValueKind.True,
                Blend = Str(m, "alphaMode") == "BLEND",
            };
            if (m.TryGetProperty("pbrMetallicRoughness", out var pbr))
            {
                if (Floats(pbr, "baseColorFactor") is { Length: 4 } c)
                    material.Color = new Vector4(c[0], c[1], c[2], c[3]);
                if (pbr.TryGetProperty("roughnessFactor", out var rough) && rough.ValueKind == JsonValueKind.Number)
                    material.Roughness = Math.Clamp(rough.GetSingle(), 0, 1);
                if (pbr.TryGetProperty("baseColorTexture", out var bct))
                {
                    var texture = Int(bct, "index");
                    if (texture >= 0 && texture < _textures.Length)
                        material.Texture = Int(_textures[texture], "source");
                }
            }
            return material;
        }

        Primitive3D[] ReadMesh(JsonElement mesh)
        {
            var result = new List<Primitive3D>();
            if (!mesh.TryGetProperty("primitives", out var primitives))
                return result.ToArray();
            foreach (var p in primitives.EnumerateArray())
            {
                if (Int(p, "mode", 4) != 4 || !p.TryGetProperty("attributes", out var attributes))
                    continue;
                var position = Int(attributes, "POSITION");
                if (position < 0)
                    continue;
                var positions = Vectors3(position);
                var n = positions.Length;
                var normal = Int(attributes, "NORMAL");
                var uv = Int(attributes, "TEXCOORD_0");
                var joints = Int(attributes, "JOINTS_0");
                var weights = Int(attributes, "WEIGHTS_0");
                var indices = Int(p, "indices");
                var primitive = new Primitive3D
                {
                    Positions = positions,
                    Normals = normal >= 0 ? Vectors3(normal) : Enumerable.Repeat(Vector3.UnitY, n).ToArray(),
                    Uvs = uv >= 0 ? Vectors2(uv) : new Vector2[n],
                    Joints = joints >= 0 ? Vectors4(joints) : System.Array.Empty<Vector4>(),
                    Weights = weights >= 0 ? Vectors4(weights) : System.Array.Empty<Vector4>(),
                    Indices = indices >= 0 ? Accessor(indices, 1).Select(x => (uint)x).ToArray() : Enumerable.Range(0, n).Select(x => (uint)x).ToArray(),
                    Material = Int(p, "material"),
                };
                if (primitive.Normals.Length != n)
                    primitive.Normals = Enumerable.Repeat(Vector3.UnitY, n).ToArray();
                if (primitive.Uvs.Length != n)
                    primitive.Uvs = new Vector2[n];
                if (primitive.Joints.Length != n || primitive.Weights.Length != n)
                {
                    primitive.Joints = System.Array.Empty<Vector4>();
                    primitive.Weights = System.Array.Empty<Vector4>();
                }
                result.Add(primitive);
            }
            return result.ToArray();
        }

        Skin3D ReadSkin(JsonElement skin)
        {
            var joints = skin.TryGetProperty("joints", out var j) ? j.EnumerateArray().Select(x => x.GetInt32()).ToArray() : System.Array.Empty<int>();
            var ibm = Int(skin, "inverseBindMatrices");
            var inverse = Enumerable.Repeat(Matrix4x4.Identity, joints.Length).ToArray();
            if (ibm >= 0)
            {
                var values = Accessor(ibm, 16);
                for (var i = 0; i < joints.Length && (i + 1) * 16 <= values.Length; i++)
                    inverse[i] = ColumnMajor(values.AsSpan(i * 16, 16));
            }
            return new Skin3D { Joints = joints, InverseBind = inverse };
        }

        Node3D ReadNode(JsonElement n)
        {
            var node = new Node3D
            {
                Name = Str(n, "name") ?? "",
                Mesh = Int(n, "mesh"),
                Skin = Int(n, "skin"),
                Rest = new NodeTransform(Vector3.Zero, Quaternion.Identity, Vector3.One),
            };
            if (Floats(n, "matrix") is { Length: 16 } m)
            {
                Matrix4x4.Decompose(ColumnMajor(m), out var s, out var r, out var t);
                node.Rest = new NodeTransform(t, r, s);
            }
            else
            {
                if (Floats(n, "translation") is { Length: 3 } t)
                    node.Rest.T = new Vector3(t[0], t[1], t[2]);
                if (Floats(n, "rotation") is { Length: 4 } r)
                    node.Rest.R = new Quaternion(r[0], r[1], r[2], r[3]);
                if (Floats(n, "scale") is { Length: 3 } s)
                    node.Rest.S = new Vector3(s[0], s[1], s[2]);
            }
            return node;
        }

        int _animationIndex;

        Animation3D ReadAnimation(JsonElement a)
        {
            var index = ++_animationIndex;
            var samplers = a.TryGetProperty("samplers", out var s) ? s.EnumerateArray().ToArray() : System.Array.Empty<JsonElement>();
            var duration = 0f;
            var channels = new List<Channel3D>();
            if (a.TryGetProperty("channels", out var cs))
            {
                foreach (var c in cs.EnumerateArray())
                {
                    var sampler = Int(c, "sampler");
                    if (sampler < 0 || sampler >= samplers.Length || !c.TryGetProperty("target", out var target))
                        continue;
                    var node = Int(target, "node");
                    var path = Str(target, "path");
                    if (node < 0)
                        continue;
                    ChannelPath kind;
                    int width;
                    switch (path)
                    {
                        case "translation": kind = ChannelPath.Translation; width = 3; break;
                        case "rotation": kind = ChannelPath.Rotation; width = 4; break;
                        case "scale": kind = ChannelPath.Scale; width = 3; break;
                        default: continue; // morph target weights
                    }
                    var input = Int(samplers[sampler], "input");
                    var output = Int(samplers[sampler], "output");
                    if (input < 0 || output < 0)
                        continue;
                    var interpolation = Str(samplers[sampler], "interpolation") ?? "LINEAR";
                    var times = Accessor(input, 1);
                    if (times.Length > 0)
                        duration = MathF.Max(duration, times[^1]);
                    var values = Accessor(output, width);
                    if (interpolation == "CUBICSPLINE")
                    {
                        // Keys hold (in tangent, value, out tangent): keep the values.
                        var kept = new float[values.Length / 3];
                        for (var k = 0; (k + 1) * width * 3 <= values.Length; k++)
                            System.Array.Copy(values, k * width * 3 + width, kept, k * width, width);
                        values = kept;
                    }
                    channels.Add(new Channel3D { Node = node, Path = kind, Step = interpolation == "STEP", Times = times, Values = values });
                }
            }
            return new Animation3D { Name = Str(a, "name") ?? $"Animation {index}", Duration = duration, Channels = channels.ToArray() };
        }

        (byte[] Bytes, int Offset, int Length, int Stride) View(int index)
        {
            if (index < 0 || index >= _views.Length)
                throw new InvalidDataException($"no buffer view {index}");
            var v = _views[index];
            var buffer = Int(v, "buffer", 0);
            if (buffer < 0 || buffer >= _buffers.Count)
                throw new InvalidDataException($"no buffer {buffer}");
            var bytes = _buffers[buffer];
            var offset = Int(v, "byteOffset", 0);
            var length = Int(v, "byteLength", 0);
            if (offset < 0 || length < 0 || offset + length > bytes.Length)
                throw new InvalidDataException($"buffer view {index} out of its buffer");
            return (bytes, offset, length, Int(v, "byteStride", 0));
        }

        static int Components(string type) => type switch
        {
            "SCALAR" => 1,
            "VEC2" => 2,
            "VEC3" => 3,
            "VEC4" => 4,
            "MAT2" => 4,
            "MAT3" => 9,
            "MAT4" => 16,
            _ => 1
        };

        /// <summary>An accessor as floats, <paramref name="width"/> per element (normalized integers mapped to 0..1 / -1..1).</summary>
        float[] Accessor(int index, int width)
        {
            if (index < 0 || index >= _accessors.Length)
                throw new InvalidDataException($"no accessor {index}");
            var a = _accessors[index];
            var count = Int(a, "count", 0);
            var components = Components(Str(a, "type"));
            var kind = Int(a, "componentType", 5126);
            var normalized = a.TryGetProperty("normalized", out var nz) && nz.ValueKind == JsonValueKind.True;
            var result = new float[count * width];
            var view = Int(a, "bufferView");
            if (view < 0)
                return result; // all zeros (sparse-only accessors are not read)
            var (bytes, viewOffset, viewLength, stride) = View(view);
            var size = kind switch { 5120 or 5121 => 1, 5122 or 5123 => 2, _ => 4 };
            if (stride == 0)
                stride = size * components;
            var start = viewOffset + Int(a, "byteOffset", 0);
            var take = Math.Min(width, components);
            for (var i = 0; i < count; i++)
            {
                var at = start + i * stride;
                if (at + size * take > viewOffset + viewLength)
                    throw new InvalidDataException($"accessor {index} out of its buffer view");
                for (var c = 0; c < take; c++)
                    result[i * width + c] = Read(bytes, at + c * size, kind, normalized);
            }
            return result;
        }

        static float Read(byte[] b, int at, int kind, bool normalized) => kind switch
        {
            5120 => normalized ? MathF.Max((sbyte)b[at] / 127f, -1f) : (sbyte)b[at],
            5121 => normalized ? b[at] / 255f : b[at],
            5122 => normalized ? MathF.Max(BitConverter.ToInt16(b, at) / 32767f, -1f) : BitConverter.ToInt16(b, at),
            5123 => normalized ? BitConverter.ToUInt16(b, at) / 65535f : BitConverter.ToUInt16(b, at),
            5125 => BitConverter.ToUInt32(b, at),
            _ => BitConverter.ToSingle(b, at)
        };

        Vector2[] Vectors2(int accessor)
        {
            var f = Accessor(accessor, 2);
            var v = new Vector2[f.Length / 2];
            for (var i = 0; i < v.Length; i++)
                v[i] = new Vector2(f[i * 2], f[i * 2 + 1]);
            return v;
        }

        Vector3[] Vectors3(int accessor)
        {
            var f = Accessor(accessor, 3);
            var v = new Vector3[f.Length / 3];
            for (var i = 0; i < v.Length; i++)
                v[i] = new Vector3(f[i * 3], f[i * 3 + 1], f[i * 3 + 2]);
            return v;
        }

        Vector4[] Vectors4(int accessor)
        {
            var f = Accessor(accessor, 4);
            var v = new Vector4[f.Length / 4];
            for (var i = 0; i < v.Length; i++)
                v[i] = new Vector4(f[i * 4], f[i * 4 + 1], f[i * 4 + 2], f[i * 4 + 3]);
            return v;
        }

        /// <summary>A glTF (column-major, column vectors) matrix as a System.Numerics one (row vectors): the same 16 floats.</summary>
        static Matrix4x4 ColumnMajor(ReadOnlySpan<float> m) => new(
            m[0], m[1], m[2], m[3],
            m[4], m[5], m[6], m[7],
            m[8], m[9], m[10], m[11],
            m[12], m[13], m[14], m[15]);
    }

    /// <summary>data:...;base64, contents; null when not a base64 data URI.</summary>
    internal static byte[] DataUri(string uri)
    {
        if (!uri.StartsWith("data:", StringComparison.Ordinal))
            return null;
        var at = uri.IndexOf(";base64,", StringComparison.Ordinal);
        if (at < 0)
            return null;
        try
        {
            return Convert.FromBase64String(uri.Substring(at + 8));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>PNG / JPEG / WebP through Skia's codecs, as unpremultiplied RGBA.</summary>
    internal static Image3D DecodeImage(byte[] encoded)
    {
        if (encoded == null || encoded.Length == 0)
            return null;
        using var image = SKImage.FromEncodedData(encoded);
        return ImagePixels(image);
    }

    /// <summary>An image's pixels as unpremultiplied RGBA; null when it has none on the CPU (a GPU-only image).</summary>
    internal static Image3D ImagePixels(SKImage image)
    {
        if (image == null || image.IsTextureBacked)
            return null;
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        if (!image.ReadPixels(bitmap.PeekPixels()))
            return null;
        var pixels = bitmap.Bytes;
        return new Image3D { Rgba = pixels, Width = image.Width, Height = image.Height };
    }
}
