#nullable disable
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;
using System.Text.RegularExpressions;

// Stand-in for the SkiaSharp SKMesh API (SkiaSharp PR #3779, not in a released SkiaSharp yet).
// Same type names, members and semantics as SkiaSharp's own, drawn with what released SkiaSharp has
// (SKCanvas.DrawVertices + SKRuntimeEffect). When SkiaSharp ships SKMesh, delete
// `using DrawnUi.Draw.Meshes;` from your code and the real API takes over, nothing else changes.
namespace DrawnUi.Draw.Meshes;

/// <summary>How the vertices of an <see cref="SKMesh"/> make triangles (stand-in for SkiaSharp's SKMeshMode).</summary>
public enum SKMeshMode
{
    /// <summary>Every 3 vertices (or indices) make one triangle.</summary>
    Triangles = 0,
    /// <summary>Each vertex after the first two makes a triangle with the two before it.</summary>
    TriangleStrip = 1,
}

/// <summary>Type of a mesh vertex attribute (stand-in for SkiaSharp's SKMeshSpecificationAttributeType).</summary>
public enum SKMeshSpecificationAttributeType
{
    /// <summary>One float, SkSL <c>float</c>.</summary>
    Float = 0,
    /// <summary>Two floats, SkSL <c>float2</c>.</summary>
    Float2 = 1,
    /// <summary>Three floats, SkSL <c>float3</c>.</summary>
    Float3 = 2,
    /// <summary>Four floats, SkSL <c>float4</c>.</summary>
    Float4 = 3,
    /// <summary>Four bytes read as 0..1, SkSL <c>half4</c>.</summary>
    Ubyte4Unorm = 4,
}

/// <summary>Type of a mesh varying (stand-in for SkiaSharp's SKMeshSpecificationVaryingType).</summary>
public enum SKMeshSpecificationVaryingType
{
    /// <summary>SkSL <c>float</c>.</summary>
    Float = 0,
    /// <summary>SkSL <c>float2</c>.</summary>
    Float2 = 1,
    /// <summary>SkSL <c>float3</c>.</summary>
    Float3 = 2,
    /// <summary>SkSL <c>float4</c>.</summary>
    Float4 = 3,
    /// <summary>SkSL <c>half</c>.</summary>
    Half = 4,
    /// <summary>SkSL <c>half2</c>.</summary>
    Half2 = 5,
    /// <summary>SkSL <c>half3</c>.</summary>
    Half3 = 6,
    /// <summary>SkSL <c>half4</c>.</summary>
    Half4 = 7,
}

/// <summary>One vertex attribute: its type, byte offset inside a vertex and its SkSL name (stand-in for SkiaSharp's struct).</summary>
public struct SKMeshSpecificationAttribute
{
    /// <summary>Creates an attribute.</summary>
    public SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType type, int offset, string name)
    {
        Type = type;
        Offset = offset;
        Name = name;
    }

    /// <summary>Attribute type.</summary>
    public SKMeshSpecificationAttributeType Type { get; set; }
    /// <summary>Byte offset inside a vertex, a multiple of 4.</summary>
    public int Offset { get; set; }
    /// <summary>Field name in the SkSL <c>Attributes</c> struct.</summary>
    public string Name { get; set; }
}

/// <summary>One varying passed from the vertex to the fragment program (stand-in for SkiaSharp's struct).</summary>
public struct SKMeshSpecificationVarying
{
    /// <summary>Creates a varying.</summary>
    public SKMeshSpecificationVarying(SKMeshSpecificationVaryingType type, string name)
    {
        Type = type;
        Name = name;
    }

    /// <summary>Varying type.</summary>
    public SKMeshSpecificationVaryingType Type { get; set; }
    /// <summary>Field name in the SkSL <c>Varyings</c> struct.</summary>
    public string Name { get; set; }
}

/// <summary>
/// Vertex layout plus the SkSL vertex and fragment programs of a custom mesh (stand-in for SkiaSharp's
/// SKMeshSpecification). Same rules as Skia's SkMesh: vertex program <c>Varyings main(const Attributes a)</c>,
/// fragment program <c>float2 main(const Varyings v)</c> or <c>float2 main(const Varyings v, out half4|float4 color)</c>.
/// </summary>
public sealed class SKMeshSpecification : IDisposable
{
    const int MaxAttributes = 8;
    const int MaxVaryings = 6;
    const int MaxStride = 1024;

    // Texture side we never exceed: 4096 is the floor of GL ES 3 / WebGL 2 devices.
    internal const int MaxTextureSide = 4096;

    /// <summary>Bytes per vertex.</summary>
    public int Stride { get; }

    /// <summary>Bytes of uniform data the programs declare (vertex program uniforms first, then the fragment program's own).</summary>
    public int UniformSize { get; }

    internal SKMeshSpecificationAttribute[] Attributes { get; }
    internal bool HasColor { get; }
    internal int TexelsPerVertex { get; }      // RGBA texels of a vertex's attributes, packed (Ubyte4Unorm as 4 floats)

    // The vertex bytes reach the programs as an RGBA8 image (one float per texel, wrapped without a copy).
    // Convert program: once per vertex range, rebuilds each float from its bytes and packs a vertex's
    // attributes into TexelsPerVertex texels, as fp16 hi + lo rows (draw) or F32 (vertex program on the CPU).
    internal SKRuntimeEffect ConvertEffect { get; }
    // Draw program: vertex program run 3 times per pixel + fragment program, fed by DrawVertices texCoords; or,
    // when Varyings is set, the varyings the varyings pass computed per vertex, mixed per pixel.
    internal SKRuntimeEffect DrawEffect { get; }
    // Varyings pass (a vertex program without uniforms): packed attributes -> vertex program -> varyings packed
    // per vertex as the draw program reads them. Null when the draw program runs the vertex program itself.
    internal SKRuntimeEffect VaryingsEffect { get; private init; }
    internal VaryingLayout Varyings { get; private init; }
    // Vertex program alone, run on the CPU (raster) to get the vertex positions DrawVertices needs.
    // Null when the vertex program copies the "position" attribute as is.
    internal SKRuntimeEffect PositionEffect { get; }
    internal int PositionAttributeFloat { get; } = -1; // float index of the "position" attribute inside a vertex

    // Uniform and child holders of the programs, made once and refilled for every shader (ToShader copies
    // them), under EffectLock: building them per draw cost more than the shader itself.
    internal readonly object EffectLock = new();
    CachedUniforms _drawUniforms, _positionUniforms, _convertUniforms;
    SKRuntimeEffectChildren _drawChildren, _positionChildren, _convertChildren;
    internal CachedUniforms DrawUniforms => _drawUniforms ??= new CachedUniforms(DrawEffect);
    internal SKRuntimeEffectChildren DrawChildren => _drawChildren ??= new SKRuntimeEffectChildren(DrawEffect);
    internal CachedUniforms PositionUniforms => _positionUniforms ??= new CachedUniforms(PositionEffect);
    internal SKRuntimeEffectChildren PositionChildren => _positionChildren ??= new SKRuntimeEffectChildren(PositionEffect);
    internal CachedUniforms ConvertUniforms => _convertUniforms ??= new CachedUniforms(ConvertEffect);
    CachedUniforms _varyingsUniforms;
    SKRuntimeEffectChildren _varyingsChildren;
    internal CachedUniforms VaryingsUniforms => _varyingsUniforms ??= new CachedUniforms(VaryingsEffect);
    internal SKRuntimeEffectChildren VaryingsChildren => _varyingsChildren ??= new SKRuntimeEffectChildren(VaryingsEffect);

