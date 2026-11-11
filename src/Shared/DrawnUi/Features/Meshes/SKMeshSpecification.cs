#nullable disable
using System.Collections.Generic;
using System.Linq;
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
    internal int TexelsPerVertex { get; }      // RGBA texels holding the packed attribute floats (Ubyte4Unorm as 4 floats)
    internal bool HasColor { get; }

    // Draw program: vertex program run 3 times per pixel + fragment program, fed by DrawVertices texCoords.
    internal SKRuntimeEffect DrawEffect { get; }
    // Vertex program alone, run on the CPU (raster) to get the vertex positions DrawVertices needs.
    // Null when the vertex program copies the "position" attribute as is.
    internal SKRuntimeEffect PositionEffect { get; }
    internal int PositionAttributeFloat { get; } = -1; // float index of the "position" attribute in a packed vertex

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

    SKMeshSpecification(int stride, SKMeshSpecificationAttribute[] attributes, int texels, bool hasColor, SKRuntimeEffect draw, SKRuntimeEffect positions, int positionFloat,
        UniformSlot[] uniforms, UniformSlot[] vertexUniforms, int uniformSize, ChildSlot[] children)
    {
        Stride = stride;
        Attributes = attributes;
        TexelsPerVertex = texels;
        HasColor = hasColor;
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

        // Packed attribute floats.
        var attrs = attributes.ToArray();
        var varys = varyings.ToArray();
        int floats = 0;
        int positionFloat = -1;
        var floatIndex = new int[attrs.Length];
        for (int i = 0; i < attrs.Length; i++)
        {
            floatIndex[i] = floats;
            if (attrs[i].Name == "position" && attrs[i].Type == SKMeshSpecificationAttributeType.Float2)
                positionFloat = floats;
            floats += AttributeFloats(attrs[i].Type);
        }
        int texels = (floats + 3) / 4;

        var structs = BuildStructs(attrs, varys);

        // Vertex program alone: checked first so its errors carry the "VS:" prefix, and used on the CPU for
        // positions when the vertex program does more than copy the "position" attribute.
        var positionSource = new StringBuilder()
            .Append(structs).Append('\n').Append(vs).Append('\n')
            .Append("uniform shader _sm_attr;\n")
            .Append("float4 _sm_t(float x, float y) { return float4(_sm_attr.eval(float2(x + 0.5, y + 0.5))); }\n")
            .Append(BuildLoad(attrs, floatIndex, texels))
            .Append("half4 main(float2 p) {\n")
            .Append("  float vx = floor(p.x);\n  float vy = floor(p.y);\n")
            .Append($"  Varyings v = _sm_vs(_sm_load(vx * {texels}.0, vy));\n")
            .Append("  return half4(half2(v.position), 0.0, 1.0);\n}\n")
            .ToString();

        var positionEffect = SKRuntimeEffect.CreateShader(positionSource, out var vsErrors);
        if (positionEffect == null)
        {
            errors = "VS: " + vsErrors;
            return null;
        }

        bool passthrough = positionFloat >= 0 && copiesPosition;

        var drawSource = new StringBuilder()
            .Append(structs).Append('\n').Append(vs).Append('\n').Append(fs).Append('\n')
            .Append(BuildDrawMain(attrs, varys, floatIndex, texels, colorType, alphaType))
            .ToString();

        var drawEffect = SKRuntimeEffect.CreateShader(drawSource, out var fsErrors);
        if (drawEffect == null)
        {
            positionEffect.Dispose();
            errors = "FS: " + fsErrors;
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

        return new SKMeshSpecification(vertexStride, attrs, texels, hasColor, drawEffect, positionEffect, positionFloat,
            uniforms.ToArray(), vertexUniforms, uniformSize, children);
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

    // Attributes _sm_load(x, y): reads one vertex from the attribute image, its texels starting at column x.
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
        return sb.ToString();
    }

    // DrawVertices texCoords of triangle i: cell (i % W, i / W), corners at +0.125 / +0.875, so the
    // interpolated texCoord gives the triangle (floor) and its barycentrics (fraction).
    internal const float CornerLow = 0.125f;
    internal const float CornerHigh = 0.875f;

    static string BuildDrawMain(SKMeshSpecificationAttribute[] attrs, SKMeshSpecificationVarying[] varys, int[] floatIndex,
        int texels, string colorType, SKAlphaType alphaType)
    {
        var sb = new StringBuilder();
        // _sm_grid.x: rows of the high half block (the low halves follow), .y: paint alpha applied on top.
        sb.Append("uniform float4 _sm_grid;\nuniform shader _sm_attr;\nuniform shader _sm_paint;\n");
        if (colorType != null)
            sb.Append("uniform blender _sm_blend;\n");
        // Each attribute float is stored as two halves (hi + lo): exact in fp16 storage and fp16 shader math,
        // ~22 bits once added in float.
        sb.Append("float4 _sm_t(float x, float y) {\n  float2 c = float2(x + 0.5, y + 0.5);\n")
          .Append("  return float4(_sm_attr.eval(c)) + float4(_sm_attr.eval(float2(c.x, c.y + _sm_grid.x)));\n}\n");
        sb.Append(BuildLoad(attrs, floatIndex, texels));

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
          .Append($"  float x0 = cx * {3 * texels}.0;\n")
          .Append($"  Varyings v = _sm_mix(_sm_vs(_sm_load(x0, cy)), _sm_vs(_sm_load(x0 + {texels}.0, cy)), _sm_vs(_sm_load(x0 + {2 * texels}.0, cy)), w);\n");
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
        DrawEffect?.Dispose();
        PositionEffect?.Dispose();
    }
}