    // The draw program for a paint without a shader (a plain color): the color is read at one point, so the local
    // coords (the "position" varying) are unused and the GPU compiler drops their texel reads. Compiled at its
    // first use; null if it does not compile (the draw program is used then).
    string _drawSource, _varyingsSource; // kept for diagnostics (the generated SkSL)
    bool _solidTried;
    SKRuntimeEffect _drawSolidEffect;
    CachedUniforms _drawSolidUniforms;
    SKRuntimeEffectChildren _drawSolidChildren;
    internal SKRuntimeEffect DrawSolidEffect
    {
        get
        {
            if (!_solidTried)
            {
                _solidTried = true;
                _drawSolidEffect = SKRuntimeEffect.CreateShader(_drawSource.Replace("_sm_paint.eval(lc)", "_sm_paint.eval(float2(0.0))"), out _);
            }
            return _drawSolidEffect;
        }
    }
    internal CachedUniforms DrawSolidUniforms => _drawSolidUniforms ??= new CachedUniforms(DrawSolidEffect);
    internal SKRuntimeEffectChildren DrawSolidChildren => _drawSolidChildren ??= new SKRuntimeEffectChildren(DrawSolidEffect);
    internal SKRuntimeEffectChildren ConvertChildren => _convertChildren ??= new SKRuntimeEffectChildren(ConvertEffect);

    // A program's uniform holder that writes a value only when it differs from the one it holds (each write allocates
    // in SkiaSharp): a mesh drawn every frame writes only what changed since the last draw of its specification.
    internal sealed class CachedUniforms : IDisposable
    {
        readonly Dictionary<string, byte[]> _written = new();

        public CachedUniforms(SKRuntimeEffect effect) => Holder = new SKRuntimeEffectUniforms(effect);

        public SKRuntimeEffectUniforms Holder { get; }

        public void Set(string name, ReadOnlySpan<float> values, bool scalar = false)
        {
            var bytes = MemoryMarshal.AsBytes(values);
            if (Holds(name, bytes))
                return;
            if (scalar)
                Holder[name] = values[0];
            else
                Holder[name] = values;
            Keep(name, bytes);
        }

        public void Set(string name, ReadOnlySpan<int> values, bool scalar = false)
        {
            var bytes = MemoryMarshal.AsBytes(values);
            if (Holds(name, bytes))
                return;
            if (scalar)
                Holder[name] = values[0];
            else
                Holder[name] = values;
            Keep(name, bytes);
        }

        bool Holds(string name, ReadOnlySpan<byte> bytes)
            => _written.TryGetValue(name, out var last) && bytes.SequenceEqual(last);

        void Keep(string name, ReadOnlySpan<byte> bytes)
        {
            if (!_written.TryGetValue(name, out var last) || last.Length != bytes.Length)
                _written[name] = last = new byte[bytes.Length];
            bytes.CopyTo(last);
        }

        public void Dispose() => Holder.Dispose();
    }

    internal UniformSlot[] Uniforms { get; }          // user uniforms of the draw program, in mesh layout order
    internal UniformSlot[] VertexUniforms { get; }     // same, for the position program (vertex program uniforms only)
    internal ChildSlot[] Children { get; }             // user children, fragment program order

    internal readonly struct UniformSlot
    {
        public UniformSlot(string name, int offset, int size, bool isInt, bool isArray)
        {
            Name = name; Offset = offset; Size = size; IsInt = isInt; IsArray = isArray;
        }
        public readonly string Name;
        public readonly int Offset;
        public readonly int Size;
        public readonly bool IsInt;
        public readonly bool IsArray;
    }

    internal readonly struct ChildSlot
    {
        public ChildSlot(string name, string kind) { Name = name; Kind = kind; }
        public readonly string Name;
        public readonly string Kind; // "shader", "colorFilter", "blender"
    }

    SKMeshSpecification(int stride, SKMeshSpecificationAttribute[] attributes, int texels, bool hasColor, SKRuntimeEffect convert,
        SKRuntimeEffect draw, SKRuntimeEffect positions, int positionFloat,
        UniformSlot[] uniforms, UniformSlot[] vertexUniforms, int uniformSize, ChildSlot[] children)
    {
        Stride = stride;
        Attributes = attributes;
        TexelsPerVertex = texels;
        HasColor = hasColor;
        ConvertEffect = convert;
        DrawEffect = draw;
        PositionEffect = positions;
        PositionAttributeFloat = positionFloat;
        Uniforms = uniforms;
        VertexUniforms = vertexUniforms;
        UniformSize = uniformSize;
        Children = children;
    }

    /// <summary>
    /// Creates a specification whose fragment program does not output a color. Returns null and the reason
    /// in <paramref name="errors"/> on failure.
    /// </summary>
    public static SKMeshSpecification Make(ReadOnlySpan<SKMeshSpecificationAttribute> attributes, int vertexStride,
        ReadOnlySpan<SKMeshSpecificationVarying> varyings, string vertexShader, string fragmentShader, out string errors)
        => Make(attributes, vertexStride, varyings, vertexShader, fragmentShader, null, SKAlphaType.Premul, out errors);

    /// <summary>
    /// Creates a specification. A fragment program with an <c>out half4|float4 color</c> parameter needs
    /// <paramref name="colorSpace"/> and a valid <paramref name="alphaType"/>. Returns null and the reason in
    /// <paramref name="errors"/> on failure.
    /// </summary>
    public static SKMeshSpecification Make(ReadOnlySpan<SKMeshSpecificationAttribute> attributes, int vertexStride,
        ReadOnlySpan<SKMeshSpecificationVarying> varyings, string vertexShader, string fragmentShader,
        SKColorSpace colorSpace, SKAlphaType alphaType, out string errors)
    {
        if (attributes.Length == 0)
            throw new ArgumentException("At least one attribute is required.", nameof(attributes));
        if (vertexShader == null)
            throw new ArgumentNullException(nameof(vertexShader));
        if (fragmentShader == null)
            throw new ArgumentNullException(nameof(fragmentShader));

        errors = Validate(attributes, vertexStride, varyings);
        if (errors != null)
            return null;

        var vs = StripComments(vertexShader);
        var fs = StripComments(fragmentShader);

        if (!MainRegex.IsMatch(vs) || !MainRegex.IsMatch(fs))
        {
            errors = "No main function found.";
            return null;
        }

        // Fragment program signature: does it output a color?
        string colorType = null;
        var fsMain = FsMainRegex.Match(fs);
        if (fsMain.Success)
        {
            var color = FsColorParamRegex.Match(fsMain.Groups[1].Value);
            if (color.Success)
                colorType = color.Groups[1].Value;
        }
        bool hasColor = colorType != null;
        if (hasColor)
        {
            if (colorSpace == null)
            {
                errors = "Must provide a color space if FS returns a color.";
                return null;
            }
            if (alphaType == SKAlphaType.Unknown)
            {
                errors = "Must provide a valid alpha type if FS returns a color.";
                return null;
            }
        }

        // Uniforms and children of both programs.
        var vsUniforms = ParseUniforms(vs);
        foreach (var u in vsUniforms)
        {
            if (u.IsChild)
            {
                errors = "VS: error: effects are not permitted in mesh vertex shaders";
                return null;
            }
        }
        var fsUniforms = ParseUniforms(fs);
        var vsByName = vsUniforms.ToDictionary(x => x.Name);
        var fsDuplicates = new List<UniformDecl>();
        foreach (var u in fsUniforms)
        {
            if (vsByName.TryGetValue(u.Name, out var other))
            {
                if (other.Type != u.Type || other.Array != u.Array)
                {
                    errors = $"Uniform {u.Name} declared with different types in vertex and fragment shaders.";
                    return null;
                }
                fsDuplicates.Add(u);
            }
        }
        // The fragment program reuses the vertex program's declaration of a shared uniform.
        for (int i = fsDuplicates.Count - 1; i >= 0; i--)
            fs = fs.Remove(fsDuplicates[i].Index, fsDuplicates[i].Length).Insert(fsDuplicates[i].Index, new string(' ', fsDuplicates[i].Length));

        // Both programs are compiled separately by Skia: helpers with the same name in both are legal there,
        // here they share one program, so the fragment program's copies get their own name.
        var vsNames = TopLevelNames(vs);
        foreach (var name in TopLevelNames(fs))
        {
            if (name != "main" && vsNames.Contains(name))
                fs = Regex.Replace(fs, $@"(?<![\.\w]){Regex.Escape(name)}\b", name + "_smfs");
        }

        bool copiesPosition = IsPositionPassthrough(vs);
        vs = MainRegex.Replace(vs, "_sm_vs(");
        fs = MainRegex.Replace(fs, "_sm_fs(");

        // Packed attribute floats: attribute i at float floatIndex[i] of its vertex's texels. No attribute straddles
        // two texels (largest first, first texel with room), and "position" gets a texel of its own, last: a draw
        // whose fragment program does not read the position (local coords unused with a plain paint color) reads
        // none of it, the GPU compiler drops those texel reads.
        var attrs = attributes.ToArray();
        var varys = varyings.ToArray();
        int positionFloat = -1;
        int positionAttr = -1;
        var floatIndex = new int[attrs.Length];
        var room = new List<int>(); // free floats per texel
        foreach (int i in Enumerable.Range(0, attrs.Length).OrderByDescending(i => AttributeFloats(attrs[i].Type)).ThenBy(i => i))
        {
            if (attrs[i].Name == "position" && attrs[i].Type == SKMeshSpecificationAttributeType.Float2)
            {
                positionFloat = attrs[i].Offset / 4; // in the vertex bytes
                positionAttr = i;
                continue;
            }
            int n = AttributeFloats(attrs[i].Type);
            int t = room.FindIndex(free => free >= n);
            if (t < 0)
            {
                room.Add(4);
                t = room.Count - 1;
            }
            floatIndex[i] = t * 4 + 4 - room[t];
            room[t] -= n;
        }
        if (positionAttr >= 0)
        {
            floatIndex[positionAttr] = room.Count * 4;
            room.Add(2);
        }
        int texels = room.Count;

        var structs = BuildStructs(attrs, varys);

        // Vertex program alone: checked first so its errors carry the "VS:" prefix, and used on the CPU for
        // positions when the vertex program does more than copy the "position" attribute.
        // Input: the packed image, F32, or fp16 hi + lo rows when _sm_pk.w (rows of the high halves) is set.
        // _sm_pos.y = 0 (raster F32 target): pixel (x, y) = vertex y * _sm_pos.x + x, its position in .xy.
        // _sm_pos.y = 1 (GPU RGBA8 target, where F32 cannot be rendered): pixel (x, y) = the 4 IEEE bytes of
        // position.x (x even) or .y (x odd) at corner y * _sm_pos.x + x / 2 of the triangle list, so the read-back
        // bytes ARE the DrawVertices positions, in order.
        // The bytes are built with exact float math (powers of two by squaring): bit-exact on any GPU.
        var positionSource = new StringBuilder()
            .Append(structs).Append('\n').Append(vs).Append('\n')
            .Append("uniform shader _sm_attr;\nuniform float4 _sm_pk;\nuniform float4 _sm_pos;\n")
            .Append("float4 _sm_t(float x, float y) {\n  float2 c = float2(x + 0.5, y + 0.5);\n  float4 r = float4(_sm_attr.eval(c));\n")
            .Append("  if (_sm_pk.w > 0.0) { r += float4(_sm_attr.eval(float2(c.x, c.y + _sm_pk.w))); }\n  return r;\n}\n")
            .Append(BuildLoad(attrs, floatIndex, texels))
            .Append("float4 _sm_enc(float x) {\n  float a = abs(x);\n  if (a < 1.17549435e-38) { return float4(0.0); }\n")
            .Append("  float p = 1.0;\n  float ip = 1.0;\n  float e = 0.0;\n")
            .Append("  if (a >= 1.0) {\n")
            .Append("    float s = 2.0;\n    float r = 0.5;\n")
            // squares 2^1 .. 2^64, tried from the largest
            .Append("    float s1 = s; float s2 = s1 * s1; float s4 = s2 * s2; float s8 = s4 * s4; float s16 = s8 * s8; float s32 = s16 * s16; float s64 = s32 * s32;\n")
            .Append("    float r1 = r; float r2 = r1 * r1; float r4 = r2 * r2; float r8 = r4 * r4; float r16 = r8 * r8; float r32 = r16 * r16; float r64 = r32 * r32;\n")
            .Append("    if (p * s64 <= a) { p *= s64; ip *= r64; e += 64.0; }\n    if (p * s32 <= a) { p *= s32; ip *= r32; e += 32.0; }\n")
            .Append("    if (p * s16 <= a) { p *= s16; ip *= r16; e += 16.0; }\n    if (p * s8 <= a) { p *= s8; ip *= r8; e += 8.0; }\n")
            .Append("    if (p * s4 <= a) { p *= s4; ip *= r4; e += 4.0; }\n    if (p * s2 <= a) { p *= s2; ip *= r2; e += 2.0; }\n")
            .Append("    if (p * s1 <= a) { p *= s1; ip *= r1; e += 1.0; }\n  } else {\n")
            // smallest power of two above a, then half of it
            .Append("    float s = 0.5;\n    float r = 2.0;\n")
            .Append("    float s1 = s; float s2 = s1 * s1; float s4 = s2 * s2; float s8 = s4 * s4; float s16 = s8 * s8; float s32 = s16 * s16; float s64 = s32 * s32;\n")
            .Append("    float r1 = r; float r2 = r1 * r1; float r4 = r2 * r2; float r8 = r4 * r4; float r16 = r8 * r8; float r32 = r16 * r16; float r64 = r32 * r32;\n")
            .Append("    if (p * s64 > a) { p *= s64; ip *= r64; e -= 64.0; }\n    if (p * s32 > a) { p *= s32; ip *= r32; e -= 32.0; }\n")
            .Append("    if (p * s16 > a) { p *= s16; ip *= r16; e -= 16.0; }\n    if (p * s8 > a) { p *= s8; ip *= r8; e -= 8.0; }\n")
            .Append("    if (p * s4 > a) { p *= s4; ip *= r4; e -= 4.0; }\n    if (p * s2 > a) { p *= s2; ip *= r2; e -= 2.0; }\n")
            .Append("    if (p * s1 > a) { p *= s1; ip *= r1; e -= 1.0; }\n")
            .Append("    p *= 0.5;\n    ip *= 2.0;\n    e -= 1.0;\n  }\n")
            .Append("  float f = (a * ip - 1.0) * 8388608.0;\n  float be = e + 127.0;\n")
            .Append("  float b0 = mod(f, 256.0);\n  float b1 = mod(floor(f * 0.00390625), 256.0);\n")
            .Append("  float b2 = floor(f * 0.0000152587890625) + mod(be, 2.0) * 128.0;\n")
            .Append("  float b3 = floor(be * 0.5) + (x < 0.0 ? 128.0 : 0.0);\n")
            .Append("  return float4(b0, b1, b2, b3) / 255.0;\n}\n")
            .Append(CornerSource)
            .Append("half4 main(float2 p) {\n  float x = floor(p.x);\n  float y = floor(p.y);\n")
            .Append("  if (_sm_pos.y > 0.5) {\n    float h = floor(x * 0.5);\n")
            // corner c of the triangle list (triangle t, corner k), its vertex found like the draw program does;
            // a triangle with an index past the vertices (_sm_pos.z) gets 0 at all corners: drawn empty
            .Append("    float c = y * _sm_pos.x + h;\n    float t = floor((c + 0.5) / 3.0);\n    float k = c - t * 3.0;\n")
            .Append("    float v0 = _sm_corner(t, 0.0);\n    float v1 = _sm_corner(t, 1.0);\n    float v2 = _sm_corner(t, 2.0);\n")
            .Append("    if (max(v0, max(v1, v2)) >= _sm_pos.z) { return half4(0.0); }\n")
            .Append("    Varyings w = _sm_vs(_sm_vload(k < 0.5 ? v0 : (k < 1.5 ? v1 : v2)));\n")
            .Append("    return half4(_sm_enc(x - h * 2.0 < 0.5 ? w.position.x : w.position.y));\n  }\n")
            .Append("  Varyings v = _sm_vs(_sm_vload(y * _sm_pos.x + x));\n")
            .Append("  return half4(half2(v.position), 0.0, 1.0);\n}\n")
            .ToString();

        var positionEffect = SKRuntimeEffect.CreateShader(positionSource, out var vsErrors);
        if (positionEffect == null)
        {
            errors = "VS: " + vsErrors;
            return null;
        }

        bool passthrough = positionFloat >= 0 && copiesPosition;

        // A vertex program without uniforms gives the same varyings at every draw: they are computed once per vertex
        // range (varyings pass) and the draw program reads them, so it runs no vertex program per pixel and a Half
        // varying costs one fp16 read instead of two (Float: high + low half).
        bool drawsVaryings = !vsUniforms.Any(u => !u.IsChild);
        VaryingLayout layout = drawsVaryings ? VaryingLayout.Of(varys) : null;

        var drawSource = new StringBuilder()
            .Append(structs).Append('\n').Append(vs).Append('\n').Append(fs).Append('\n')
            .Append(BuildDrawMain(attrs, varys, floatIndex, texels, colorType, alphaType, layout))
            .ToString();

        var drawEffect = SKRuntimeEffect.CreateShader(drawSource, out var fsErrors);
        if (drawEffect == null)
        {
            positionEffect.Dispose();
            errors = "FS: " + fsErrors;
            return null;
        }

        SKRuntimeEffect varyingsEffect = null;
        string varyingsSource = null;
        if (layout != null)
        {
            varyingsSource = new StringBuilder()
                .Append(structs).Append('\n').Append(vs).Append('\n')
                .Append(BuildVaryingsPass(attrs, vertexStride / 4, layout))
                .ToString();
            varyingsEffect = SKRuntimeEffect.CreateShader(varyingsSource, out var varyingsErrors);
            if (varyingsEffect == null)
            {
                positionEffect.Dispose();
                drawEffect.Dispose();
                errors = "DrawnUi SKMesh stand-in: " + varyingsErrors;
                return null;
            }
        }

        var convertEffect = SKRuntimeEffect.CreateShader(BuildConvert(attrs, floatIndex, texels, vertexStride / 4), out var convertErrors);
        if (convertEffect == null)
        {
            positionEffect.Dispose();
            drawEffect.Dispose();
            varyingsEffect?.Dispose();
            errors = "DrawnUi SKMesh stand-in: " + convertErrors;
            return null;
        }

        // Uniform layout: Skia packs the vertex program's uniforms first, then the fragment program's own,
        // tightly (4-byte aligned), the same layout a runtime effect uses.
        var uniforms = new List<UniformSlot>();
        int uniformSize = 0;
        foreach (var u in vsUniforms.Concat(fsUniforms.Except(fsDuplicates)).Where(x => !x.IsChild))
        {
            int size = UniformBytes(u.Type, u.Array, out bool isInt);
            if (size <= 0)
            {
                positionEffect.Dispose();
                drawEffect.Dispose();
                convertEffect.Dispose();
                varyingsEffect?.Dispose();
                errors = $"Uniform {u.Name}: type '{u.Type}{u.Array}' is not supported by the DrawnUi SKMesh stand-in.";
                return null;
            }
            uniforms.Add(new UniformSlot(u.Name, uniformSize, size, isInt, u.Array.Length > 0));
            uniformSize += size;
        }
        var vertexUniforms = uniforms.Where(x => vsUniforms.Any(v => v.Name == x.Name)).ToArray();

        var children = fsUniforms.Where(x => x.IsChild).Select(x => new ChildSlot(x.Name, x.Type)).ToArray();

        if (passthrough)
        {
            positionEffect.Dispose();
            positionEffect = null;
        }

        return new SKMeshSpecification(vertexStride, attrs, texels, hasColor, convertEffect, drawEffect, positionEffect, positionFloat,
            uniforms.ToArray(), vertexUniforms, uniformSize, children) { _drawSource = drawSource, _varyingsSource = varyingsSource, VaryingsEffect = varyingsEffect, Varyings = layout };
    }

    static int UniformBytes(string type, string array, out bool isInt)
    {
        isInt = type.StartsWith("int");
        int one = type switch
        {
            "float" or "half" or "int" => 4,
            "float2" or "half2" or "int2" => 8,
            "float3" or "half3" or "int3" => 12,
            "float4" or "half4" or "int4" or "float2x2" or "half2x2" => 16,
            "float3x3" or "half3x3" => 36,
            "float4x4" or "half4x4" => 64,
            _ => 0,
        };
        if (array.Length == 0)
            return one;
        return int.TryParse(array.Trim('[', ']'), out var count) && count > 0 ? one * count : 0;
    }

    static string Validate(ReadOnlySpan<SKMeshSpecificationAttribute> attributes, int stride, ReadOnlySpan<SKMeshSpecificationVarying> varyings)
    {
        if (attributes.Length > MaxAttributes)
            return $"A maximum of {MaxAttributes} attributes is allowed.";
        if (stride <= 0 || stride % 4 != 0)
            return "Vertex stride must be a non-zero multiple of 4.";
        if (stride > MaxStride)
            return $"Stride cannot exceed {MaxStride}.";
        foreach (var a in attributes)
        {
            if (a.Offset < 0 || a.Offset % 4 != 0)
                return "Attribute offset must be a multiple of 4.";
            if (a.Offset + AttributeBytes(a.Type) > stride)
                return "Attribute offset plus size cannot exceed stride.";
            if (!IsIdentifier(a.Name))
                return $"\"{a.Name}\" is not a valid attribute name.";
        }
        bool hasPosition = false;
        foreach (var v in varyings)
        {
            if (!IsIdentifier(v.Name))
                return $"\"{v.Name}\" is not a valid varying name.";
            if (v.Name == "position")
            {
                if (v.Type != SKMeshSpecificationVaryingType.Float2)
                    return "Varying \"position\" must have type float2.";
                hasPosition = true;
            }
        }
        if (varyings.Length + (hasPosition ? 0 : 1) > MaxVaryings)
            return $"A maximum of {MaxVaryings} varyings is allowed.";
        return null;
    }

    static bool IsIdentifier(string name) => !string.IsNullOrEmpty(name) && IdentifierRegex.IsMatch(name);

    internal static int AttributeFloats(SKMeshSpecificationAttributeType type) => type switch
    {
        SKMeshSpecificationAttributeType.Float => 1,
        SKMeshSpecificationAttributeType.Float2 => 2,
        SKMeshSpecificationAttributeType.Float3 => 3,
        _ => 4,
    };

    static int AttributeBytes(SKMeshSpecificationAttributeType type)
        => type == SKMeshSpecificationAttributeType.Ubyte4Unorm ? 4 : AttributeFloats(type) * 4;

    static string AttributeSkslType(SKMeshSpecificationAttributeType type) => type switch
    {
        SKMeshSpecificationAttributeType.Float => "float",
        SKMeshSpecificationAttributeType.Float2 => "float2",
        SKMeshSpecificationAttributeType.Float3 => "float3",
        SKMeshSpecificationAttributeType.Float4 => "float4",
        _ => "half4",
    };

    static string VaryingSkslType(SKMeshSpecificationVaryingType type) => type switch
    {
        SKMeshSpecificationVaryingType.Float => "float",
        SKMeshSpecificationVaryingType.Float2 => "float2",
        SKMeshSpecificationVaryingType.Float3 => "float3",
        SKMeshSpecificationVaryingType.Float4 => "float4",
        SKMeshSpecificationVaryingType.Half => "half",
        SKMeshSpecificationVaryingType.Half2 => "half2",
        SKMeshSpecificationVaryingType.Half3 => "half3",
        _ => "half4",
    };

    static string BuildStructs(SKMeshSpecificationAttribute[] attrs, SKMeshSpecificationVarying[] varys)
    {
        var sb = new StringBuilder("struct Attributes {\n");
        foreach (var a in attrs)
            sb.Append("  ").Append(AttributeSkslType(a.Type)).Append(' ').Append(a.Name).Append(";\n");
        sb.Append("};\nstruct Varyings {\n");
        if (!varys.Any(v => v.Name == "position"))
            sb.Append("  float2 position;\n");
        foreach (var v in varys)
            sb.Append("  ").Append(VaryingSkslType(v.Type)).Append(' ').Append(v.Name).Append(";\n");
        sb.Append("};\n");
        return sb.ToString();
    }

    // Attributes _sm_vload(v): vertex v of the packed image (its texels side by side, _sm_pk.y vertices per row,
    // a power of two; _sm_pk.z = 1 / _sm_pk.y). _sm_t(x, y) reads one packed texel.
    static string BuildLoad(SKMeshSpecificationAttribute[] attrs, int[] floatIndex, int texels)
    {
        const string xyzw = "xyzw";
        var sb = new StringBuilder("Attributes _sm_load(float x, float y) {\n  Attributes a;\n");
        for (int t = 0; t < texels; t++)
            sb.Append($"  float4 t{t} = _sm_t(x + {t}.0, y);\n");
        for (int i = 0; i < attrs.Length; i++)
        {
            int n = AttributeFloats(attrs[i].Type);
            var parts = Enumerable.Range(floatIndex[i], n).Select(f => $"t{f / 4}.{xyzw[f % 4]}");
            var type = AttributeSkslType(attrs[i].Type);
            sb.Append("  a.").Append(attrs[i].Name).Append(" = ")
              .Append(n == 1 ? parts.First() : $"{type}({string.Join(", ", parts)})").Append(";\n");
        }
        sb.Append("  return a;\n}\n");
        sb.Append("Attributes _sm_vload(float v) {\n  float y = floor(v * _sm_pk.z);\n")
          .Append($"  return _sm_load((v - y * _sm_pk.y) * {texels}.0, y);\n}}\n");
        return sb.ToString();
    }

    // Convert program: the vertex bytes as they are (RGBA8, one float per texel, _sm_vtx.y texels per row, a
    // power of two, _sm_vtx.z = 1 / _sm_vtx.y, the mesh's first float at _sm_vtx.x) into the packed image:
    // output texel (x, y) = texel x % texels of vertex y * _sm_cv.x + x / texels.
    // _sm_cv.z = 1: plain F32 floats. Else rows [0, _sm_cv.y) hold fp16 high halves and the rows after them the
    // low halves: hi is the float rounded to 11 bits, lo = float - hi, both exact in fp16 storage and math
    // (~22 bits once added; values beyond fp16 range overflow, below 6e-5 lose bits).
    // Each float is rebuilt from its IEEE bytes with exact float math (exp2 is approximate on the CPU), so every
    // GPU and the CPU get the value the app wrote, whatever precision their texture reads have. Zero and
    // subnormals give 0.
    static string BuildConvert(SKMeshSpecificationAttribute[] attrs, int[] floatIndex, int texels, int strideFloats)
    {
        var sb = new StringBuilder("uniform shader _sm_attr;\nuniform float4 _sm_vtx;\nuniform float4 _sm_cv;\n");
        sb.Append("float4 _sm_b(float i) {\n  float y = floor(i * _sm_vtx.z);\n")
          .Append("  return floor(float4(_sm_attr.eval(float2(i - y * _sm_vtx.y + 0.5, y + 0.5))) * 255.0 + 0.5);\n}\n")
          // float i of the vertex bytes: plain, or its high or low fp16 part
          .Append("float _sm_f(float i, float lo) {\n  float4 b = _sm_b(i);\n")
          .Append("  float e = mod(b.w, 128.0) * 2.0 + floor(b.z * 0.0078125);\n")
          .Append("  if (e == 0.0) { return 0.0; }\n")
          .Append("  float m = mod(b.z, 128.0) * 65536.0 + b.y * 256.0 + b.x;\n")
          .Append("  float d = e - 127.0;\n  float a = abs(d);\n  float s = d < 0.0 ? 0.5 : 2.0;\n  float p = 1.0;\n")
          .Append("  for (int k = 0; k < 7; k++) {\n")
          .Append("    float h = floor(a * 0.5);\n    if (a - h * 2.0 > 0.5) { p *= s; }\n    s *= s;\n    a = h;\n  }\n")
          .Append("  float x = (m * 0.00000011920928955078125 + 1.0) * p;\n")
          .Append("  if (_sm_cv.z < 0.5) {\n    float hi = (floor(m * 0.0001220703125 + 0.5) * 0.0009765625 + 1.0) * p;\n    x = lo > 0.5 ? x - hi : hi;\n  }\n")
          .Append("  return b.w >= 128.0 ? -x : x;\n}\n");

        // Packed float q of a vertex: its source in the vertex bytes.
        var lanes = new string[texels * 4];
        for (int q = 0; q < lanes.Length; q++)
            lanes[q] = "0.0";
        for (int i = 0; i < attrs.Length; i++)
        {
            int n = AttributeFloats(attrs[i].Type);
            int f = attrs[i].Offset / 4;
            for (int c = 0; c < n; c++)
                lanes[floatIndex[i] + c] = attrs[i].Type == SKMeshSpecificationAttributeType.Ubyte4Unorm
                    ? $"(lo > 0.5 ? 0.0 : _sm_b(i + {f}.0).{"xyzw"[c]} / 255.0)" // already fp16-exact enough: unorm8
                    : $"_sm_f(i + {f + c}.0, lo)";
        }

        sb.Append("half4 main(float2 p) {\n  float y = floor(p.y);\n  float lo = 0.0;\n")
          .Append("  if (_sm_cv.z < 0.5 && y >= _sm_cv.y) { y -= _sm_cv.y; lo = 1.0; }\n")
          .Append($"  float x = floor(p.x);\n  float vx = floor((x + 0.5) / {texels}.0);\n  float j = x - vx * {texels}.0;\n")
          .Append($"  float i = _sm_vtx.x + (y * _sm_cv.x + vx) * {strideFloats}.0;\n")
          .Append("  float4 r = float4(0.0);\n");
        for (int t = 0; t < texels; t++)
        {
            sb.Append(t == 0 ? "  if (j < 0.5) " : $"  else if (j < {t}.5) ")
              .Append($"{{ r = float4({lanes[t * 4]}, {lanes[t * 4 + 1]}, {lanes[t * 4 + 2]}, {lanes[t * 4 + 3]}); }}\n");
        }
        sb.Append("  return half4(r);\n}\n");
        return sb.ToString();
    }

    // DrawVertices texCoords of triangle i: cell (i % W, i / W), corners at +0.125 / +0.875, so the
    // interpolated texCoord gives the triangle (floor) and its barycentrics (fraction).
    internal const float CornerLow = 0.125f;
    internal const float CornerHigh = 0.875f;

    // _sm_corner(t, k): the vertex of corner k of triangle t. _sm_grid.z: 1 for a triangle strip, .w: 1 when
    // indexed. _sm_ix.x: first index, .y: index image width (a power of two), .z: 1 / width. The index image
    // holds the index bytes as they are, 2 indices per texel.
    const string CornerSource =
        "uniform float4 _sm_grid;\nuniform float4 _sm_ix;\nuniform shader _sm_idx;\n" +
        "float _sm_corner(float t, float k) {\n  float c = _sm_grid.z > 0.5 ? t + k : t * 3.0 + k;\n" +
        "  if (_sm_grid.w > 0.5) {\n    c += _sm_ix.x;\n    float h = floor(c * 0.5);\n    float y = floor(h * _sm_ix.z);\n" +
        "    float4 b = floor(float4(_sm_idx.eval(float2(h - y * _sm_ix.y + 0.5, y + 0.5))) * 255.0 + 0.5);\n" +
        "    c = c - h * 2.0 > 0.5 ? b.z + b.w * 256.0 : b.x + b.y * 256.0;\n  }\n" +
        "  return c;\n}\n";

    static string BuildDrawMain(SKMeshSpecificationAttribute[] attrs, SKMeshSpecificationVarying[] varys, int[] floatIndex,
        int texels, string colorType, SKAlphaType alphaType, VaryingLayout layout)
    {
        var sb = new StringBuilder();
        if (layout != null)
            return BuildDrawVaryingsMain(varys, colorType, alphaType, layout);
        // _sm_grid.x: triangle cells per texCoord row, .y: paint alpha applied on top, .z: 1 for a triangle strip,
        // .w: 1 when indexed. _sm_ix.x: first index, .y: index image width (a power of two), .z: 1 / width.
        // _sm_pk: the packed fp16 image (see BuildLoad), .w: rows of its high half block (the low halves follow).
        sb.Append("uniform float4 _sm_pk;\nuniform shader _sm_attr;\nuniform shader _sm_paint;\n");
        if (colorType != null)
            sb.Append("uniform blender _sm_blend;\n");
        sb.Append("float4 _sm_t(float x, float y) {\n  float2 c = float2(x + 0.5, y + 0.5);\n")
          .Append("  return float4(_sm_attr.eval(c)) + float4(_sm_attr.eval(float2(c.x, c.y + _sm_pk.w)));\n}\n");
        sb.Append(BuildLoad(attrs, floatIndex, texels));
        sb.Append(CornerSource);
        sb.Append(BuildMixAndMain(varys, colorType, alphaType, "_sm_vs(_sm_vload(_sm_corner(t, K)))"));
        return sb.ToString();
    }

    // _sm_mix and main: the varyings of the 3 corners (`corner`, with t the triangle and K the corner) mixed by the
    // barycentrics, then the fragment program and the paint.
    static string BuildMixAndMain(SKMeshSpecificationVarying[] varys, string colorType, SKAlphaType alphaType, string corner)
    {
        var sb = new StringBuilder();
        sb.Append("Varyings _sm_mix(Varyings a, Varyings b, Varyings c, float3 w) {\n  Varyings v;\n");
        var fields = new List<(string name, bool half)> { ("position", false) };
        foreach (var v in varys)
            if (v.Name != "position")
                fields.Add((v.Name, v.Type >= SKMeshSpecificationVaryingType.Half));
        foreach (var (name, half) in fields)
        {
            if (half)
                sb.Append($"  v.{name} = a.{name} * half(w.x) + b.{name} * half(w.y) + c.{name} * half(w.z);\n");
            else
                sb.Append($"  v.{name} = a.{name} * w.x + b.{name} * w.y + c.{name} * w.z;\n");
        }
        sb.Append("  return v;\n}\n");

        float scale = 1f / (CornerHigh - CornerLow);
        var inv = scale.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        var low = CornerLow.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        sb.Append("half4 main(float2 p) {\n")
          .Append("  float cx = floor(p.x);\n  float cy = floor(p.y);\n")
          .Append($"  float b1 = (p.x - cx - {low}) * {inv};\n  float b2 = (p.y - cy - {low}) * {inv};\n")
          .Append("  float3 w = float3(1.0 - b1 - b2, b1, b2);\n")
          .Append("  float t = cy * _sm_grid.x + cx;\n")
          .Append($"  Varyings v = _sm_mix({corner.Replace("K", "0.0")}, {corner.Replace("K", "1.0")}, {corner.Replace("K", "2.0")}, w);\n");
        if (colorType == null)
        {
            sb.Append("  float2 lc = _sm_fs(v);\n")
              .Append("  return _sm_paint.eval(lc) * half(_sm_grid.y);\n}\n");
        }
        else
        {
            sb.Append($"  {colorType} color = {colorType}(0.0);\n")
              .Append("  float2 lc = _sm_fs(v, color);\n");
            if (alphaType == SKAlphaType.Opaque)
                sb.Append("  color.a = 1.0;\n");
            else if (alphaType == SKAlphaType.Unpremul)
                sb.Append("  color.rgb *= color.a;\n");
            sb.Append("  return _sm_blend.eval(_sm_paint.eval(lc), half4(color)) * half(_sm_grid.y);\n}\n");
        }
        return sb.ToString();
    }

    // Where the varyings pass stores each varying of a vertex: its texels side by side, the high texels first (Float
    // varyings rounded to fp16, Half varyings as they are), then the low texels (the rest of each Float varying, so
    // high + low gives ~22 bits). No varying straddles two texels; "position" has a texel of its own in both parts,
    // last, so a draw that does not read the position reads none of it.
    internal sealed class VaryingLayout
    {
        public readonly record struct Field(string Name, int Floats, bool Half, int Hi, int Lo); // Hi, Lo: float index in the vertex, Lo -1 for Half

        public Field[] Fields { get; private init; }
        public int HiTexels { get; private init; }
        public int Texels { get; private init; } // high + low

        public static VaryingLayout Of(SKMeshSpecificationVarying[] varys)
        {
            var named = varys.Where(v => v.Name != "position")
                .Select(v => (v.Name, n: VaryingFloats(v.Type), half: v.Type >= SKMeshSpecificationVaryingType.Half)).ToList();
            var hiRoom = new List<int>();
            var loRoom = new List<int>();
            static int Place(List<int> room, int n)
            {
                int t = room.FindIndex(free => free >= n);
                if (t < 0)
                {
                    room.Add(4);
                    t = room.Count - 1;
                }
                int at = t * 4 + 4 - room[t];
                room[t] -= n;
                return at;
            }
            var hi = new Dictionary<string, int>();
            var lo = new Dictionary<string, int>();
            foreach (var f in named.OrderByDescending(f => f.n))
            {
                hi[f.Name] = Place(hiRoom, f.n);
                if (!f.half)
                    lo[f.Name] = Place(loRoom, f.n);
            }
            int positionHi = hiRoom.Count * 4;
            hiRoom.Add(2);
            int positionLo = loRoom.Count * 4;
            loRoom.Add(2);
            int hiTexels = hiRoom.Count;
            var fields = new List<Field> { new("position", 2, false, positionHi, hiTexels * 4 + positionLo) };
            foreach (var f in named)
                fields.Add(new Field(f.Name, f.n, f.half, hi[f.Name], f.half ? -1 : hiTexels * 4 + lo[f.Name]));
            return new VaryingLayout { Fields = fields.ToArray(), HiTexels = hiTexels, Texels = hiTexels + loRoom.Count };
        }

        static int VaryingFloats(SKMeshSpecificationVaryingType type) => type switch
        {
            SKMeshSpecificationVaryingType.Float or SKMeshSpecificationVaryingType.Half => 1,
            SKMeshSpecificationVaryingType.Float2 or SKMeshSpecificationVaryingType.Half2 => 2,
            SKMeshSpecificationVaryingType.Float3 or SKMeshSpecificationVaryingType.Half3 => 3,
            _ => 4,
        };
    }

    // Varyings pass: output texel (x, y) = texel x % Texels of vertex y * _sm_vy.x + x / Texels: the vertex program run
    // once on the vertex bytes as they are (RGBA8 image, one float per texel; _sm_vtx as in BuildConvert), then the
    // texel's 4 lanes picked. A high lane of a Float varying holds the value rounded to 11 significant bits (exact in
    // fp16), its low lane the rest; both are written as they are into an RgbaF16 target. Floats are rebuilt from
    // their bytes and split with exact float math (powers of two by squaring, choices made by multiplying with 0 or
    // 1): the same on every GPU. Everything works on 4 floats at once and runs once per pixel: a small program
    // (a vertex program per branch made one too big for Mali's compiler).
    static string BuildVaryingsPass(SKMeshSpecificationAttribute[] attrs, int strideFloats, VaryingLayout layout)
    {
        const string xyzw = "xyzw";
        var sb = new StringBuilder("uniform shader _sm_attr;\nuniform float4 _sm_vtx;\nuniform float4 _sm_vy;\n");
        sb.Append("float4 _sm_b(float i) {\n  float y = floor(i * _sm_vtx.z);\n")
          .Append("  return floor(float4(_sm_attr.eval(float2(i - y * _sm_vtx.y + 0.5, y + 0.5))) * 255.0 + 0.5);\n}\n")
          // floats i .. i + 3 of the vertex bytes (zero and subnormals: 0)
          .Append("float4 _sm_f4(float i) {\n")
          .Append("  float4 b0 = _sm_b(i);\n  float4 b1 = _sm_b(i + 1.0);\n  float4 b2 = _sm_b(i + 2.0);\n  float4 b3 = _sm_b(i + 3.0);\n")
          .Append("  float4 bx = float4(b0.x, b1.x, b2.x, b3.x);\n  float4 by = float4(b0.y, b1.y, b2.y, b3.y);\n")
          .Append("  float4 bz = float4(b0.z, b1.z, b2.z, b3.z);\n  float4 bw = float4(b0.w, b1.w, b2.w, b3.w);\n")
          .Append("  float4 e = mod(bw, 128.0) * 2.0 + floor(bz * 0.0078125);\n")
          .Append("  float4 m = mod(bz, 128.0) * 65536.0 + by * 256.0 + bx;\n")
          .Append("  float4 d = e - 127.0;\n  float4 a = abs(d);\n")
          .Append("  float4 s = 2.0 - 1.5 * step(d, float4(-0.5));\n  float4 p = float4(1.0);\n")
          .Append("  for (int k = 0; k < 7; k++) {\n")
          .Append("    float4 h = floor(a * 0.5);\n    float4 bit = a - h * 2.0;\n")
          .Append("    p = p * (1.0 - bit) + (p * s) * bit;\n    s *= s;\n    a = h;\n  }\n")
          .Append("  float4 x = (m * 0.00000011920928955078125 + 1.0) * p * (1.0 - step(e, float4(0.5)));\n")
          .Append("  return x * (1.0 - 2.0 * step(float4(128.0), bw));\n}\n")
          // x rounded to 11 significant bits per lane; under 2^-14 (fp16 subnormals): 0, all in the low part. The power of
          // two is searched for at least 2^-14 (sa): for 0 the search reached 2^128, infinite, and 0 x infinity is NaN on
          // Mali (desktop GPUs and the CPU happened to give 0)
          .Append("float4 _sm_hi4(float4 x) {\n  float4 a = abs(x);\n  float4 sa = max(a, float4(0.00006103515625));\n  float4 big = step(float4(1.0), a);\n")
          .Append("  float4 s1 = 0.5 + 1.5 * big; float4 s2 = s1 * s1; float4 s4 = s2 * s2; float4 s8 = s4 * s4; float4 s16 = s8 * s8; float4 s32 = s16 * s16; float4 s64 = s32 * s32;\n")
          .Append("  float4 r1 = 2.0 - 1.5 * big; float4 r2 = r1 * r1; float4 r4 = r2 * r2; float4 r8 = r4 * r4; float4 r16 = r8 * r8; float4 r32 = r16 * r16; float4 r64 = r32 * r32;\n")
          .Append("  float4 p = float4(1.0);\n  float4 ip = float4(1.0);\n  float4 t;\n  float4 c;\n");
        // big lanes: largest power of two <= a; small lanes: smallest power of two > a (halved below)
        foreach (var k in new[] { 64, 32, 16, 8, 4, 2, 1 })
            sb.Append($"  t = p * s{k};\n  c = big * step(t, sa) + (1.0 - big) * (1.0 - step(t, sa));\n")
              .Append($"  p = p * (1.0 - c) + t * c;\n  ip = ip * (1.0 - c) + (ip * r{k}) * c;\n");
        sb.Append("  p = p * big + (p * 0.5) * (1.0 - big);\n  ip = ip * big + (ip * 2.0) * (1.0 - big);\n")
          .Append("  float4 h = floor(a * ip * 1024.0 + 0.5) * 0.0009765625 * p * step(float4(0.00006103515625), a);\n")
          .Append("  return h * sign(x);\n}\n");

        sb.Append("Attributes _sm_rload(float i) {\n  Attributes a;\n");
        foreach (var attr in attrs)
        {
            int f = attr.Offset / 4;
            int n = AttributeFloats(attr.Type);
            sb.Append("  a.").Append(attr.Name).Append(" = ");
            if (attr.Type == SKMeshSpecificationAttributeType.Ubyte4Unorm)
                sb.Append($"half4(_sm_b(i + {f}.0) / 255.0)");
            else
                sb.Append($"_sm_f4(i + {f}.0){(n == 4 ? "" : "." + xyzw.Substring(0, n))}");
            sb.Append(";\n");
        }
        sb.Append("  return a;\n}\n");

        // the varyings once, the high parts of each Float varying once, then texel j's 4 lanes
        int t = layout.Texels;
        sb.Append("half4 main(float2 p) {\n  float x = floor(p.x);\n  float y = floor(p.y);\n")
          .Append($"  float vx = floor((x + 0.5) / {t}.0);\n  float j = x - vx * {t}.0;\n")
          .Append($"  Varyings v = _sm_vs(_sm_rload(_sm_vtx.x + (y * _sm_vy.x + vx) * {strideFloats}.0));\n");
        var lanes = new string[t * 4];
        for (int q = 0; q < lanes.Length; q++)
            lanes[q] = "0.0";
        int index = 0;
        foreach (var f in layout.Fields)
        {
            string Comp(int c) => f.Floats == 1 ? $"float(v.{f.Name})" : $"float(v.{f.Name}.{xyzw[c]})";
            if (f.Half)
            {
                for (int c = 0; c < f.Floats; c++)
                    lanes[f.Hi + c] = Comp(c);
                continue;
            }
            var hi = $"_sm_h{index++}";
            var padded = string.Join(", ", Enumerable.Range(0, 4).Select(c => c < f.Floats ? Comp(c) : "0.0"));
            sb.Append($"  float4 {hi} = _sm_hi4(float4({padded}));\n");
            for (int c = 0; c < f.Floats; c++)
            {
                lanes[f.Hi + c] = $"{hi}.{xyzw[c]}";
                lanes[f.Lo + c] = $"({Comp(c)} - {hi}.{xyzw[c]})";
            }
        }
        sb.Append("  float4 r = float4(0.0);\n");
        for (int k = 0; k < t; k++)
            sb.Append(k == 0 ? "  if (j < 0.5) " : $"  else if (j < {k}.5) ")
              .Append($"{{ r = float4({lanes[k * 4]}, {lanes[k * 4 + 1]}, {lanes[k * 4 + 2]}, {lanes[k * 4 + 3]}); }}\n");
        sb.Append("  return half4(r);\n}\n");
        return sb.ToString();
    }

    // Draw program reading the varyings image (_sm_attr; _sm_pk.y vertices per row, a power of two, .z = 1 / that).
    static string BuildDrawVaryingsMain(SKMeshSpecificationVarying[] varys, string colorType, SKAlphaType alphaType, VaryingLayout layout)
    {
        const string xyzw = "xyzw";
        var sb = new StringBuilder("uniform float4 _sm_pk;\nuniform shader _sm_attr;\nuniform shader _sm_paint;\n");
        if (colorType != null)
            sb.Append("uniform blender _sm_blend;\n");
        sb.Append("Varyings _sm_vary(float v) {\n  float y = floor(v * _sm_pk.z);\n")
          .Append($"  float x = (v - y * _sm_pk.y) * {layout.Texels}.0 + 0.5;\n  y += 0.5;\n");
        for (int k = 0; k < layout.Texels; k++)
            sb.Append($"  float4 t{k} = float4(_sm_attr.eval(float2(x + {k}.0, y)));\n");
        sb.Append("  Varyings o;\n");
        foreach (var f in layout.Fields)
        {
            var parts = Enumerable.Range(0, f.Floats).Select(c =>
            {
                int h = f.Hi + c;
                var hi = $"t{h / 4}.{xyzw[h % 4]}";
                if (f.Half)
                    return hi;
                int l = f.Lo + c;
                return $"({hi} + t{l / 4}.{xyzw[l % 4]})";
            }).ToArray();
            var type = varys.FirstOrDefault(v => v.Name == f.Name) is { Name: not null } decl && f.Name != "position"
                ? VaryingSkslType(decl.Type)
                : "float2";
            sb.Append($"  o.{f.Name} = {(f.Floats == 1 ? $"{type}({parts[0]})" : $"{type}({string.Join(", ", parts)})")};\n");
        }
        sb.Append("  return o;\n}\n");
        sb.Append(CornerSource);
        sb.Append(BuildMixAndMain(varys, colorType, alphaType, "_sm_vary(_sm_corner(t, K))"));
        return sb.ToString();
    }

    // The vertex program writes `v.position = a.position;` and touches v.position nowhere else: DrawVertices
    // can take the positions straight from the "position" attribute, no CPU run of the vertex program.
    static bool IsPositionPassthrough(string vs)
    {
        var main = VsMainRegex.Match(vs);
        if (!main.Success)
            return false;
        var attr = main.Groups[1].Value;
        var assign = Regex.Match(vs, $@"\b(\w+)\.position\s*=\s*{Regex.Escape(attr)}\.position\s*;");
        if (!assign.Success)
            return false;
        var v = assign.Groups[1].Value;
        var rest = vs.Remove(assign.Index, assign.Length);
        return !Regex.IsMatch(rest, $@"\b{Regex.Escape(v)}\.position\b")         // no other write or read
               && !Regex.IsMatch(rest, $@"[(,]\s*{Regex.Escape(v)}\s*[,)]")       // not passed to a function
               && !Regex.IsMatch(rest, $@"\b{Regex.Escape(v)}\s*=[^=]");            // not reassigned whole
    }

    record UniformDecl(string Type, string Name, string Array, bool IsChild, int Index, int Length);

    static List<UniformDecl> ParseUniforms(string src)
    {
        var list = new List<UniformDecl>();
        foreach (Match m in UniformRegex.Matches(src))
        {
            var type = m.Groups[1].Value;
            list.Add(new UniformDecl(type, m.Groups[2].Value, m.Groups[3].Value.Replace(" ", ""),
                type is "shader" or "colorFilter" or "blender", m.Index, m.Length));
        }
        return list;
    }

    static HashSet<string> TopLevelNames(string src)
    {
        var set = new HashSet<string>();
        foreach (Match m in FunctionRegex.Matches(src))
            if (!Keywords.Contains(m.Groups[1].Value))
                set.Add(m.Groups[1].Value);
        foreach (Match m in StructRegex.Matches(src))
            set.Add(m.Groups[1].Value);
        foreach (Match m in ConstRegex.Matches(src))
            set.Add(m.Groups[1].Value);
        return set;
    }

    // Comments out, line breaks kept (error line numbers stay meaningful).
    static string StripComments(string src)
        => CommentRegex.Replace(src, m => Regex.Replace(m.Value, @"[^\n]", " "));

    static readonly HashSet<string> Keywords = new() { "if", "for", "while", "do", "switch", "return", "else" };
    static readonly Regex CommentRegex = new(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
    static readonly Regex MainRegex = new(@"\bmain\s*\(", RegexOptions.Compiled);
    static readonly Regex VsMainRegex = new(@"\bmain\s*\(\s*(?:const\s+)?Attributes\s+(\w+)\s*\)", RegexOptions.Compiled);
    static readonly Regex FsMainRegex = new(@"\bmain\s*\(([^)]*)\)", RegexOptions.Compiled);
    static readonly Regex FsColorParamRegex = new(@",\s*out\s+(half4|float4)\s+\w+\s*$", RegexOptions.Compiled);
    static readonly Regex IdentifierRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    static readonly Regex UniformRegex = new(@"(?:layout\s*\([^)]*\)\s*)?\buniform\s+(\w+)\s+(\w+)\s*(\[\s*\w*\s*\])?\s*;", RegexOptions.Compiled);
    static readonly Regex FunctionRegex = new(@"(?m)^\s*(?:inline\s+|noinline\s+)?\w+\s+(\w+)\s*\([^;{}]*\)\s*\{", RegexOptions.Compiled);
    static readonly Regex StructRegex = new(@"\bstruct\s+(\w+)", RegexOptions.Compiled);
    static readonly Regex ConstRegex = new(@"(?m)^\s*const\s+\w+\s+(\w+)\s*=", RegexOptions.Compiled);

    /// <summary>Releases the compiled programs.</summary>
    public void Dispose()
    {
        lock (EffectLock)
        {
            _drawUniforms?.Dispose();
            _drawChildren?.Dispose();
            _positionUniforms?.Dispose();
            _positionChildren?.Dispose();
            _convertUniforms?.Dispose();
            _convertChildren?.Dispose();
            _drawSolidUniforms?.Dispose();
            _drawSolidChildren?.Dispose();
            _varyingsUniforms?.Dispose();
            _varyingsChildren?.Dispose();
            _varyingsUniforms = null;
            _varyingsChildren = null;
            _drawUniforms = _positionUniforms = _convertUniforms = _drawSolidUniforms = null;
            _drawChildren = _positionChildren = _convertChildren = _drawSolidChildren = null;
        }
        _drawSolidEffect?.Dispose();
        VaryingsEffect?.Dispose();
        ConvertEffect?.Dispose();
        DrawEffect?.Dispose();
        PositionEffect?.Dispose();
    }
}
